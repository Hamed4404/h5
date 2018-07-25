﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


namespace TriageBuildFailures.GitHub
{
    public class GitHubConfig
    {
        public string AccessToken { get; set; }
        public int FlakyProjectColumn { get; set; }
        public string BuildBuddyUsername { get; set; }
    }
}
