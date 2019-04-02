﻿// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace TriageBuildFailures.VSTS.Models
{
    public class DeploymentJobItem
    {
        public DeploymentJob Job { get; set; }
        public IEnumerable<DeploymentTask> Tasks { get; set; }
    }
}
