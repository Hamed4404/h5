// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNetCore.OutputCaching;

/// <summary>
/// A policy which caches un-authenticated, GET and HEAD, 200 responses.
/// </summary>
internal sealed class DefaultOutputCachePolicy : IOutputCachePolicy
{
    public static readonly DefaultOutputCachePolicy Instance = new();

    private DefaultOutputCachePolicy()
    {
    }

    /// <inheritdoc />
    Task IOutputCachePolicy.CacheRequestAsync(OutputCacheContext context)
    {
        var attemptOutputCaching = AttemptOutputCaching(context);
        context.EnableOutputCaching = true;
        context.AllowCacheLookup = attemptOutputCaching;
        context.AllowCacheStorage = attemptOutputCaching;
        context.AllowLocking = true;

        // Vary by any query by default
        context.CachedVaryByRules.QueryKeys = "*";

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    Task IOutputCachePolicy.ServeFromCacheAsync(OutputCacheContext context)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    Task IOutputCachePolicy.ServeResponseAsync(OutputCacheContext context)
    {
        var response = context.HttpContext.Response;

        // Verify existence of cookie headers
        if (!StringValues.IsNullOrEmpty(response.Headers.SetCookie))
        {
            context.Logger.ResponseWithSetCookieNotCacheable();
            context.AllowCacheStorage = false;
            return Task.CompletedTask;
        }

        // Check response code
        if (response.StatusCode != StatusCodes.Status200OK)
        {
            context.Logger.ResponseWithUnsuccessfulStatusCodeNotCacheable(response.StatusCode);
            context.AllowCacheStorage = false;
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    private static bool AttemptOutputCaching(OutputCacheContext context)
    {
        // Check if the current request fulfisls the requirements to be cached

        var request = context.HttpContext.Request;

        // Verify the method
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
        {
            context.Logger.RequestMethodNotCacheable(request.Method);
            return false;
        }

        // Verify existence of authorization headers
        if (!StringValues.IsNullOrEmpty(request.Headers.Authorization) || request.HttpContext.User?.Identity?.IsAuthenticated == true)
        {
            context.Logger.RequestWithAuthorizationNotCacheable();
            return false;
        }

        return true;
    }
}
