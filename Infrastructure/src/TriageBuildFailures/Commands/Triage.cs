﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using McMaster.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TriageBuildFailures.Abstractions;
using TriageBuildFailures.Email;
using TriageBuildFailures.GitHub;
using TriageBuildFailures.Handlers;
using TriageBuildFailures.TeamCity;
using TriageBuildFailures.VSTS;
using TriageBuildFailures.VSTS.Models;

namespace TriageBuildFailures.Commands
{
    public class Triage
    {
        public static readonly string TriagedTag = "Triaged";

        private readonly IDictionary<Type, ICIClient> _ciClients = new Dictionary<Type, ICIClient>();
        private readonly GitHubClientWrapper _ghClient;
        private readonly EmailClient _emailClient;
        private readonly IReporter _reporter;
        private readonly Config _config;

        public Triage(Config config, IReporter reporter)
        {
            _config = config;
            _reporter = reporter;

            var vstsClient = GetVSTSClient(_config);
            _ghClient = GetGitHubClient(_config);
            _ciClients[typeof(PullRequestClient)] = GetPullRequestClient(_ghClient, vstsClient);
            _ciClients[typeof(TeamCityClientWrapper)] = GetTeamCityClient(_config);
            _ciClients[typeof(VSTSClient)] = vstsClient;
            _emailClient = GetEmailClient(_config);
        }

        private DateTime CutoffDate { get; } = DateTime.Now.AddHours(-24);

        private static readonly IEnumerable<HandleFailureBase> Handlers = new List<HandleFailureBase>
        {
            new HandleLowValueBuilds(),
            new HandleUniverseMovedOn(),
            new HandleSnapshotDependency(),
            new HandleTestFailures(),
            new HandleBuildTimeFailures(),
            new HandleUnhandled(),
        };

        private static readonly IEnumerable<string> _watchedBranches = new List<string> {
            "master",
            "release/",
            "internal/release",
            "2.2",
            "2.1",
            "2.0",
            "PR"
        };

        /// <summary>
        /// Handle each CI failure in the most appropriate way.
        /// </summary>
        /// <returns>A task indicating completion.</returns>
        public async Task TriageFailures()
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                var untriagedBuildFailures = (await GetUntriagedBuildFailures()).ToList();
                _reporter.Output($"We found {untriagedBuildFailures.Count} failed builds since {CutoffDate}. Let's triage!");

                _reporter.LogTeamCityStatistic("RAAS:UntriagedBuildFailures", untriagedBuildFailures.Count);

                foreach (var build in untriagedBuildFailures)
                {
                    _reporter.Output($"Triaging {build.WebURL} ...");
                    await HandleFailure(build);
                }
                stopWatch.Stop();

                _reporter.Output($"Done! Finished in {stopWatch.Elapsed.TotalMinutes} minutes. Let's get some coffee!");
            }
            finally
            {
                _reporter.LogTeamCityStatistic("RAAS:RetriesUsed", RetryHelpers.GetTotalRetriesUsed());
            }
        }

        /// <summary>
        /// Take the appropriate action for a CI failure.
        /// </summary>
        /// <param name="build">The CI failure which we should handle.</param>
        /// <returns></returns>
        private async Task HandleFailure(ICIBuild build)
        {
            foreach (var handler in Handlers)
            {
                handler.CIClients = _ciClients;
                handler.GHClient = _ghClient;
                handler.EmailClient = _emailClient;
                handler.Reporter = _reporter;
                handler.Config = _config;

                if (await handler.CanHandleFailure(build))
                {
                    _reporter.Output($"{handler.GetType().Name} will handle {build.WebURL}");
                    await handler.HandleFailure(build);
                    await MarkTriaged(build);
                    return;
                }
            }
        }

        /// <summary>
        /// Gets the list of CI failures which have not been previously triaged since the CutoffDate
        /// </summary>
        /// <returns>The list of CI failures which have not been previously triaged.</returns>
        private async Task<IEnumerable<ICIBuild>> GetUntriagedBuildFailures()
        {
            var result = new List<ICIBuild>();
            foreach (var ciClientKvp in _ciClients)
            {
                var ciClient = ciClientKvp.Value;
                var failedBuilds = await ciClient.GetFailedBuildsAsync(CutoffDate);

                foreach (var failedBuild in failedBuilds)
                {
                    if (IsWatchedBuild(failedBuild))
                    {
                        var tags = await ciClient.GetTagsAsync(failedBuild);
                        if (!tags.Contains(TriagedTag))
                        {
                            result.Add(failedBuild);
                        }
                    }
                    else
                    {
                        _reporter.Output($"We won't triage {failedBuild.WebURL} because it's on the wrong branch.");
                    }
                }
            }

            return result;
        }

        private bool IsWatchedBuild(ICIBuild build)
        {
            if (_watchedBranches.Any(b => build.Branch.StartsWith(b, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private async Task MarkTriaged(ICIBuild build)
        {
            await _ciClients[build.CIType].SetTagAsync(build, TriagedTag);
        }

        private GitHubClientWrapper GetGitHubClient(Config config)
        {
            return new GitHubClientWrapper(config.GitHub, _reporter);
        }

        private PullRequestClient GetPullRequestClient(GitHubClientWrapper gitHubClient, VSTSClient vstsClient)
        {
            return new PullRequestClient(gitHubClient, vstsClient, _reporter);
        }

        private EmailClient GetEmailClient(Config config)
        {
            return new EmailClient(config.Email, _reporter);
        }

        private TeamCityClientWrapper GetTeamCityClient(Config config)
        {
            return new TeamCityClientWrapper(config.TeamCity, _reporter);
        }

        private VSTSClient GetVSTSClient(Config config)
        {
            return new VSTSClient(config.VSTS, _reporter);
        }
    }
}
