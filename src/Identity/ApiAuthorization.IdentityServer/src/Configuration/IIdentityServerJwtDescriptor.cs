﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.AspNetCore.ApiAuthorization.IdentityServer.Configuration
{
    internal interface IIdentityServerJwtDescriptor
    {
        IDictionary<string, ResourceDefinition> GetResourceDefinitions();
    }
}