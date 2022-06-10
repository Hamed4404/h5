// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Configuration;

namespace Microsoft.AspNetCore.Authentication;

/// <summary>
/// Extension methods for <see cref="IAuthenticationConfigurationProvider"/>
/// </summary>
public static class AuthenticationConfigurationProviderExtensions
{
    private const string AuthenticationSchemesKey = "Authentication:Schemes";

    /// <summary>
    /// Returns the specified <see cref="IConfiguration"/> object.
    /// </summary>
    /// <param name="provider">An <see cref="IAuthenticationConfigurationProvider"/> instance.</param>
    /// <param name="authenticationScheme">The path to the section to be returned.</param>
    /// <returns>The specified <see cref="IConfiguration"/> object, or null if the requested section does not exist.</returns>
    public static IConfiguration GetSchemeConfiguration(this IAuthenticationConfigurationProvider provider, string authenticationScheme)
    {
        return provider.AuthenticationConfiguration.GetSection($"{AuthenticationSchemesKey}:{authenticationScheme}");
    }
}
