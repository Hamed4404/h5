// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace Wasm.Authentication.Server.Models;

public class ApplicationUser : IdentityUser
{
    public UserPreference UserPreference { get; set; }
}
