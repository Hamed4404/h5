﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Common;
using TeamCityApi;

namespace TriageBuildFailures.Handlers
{
    /// <summary>
    /// If we don't know what else to do email the Build Buddy to manually decide what to do. Ideally they should then update this project to handle that case.
    /// </summary>
    public class HandleUnhandled : HandleFailureBase
    {
        public override bool CanHandleFailure(TeamCityBuild build)
        {
            return true;
        }

        public override async Task HandleFailure(TeamCityBuild build)
        {
            var subject = $"{build.BuildType.Name} failed in an unhandled way";

            var message = $"The build {build.WebURL} failed and RAAS doesn't know what to do about it. Plz hlp";

            await EmailClient.SendEmail(subject: subject, body: message, to: Static.BuildBuddyEmail);
        }
    }
}
