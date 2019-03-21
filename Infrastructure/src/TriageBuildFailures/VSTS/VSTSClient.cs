﻿// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using TriageBuildFailures.Abstractions;
using TriageBuildFailures.VSTS.Models;

namespace TriageBuildFailures.VSTS
{
    public class VSTSClient : ICIClient
    {
        private enum ApiVersion
        {
            V4_1_Preview2,
            V5_0_Preview2,
            V5_0_Preview3,
            V5_0_Preview4,
            V5_0_Preview5,
            Default = V4_1_Preview2,
        }

        private readonly VSTSConfig Config;
        private readonly IReporter _reporter;

        public VSTSClient(VSTSConfig vstsConfig, IReporter reporter)
        {
            Config = vstsConfig;
            _reporter = reporter;
        }

        public async Task<IEnumerable<ICIBuild>> GetFailedBuildsAsync(DateTime startDate)
        {
            var projects = await GetProjects();

            var results = new List<ICIBuild>();
            foreach (var project in projects)
            {
                var builds = await GetBuildsForProject(project, VSTSBuildResult.Failed, VSTSBuildStatus.Completed, startDate);
                results.AddRange(builds.Select(b => new VSTSBuild(b)));
            }

            return results;
        }

        public async Task<IEnumerable<string>> GetTagsAsync(ICIBuild build)
        {
            var vstsBuild = (VSTSBuild)build;
            var result = await MakeVSTSRequest<VSTSArray<string>>(HttpMethod.Get, $"{vstsBuild.Project}/_apis/build/builds/{build.Id}/tags");

            return result.Value;
        }

        public async Task SetTagAsync(ICIBuild build, string tag)
        {
            var vstsBuild = (VSTSBuild)build;
            await MakeVSTSRequest<VSTSArray<string>>(HttpMethod.Put, $"{vstsBuild.Project}/_apis/build/builds/{build.Id}/tags/{tag}", apiVersion: ApiVersion.V5_0_Preview2);
        }

        public async Task<string> GetBuildLogAsync(ICIBuild build)
        {
            var vstsBuild = (VSTSBuild)build;
            var logs = await MakeVSTSRequest<VSTSArray<VSTSBuildLog>>(HttpMethod.Get, $"{vstsBuild.Project}/_apis/build/builds/{build.Id}/logs");
            var validationResults = GetBuildLogFromValidationResult(vstsBuild);
            if (logs == null)
            {
                return validationResults;
            }
            else
            {
                StringBuilder builder = new StringBuilder();
                foreach (var log in logs.Value)
                {
                    using (var stream = await MakeVSTSRequest(HttpMethod.Get, $"{vstsBuild.Project}/_apis/build/builds/{build.Id}/logs/{log.Id}", "text/plain"))
                    using (var streamReader = new StreamReader(stream))
                    {
                        builder.Append(await streamReader.ReadToEndAsync());
                    }
                }

                var result = builder.ToString();

                if (string.IsNullOrEmpty(result))
                {
                    return validationResults;
                }
                else
                {
                    return result;
                }
            }
        }

        public async Task<IEnumerable<ICITestOccurrence>> GetTestsAsync(ICIBuild build, BuildStatus? buildStatus = null)
        {
            var runs = await GetTestRuns(build);

            var result = new List<ICITestOccurrence>();
            foreach (var run in runs)
            {
                var results = await GetTestResults(run, buildStatus);
                result.AddRange(results.Select(r => new VSTSTestOccurrence(r)));
            }

            return result;
        }

        public Task<string> GetTestFailureTextAsync(ICITestOccurrence failure)
        {
            var vstsTest = failure as VSTSTestOccurrence;

            return Task.FromResult(vstsTest.TestCaseResult.ErrorMessage);
        }

        public async Task<VSTSBuild> GetBuild(string url)
        {
            var uri = new Uri(url);
            var query = HttpUtility.ParseQueryString(uri.Query);
            var id = query.Get("buildId");
            string project;
            if (url.Contains("dev.azure.com"))
            {
                project = url.Split('/', StringSplitOptions.RemoveEmptyEntries)[3];
            }
            else if (url.Contains("dnceng.visualstudio.com"))
            {
                project = url.Split('/', StringSplitOptions.RemoveEmptyEntries)[2];
            }
            else
            {
                throw new NotImplementedException("Unsupported url format");
            }
            var vstsUri = $"{project}/_apis/build/builds/{id}";
            var build = await MakeVSTSRequest<Build>(HttpMethod.Get, vstsUri,apiVersion: ApiVersion.V5_0_Preview5);
            return new VSTSBuild(build);
        }

        private string GetBuildLogFromValidationResult(VSTSBuild vstsBuild)
        {
            if (vstsBuild.ValidationResults != null && vstsBuild.ValidationResults.Any(v => v.Result.Equals("error", StringComparison.OrdinalIgnoreCase)))
            {
                var logStr = "";
                foreach (var validationResult in vstsBuild.ValidationResults.Where(v => v.Result.Equals("error", StringComparison.OrdinalIgnoreCase)))
                {
                    logStr += validationResult.Message + Environment.NewLine;
                }
                return logStr;
            }
            else
            {
                return null;
            }
        }

        private async Task<IEnumerable<VSTSTestCaseResult>> GetTestResults(VSTSTestRun run, BuildStatus? buildResult = null)
        {
            var queryItems = new Dictionary<string, string>();

            if (buildResult != null)
            {
                queryItems.Add("outcomes", GetStatusString(buildResult.Value));
            }

            var results = await MakeVSTSRequest<VSTSArray<VSTSTestCaseResult>>(HttpMethod.Get, $"{run.Project.Id}/_apis/test/runs/{run.Id}/results", queryItems, ApiVersion.V5_0_Preview5);

            return results.Value;
        }

        private string GetStatusString(BuildStatus status)
        {
            switch (status)
            {
                case BuildStatus.FAILURE:
                    return "Failed";
                case BuildStatus.SUCCESS:
                    return "Passed";
                default:
                    throw new NotImplementedException($"We don't know what to do with {Enum.GetName(typeof(BuildStatus), status)}");

            }
        }

        private async Task<IEnumerable<VSTSTestRun>> GetTestRuns(ICIBuild build)
        {
            var vstsBuild = (VSTSBuild)build;
            var queryItems = new Dictionary<string, string>
            {
                { "buildUri", vstsBuild.Uri.ToString() },
                { "includeRunDetails", "true" }
            };

            var runs = await MakeVSTSRequest<VSTSArray<VSTSTestRun>>(HttpMethod.Get, $"{vstsBuild.Project}/_apis/test/runs", queryItems);
            return runs.Value;
        }

        private async Task<IEnumerable<Build>> GetBuildsForProject(VSTSProject project, VSTSBuildResult? result = null, VSTSBuildStatus? status = null, DateTime? minTime = null)
        {
            var queryItems = new Dictionary<string, string>();
            if (result != null)
            {
                queryItems["resultFilter"] = Enum.GetName(typeof(VSTSBuildResult), result.Value).ToLowerInvariant();
            }

            if (status != null)
            {
                queryItems["statusFilter"] = Enum.GetName(typeof(BuildStatus), status.Value).ToLowerInvariant();
            }

            if (minTime != null)
            {
                queryItems["minTime"] = minTime.Value.ToString("yyyy'-'MM'-'ddTHH':'mm':'ss'Z'");
            }
            var builds = (await MakeVSTSRequest<VSTSArray<Build>>(HttpMethod.Get, $"{project.Id}/_apis/build/builds", queryItems, ApiVersion.V5_0_Preview4)).Value;

            // Only look at aspnet builds, and ignore PR builds.
            return builds.Where(build =>
               build.Definition.Path.StartsWith(Config.BuildPath, StringComparison.OrdinalIgnoreCase) && !build.TriggerInfo.ContainsKey("pr.sourceBranch"));
        }

        private async Task<IEnumerable<VSTSProject>> GetProjects()
        {
            var projectsObj = await MakeVSTSRequest<VSTSArray<VSTSProject>>(HttpMethod.Get, "_apis/projects");
            return projectsObj.Value;
        }

        private async Task<T> MakeVSTSRequest<T>(HttpMethod verb, string uri, IDictionary<string, string> queryItems = null, ApiVersion apiVersion = ApiVersion.Default) where T : class
        {
            using (var stream = await MakeVSTSRequest(verb, uri, "application/json", queryItems, apiVersion))
            using (var sr = new StreamReader(stream))
            using (var reader = new JsonTextReader(sr))
            {
                var serializer = new JsonSerializer
                {
                    MissingMemberHandling = MissingMemberHandling.Ignore
                };
                return serializer.Deserialize<T>(reader);
            }
        }

        private string GetVersionString(ApiVersion version)
        {
            string result;
            switch (version)
            {
                case ApiVersion.V4_1_Preview2:
                    result = "4.1-preview.2";
                    break;
                case ApiVersion.V5_0_Preview2:
                    result = "5.0-preview.2";
                    break;
                case ApiVersion.V5_0_Preview4:
                    result = "5.0-preview.4";
                    break;
                case ApiVersion.V5_0_Preview5:
                    result = "5.0-preview.5";
                    break;
                default:
                    throw new NotImplementedException($"We don't know about enum {Enum.GetName(typeof(ApiVersion), version)}.");
            }

            return result;
        }

        private async Task<Stream> MakeVSTSRequest(
            HttpMethod verb,
            string uri,
            string accept,
            IDictionary<string, string> queryItems = null,
            ApiVersion apiVersion = ApiVersion.Default)
        {
            var credentials = GetCredentials();
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

                var query = HttpUtility.ParseQueryString(string.Empty);
                query["api-version"] = GetVersionString(apiVersion);

                if (queryItems != null)
                {
                    foreach (var kvp in queryItems)
                    {
                        query[kvp.Key] = kvp.Value;
                    }
                }
                var uriBuilder = new UriBuilder("https", $"{Config.Account}.visualstudio.com")
                {
                    Path = uri,
                    Query = query.ToString()
                };

                var response = await RetryHelpers.RetryHttpRequesetAsync(client, verb, uriBuilder.Uri, _reporter);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var stream = new MemoryStream();
                    var writer = new StreamWriter(stream);
                    writer.Write(content);
                    writer.Flush();
                    stream.Position = 0;

                    return stream;
                }
                else
                {
                    var content = await response.Content.ReadAsStringAsync();
                    if(response.StatusCode == HttpStatusCode.InternalServerError && content.Contains("TF400714"))
                    {
                        //The log is missing. This is VSTS's fault, nothing we can do.
                        var stream = new MemoryStream();
                        var writer = new StreamWriter(stream);
                        writer.Write("");
                        writer.Flush();
                        stream.Position = 0;

                        return stream;
                    }
                    else
                    {
                        throw new HttpRequestException(content);
                    }
                }
            }
        }

        private string GetCredentials()
        {
            return Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", "", Config.PersonalAccessToken)));
        }
    }
}
