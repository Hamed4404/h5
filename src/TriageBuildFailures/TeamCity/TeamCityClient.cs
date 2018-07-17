// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Web;
using System.Xml.Serialization;
using Common;
using McMaster.Extensions.CommandLineUtils;

namespace TriageBuildFailures.TeamCity
{
    public class TeamCityClientWrapper
    {
        private readonly IReporter _reporter;

        private const int _defaultCount = 1000000;

        public TeamCityConfig Config { get; private set; }

        public TeamCityClientWrapper(TeamCityConfig config, IReporter reporter)
        {
            Config = config;
            _reporter = reporter;
            
            if (TeamCityBuild.BuildNames == null)
            {
                TeamCityBuild.BuildNames = GetBuildTypes();
            }
        }
        
        public string GetTestFailureText(TestOccurrence test)
        {
            var url = $"failedTestText.html?buildId={test.BuildId}&testId={test.TestId}";
            using (var stream = MakeTeamCityRequest(HttpMethod.Get, url, timeout: TimeSpan.FromMinutes(1)))
            using (var reader = new StreamReader(stream))
            {
                var error = reader.ReadToEnd().Trim();
                error = HttpUtility.HtmlDecode(error);
                var lines = error.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                bool firstEqualLineSeen = false;

                var preservedLines = new List<string>();

                foreach (var line in lines)
                {
                    if (line.StartsWith("======= Failed test run", StringComparison.OrdinalIgnoreCase))
                    {
                        if(firstEqualLineSeen)
                        {
                            break;
                        }
                        else
                        {
                            firstEqualLineSeen = true;
                        }
                    }
                    else
                    {
                        preservedLines.Add(line);
                    }
                }

                return string.Join(Environment.NewLine, preservedLines);
            }
        }

        public IEnumerable<TestOccurrence> GetTests(TeamCityBuild build)
        {
            var locator = $"build:(id:{build.Id})";
            var fields = "testOccurrence(test:id,id,name,status,duration)";

            var url = $"httpAuth/app/rest/testOccurrences?locator={locator},count:{_defaultCount}&fields={fields}";
            using (var stream = MakeTeamCityRequest(HttpMethod.Get, url, timeout: TimeSpan.FromMinutes(5)))
            {
                var serializer = new XmlSerializer(typeof(TestOccurrences));
                var tests = serializer.Deserialize(stream) as TestOccurrences;

                var results = new List<TestOccurrence>();

                foreach (var test in tests.TestList)
                {
                    test.BuildTypeId = build.BuildTypeID;
                    yield return test;
                }
            }
        }

        public IEnumerable<string> GetTags(TeamCityBuild build)
        {
            var url = $"httpAuth/app/rest/builds/{build.Id}/tags";
            using (var stream = MakeTeamCityRequest(HttpMethod.Get, url, timeout: TimeSpan.FromMinutes(1)))
            {
                var serializer = new XmlSerializer(typeof(Tags));
                var tags = serializer.Deserialize(stream) as Tags;

                foreach(var tag in tags.TagList)
                {
                    yield return tag.Name;
                }
            }
        }

        public void SetTag(TeamCityBuild build, string tag)
        {
            var url = $"app/rest/builds/{build.Id}/tags/";
            MakeTeamCityRequest(HttpMethod.Post, url, tag).Dispose();
        }

        public IList<TeamCityBuild> GetFailedBuilds(DateTime startDate)
        {
            return GetBuilds($"sinceDate:{TCDateTime(startDate)},status:FAILURE");
        }

        public IDictionary<string, string> GetBuildTypes()
        {
            var fields = "buildType(id,name)";

            var url = $"httpAuth/app/rest/buildTypes?fields={fields}";
            using (var stream = MakeTeamCityRequest(HttpMethod.Get, url))
            {
                var serializer = new XmlSerializer(typeof(BuildTypes));
                var buildTypes = serializer.Deserialize(stream) as BuildTypes;

                var result = new Dictionary<string, string>();
                foreach (var buildType in buildTypes.BuildTypeList)
                {
                    result.Add(buildType.Id, buildType.Name);
                }

                return result;
            }
        }

        public IList<TeamCityBuild> GetBuilds(DateTime startDate)
        {
            return GetBuilds($"sinceDate:{TCDateTime(startDate)}");
        }

        public IList<TeamCityBuild> GetBuilds(string locator)
        {
            var fields = "build(id,startDate,buildTypeId,status,branchName,webUrl)";

            var url = $"httpAuth/app/rest/builds?locator={locator},count:{_defaultCount}&fields={fields}";
            using (var stream = MakeTeamCityRequest(HttpMethod.Get, url))
            {
                var serializer = new XmlSerializer(typeof(Builds));
                var builds = serializer.Deserialize(stream) as Builds;

                return builds.BuildList;
            }
        }

        private Stream MakeTeamCityRequest(HttpMethod method, string url, string body = null, TimeSpan? timeout = null)
        {
            var requestUri = $"http://{Config.Server}/{url}";

            var authInfo = $"{Config.User}:{Config.Password}";
            var authEncoded = Convert.ToBase64String(Encoding.Default.GetBytes(authInfo));

            var request = new HttpRequestMessage(method, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authEncoded);

            if(body != null)
            {
                request.Content = new StringContent(body);
            }

            using (var client = new HttpClient())
            {
                if (timeout != null)
                {
                    client.Timeout = timeout.Value;
                }

                var response = client.SendAsync(request).Result;

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return response.Content.ReadAsStreamAsync().Result;
                }
                else
                {
                    var content = response.Content.ReadAsStringAsync().Result;
                    _reporter.Error($"Http error: {response.StatusCode}");
                    _reporter.Error($"Content: {content}");
                    throw new HttpRequestException(response.StatusCode.ToString());
                }
            }
        }

        public static string TCDateTime(DateTime date)
        {
            return date.ToString("yyyyMMddTHHmmss") + "-0800";
        }

        public string GetBuildLog(TeamCityBuild build)
        {
            var buildLogDir = Path.Combine("temp", "BuildLogs");
            var buildLogFile = Path.Combine(buildLogDir, $"{build.Id}.txt");

            Directory.CreateDirectory(buildLogDir);
            if (!File.Exists(buildLogFile))
            {
                using (var fileStream = File.Create(buildLogFile))
                using (var stream = MakeTeamCityRequest(HttpMethod.Get, $"httpAuth/downloadBuildLog.html?buildId={build.Id}"))
                {
                    stream.CopyTo(fileStream);
                }
            }

            return File.ReadAllText(buildLogFile);
        }
    }
}
