﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Octokit;
using TriageBuildFailures.GitHub;
using TriageBuildFailures.TeamCity;

namespace TriageBuildFailures.Handlers
{
    /// <summary>
    /// When tests fail file an issue about it. If an issue has already been filed comment on the issue so people know it's still happening.
    /// </summary>
    public class HandleTestFailures : HandleFailureBase
    {
        private const string NoStackTraceAvailable = "No stacktrace available";

        public override bool CanHandleFailure(TeamCityBuild build)
        {
            var tests = TCClient.GetTests(build);
            return tests.Any(s => s.Status == BuildStatus.FAILURE);
        }

        private string SafeGetExceptionMessage(string errors)
        {
            return string.IsNullOrEmpty(errors) ? NoStackTraceAvailable : ErrorParsing.GetExceptionMessage(errors);
        }

        private string TrimTestFailureText(string text)
        {
            var result = text;

            if(result.Length > 6000)
            {
                result = text.Substring(0, 6000);
                result += $"{Environment.NewLine}...";
            }

            return result;
        }

        private const string WorkFlowComment = @"Please use this workflow to address this flaky test issue, including checking applicable checkboxes and filling in the applicable ""TODO"" entries:

* Is this actually a flaky test?
  * No, this is a regular test failure, fix the test/product (TODO: Link to commit/PR)
  * Yes, proceed below...

* Is this test failure caused by product code flakiness? (Either this product, or another product this test depends on.)
  * [ ] File a bug against the product (TODO: Link to other bug)
  * Is it possible to change the test to avoid the flakiness?
    * Yes? Go to the ""Change the test!"" section.
    * No?
      * [ ] Disable the test (TODO: Link to PR/commit)
      * [ ] Wait for other bug to be resolved
      * [ ] Wait for us to get build that has the fix
      * [ ] Re-enable our test (TODO: Link to PR/commit)
      * [ ] Close this bug

* Is it that the test itself is flaky? This includes external transient problems (e.g. remote server problems, file system race condition, etc.)
  * Is there is a way to change our test to avoid this flakiness?
    * Yes? Change the test!
      * [ ] Change the test to avoid the flakiness, for example by using a different test strategy, or by adding retries w/ timeouts (TODO: Link to PR/commit)
      * [ ] Run the test 100 times locally as a sanity check.
      * [ ] Close this bug
    * No?
      * Is there any logging or extra information that we could add to make this more diagnosable when it happens again?
        * Yes?
            * [ ] Add the logging (TODO: Link to PR/commit)
        * No?
            * [ ] Delete the test because flaky tests are not useful (TODO: Link to PR/commit)";

        public override async Task HandleFailure(TeamCityBuild build)
        {
            var tests = TCClient.GetTests(build);
            var failures = tests.Where(s => s.Status == BuildStatus.FAILURE);

            foreach (var failure in failures)
            {
                var repo = TestToRepoMapper.FindRepo(failure.Name, Reporter);
                var owner = TestToRepoMapper.FindOwner(failure.Name, Reporter);

                var issuesTask = GHClient.GetFlakyIssues(owner, repo);
                
                var errors = TCClient.GetTestFailureText(failure);

                var applicableIssues = GetApplicableIssues(await issuesTask, failure);

                var shortTestName = GetTestName(failure);
                if (applicableIssues.Count() == 0)
                {
                    var subject = $"Test failure: {shortTestName}";
                    // TODO: CC area experts
                    var body = $@"This test [fails]({build.WebURL}) occasionally with the following error:
```
{TrimTestFailureText(errors)}
```
Other tests within that build may have failed with a similar message, but they are not listed here. Check the link above for more info.

CC @{ GetManager(repo) }";
                    //TODO: We'd like to link the test history here but TC api doens't make it easy
                    var tags = new List<string> { "test-failure" };

                    var issue = await GHClient.CreateIssue(owner, repo, subject, body, tags);
                    await GHClient.AddIssueToProject(issue, GHClient.Config.FlakyProjectColumn);
                    await GHClient.CreateComment(issue, WorkFlowComment);
                }
                // The issue already exists, comment on it if we haven't already done so for this build.
                else
                {
                    await CommentOnIssue(build, applicableIssues.First(), shortTestName);
                }
            }
        }

        private string GetManager(string repoName)
        {
            var repo = Config.GitHub.Repos.FirstOrDefault(r => r.Name.Equals(repoName, StringComparison.OrdinalIgnoreCase));

            if(repo == null || string.IsNullOrEmpty(repo.Manager))
            {
                // Default to Eilon
                return "Eilon (because the bot doesn't know who else to pick)";
            }
            else
            {
                return repo.Manager;
            }
        }

        private static string GetTestName(TestOccurrence testOccurrence)
        {
            var shortTestName = testOccurrence.Name.Replace(Constants.VSTestPrefix, string.Empty);
            shortTestName = shortTestName.Split('(').First();
            return shortTestName.Split('.').Last();
        }

        private static int LevenshteinDistance(string source, string target)
        {
            // Use the Levenshtein distance for "fuzzy matching"
            var sourceLen = source == null ? 0 : source.Length;
            var targetLen = target == null ? 0 : target.Length;

            if(sourceLen == 0)
            {
                return 0;
            }

            if(targetLen == 0)
            {
                return 0;
            }

            var matrix = new int[sourceLen+1, targetLen+1];
            for (int i = 0; i <= sourceLen; i++) matrix[i, 0] = i;
            for (int j = 0; j <= targetLen; j++) matrix[0, j] = j;

            for(int i = 1; i <= sourceLen; i++)
            {
                var sourceChar = source[i-1];
                
                for(int j = 1;j <= targetLen; j++)
                {
                    var targetChar = target[j-1];
                    var cost = 0;
                    if(sourceChar != targetChar)
                    {
                        cost = 1;
                    }

                    matrix[i, j] = new int[] { matrix[i - 1, j] + 1, matrix[i, j - 1] + 1, matrix[i - 1, j - 1] + cost }.Min();
                }
            }

            return matrix[sourceLen, targetLen];
        }

        private static bool LevenshteinClose(string source, string target)
        {
            if (source == null && target == null)
            {
                return true;
            }
            else if (source == null || target == null)
            {
                return false;
            }

            var dist = LevenshteinDistance(source, target);

            var percentSame = (source.Length - dist) / (double)source.Length;

            // After a little testing and fiddling it seems that ~70% similarity of exception messages is a good heuristic for if things are "the same problem".
            // We expect this to cause the occasional false positive/negative, but let's see what they are before doing something more complicated here.
            return percentSame >= 0.7;
        }

        private static string GetExceptionFromIssue(Issue issue)
        {
            // We put exceptions inside of triple ticks on GitHub, split by that then figure out what was inside it.
            var parts = issue.Body.Split(new string[] { "```" }, StringSplitOptions.RemoveEmptyEntries);
            if (parts == null || parts.Length < 2)
            {
                return null;
            }
            else
            {
                var insideTicks = issue.Body.StartsWith("```", StringComparison.OrdinalIgnoreCase) ? parts[0] : parts[1];
                insideTicks = insideTicks.Trim();
                return ErrorParsing.GetExceptionMessage(insideTicks);
            }
        }

        private IEnumerable<GithubIssue> GetApplicableIssues(IEnumerable<GithubIssue> issues, TestOccurrence failure)
        {
            var testError = TCClient.GetTestFailureText(failure);
            var testException = SafeGetExceptionMessage(testError); ;
            var shortTestName = GetTestName(failure);

            foreach (var issue in issues)
            {
                var issueException = GetExceptionFromIssue(issue);

                // An issue is "applicable" if any of these are true:
                // 1. The issue has the test name in the subject.
                // 2. The issue exception message is the same as or close to the test exception message.
                if (issue.Title.Contains(shortTestName, StringComparison.OrdinalIgnoreCase)
                    || (issueException != null && issueException.Equals(testException))
                    || LevenshteinClose(issueException, testException))
                {
                    yield return issue;
                }
            }
        }
    }
}
