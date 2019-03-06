// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Testing.xunit
{
    /// <summary>
    /// Skip test if running on helix (or a particular helix queue).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class SkipOnHelixAttribute : Attribute, ITestCondition
    {
        public bool IsMet
        {
            get
            {
                // Skip 
                var skip = OnHelix() && (Queues == null || Queues.Contains(GetTargetHelixQueue(), StringComparison.OrdinalIgnoreCase));
                return !skip;
            }
        }

        // Queues that should be skipped on, i.e. "Windows.10.Amd64.ClientRS4.VS2017.Open;OSX.1012.Amd64.Open"
        public string Queues { get; set; }

        public string SkipReason
        {
            get
            {
                return $"This test is skipped on helix";
            }
        }

        public static bool OnHelix() => !string.IsNullOrEmpty(GetTargetHelixQueue());
        
        public static string GetTargetHelixQueue() => Environment.GetEnvironmentVariable("helix");
    }
}
