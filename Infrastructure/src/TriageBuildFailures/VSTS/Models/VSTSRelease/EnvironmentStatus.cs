﻿// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace TriageBuildFailures.VSTS.Models
{
    public enum EnvironmentStatus
    {
        Canceled,
        InProgress,
        NotStarted,
        PartiallySucceeded,
        Queued,
        Rejected,
        Scheduled,
        Succeeded,
        Undefined
    }
}
