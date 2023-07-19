// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Microsoft.AspNetCore.Shared;

internal static class HttpContextDebugFormatter
{
    public static string ResponseToString(HttpResponse response, string? reasonPhrase)
    {
        var text = response.StatusCode.ToString(CultureInfo.InvariantCulture);
        var resolvedReasonPhrase = ResolveReasonPhrase(response, reasonPhrase);
        if (!string.IsNullOrEmpty(resolvedReasonPhrase))
        {
            text += $" {resolvedReasonPhrase}";
        }
        if (!string.IsNullOrEmpty(response.ContentType))
        {
            text += $" {response.ContentType}";
        }
        return text;
    }

    private static string? ResolveReasonPhrase(HttpResponse response, string? reasonPhrase)
    {
        return response.HttpContext.Features.Get<IHttpResponseFeature>()?.ReasonPhrase ?? reasonPhrase;
    }

    public static string RequestToString(HttpRequest request)
    {
        var text = GetRequestUrl(request, includeQueryString: true);
        if (!string.IsNullOrEmpty(request.Method))
        {
            text = $"{request.Method} {text}";
        }
        if (!string.IsNullOrEmpty(request.Protocol))
        {
            text += $" {request.Protocol}";
        }
        if (!string.IsNullOrEmpty(request.ContentType))
        {
            text += $" {request.ContentType}";
        }
        return text;
    }

    public static string ContextToString(HttpContext context, string? reasonPhrase)
    {
        var text = $"{context.Request.Method} {GetRequestUrl(context.Request, includeQueryString: false)} {context.Response.StatusCode}";
        var resolvedReasonPhrase = ResolveReasonPhrase(context.Response, reasonPhrase);
        if (!string.IsNullOrEmpty(resolvedReasonPhrase))
        {
            text += $" {resolvedReasonPhrase}";
        }

        return text;
    }

    private static string GetRequestUrl(HttpRequest request, bool includeQueryString)
    {
        // The URL might be missing because the context was manually created in a test, e.g. new DefaultHttpContext()
        // If there is no request URL then provide a more understandable empty value.
        if (string.IsNullOrEmpty(request.Scheme) || !request.Host.HasValue)
        {
            return "(unset)";
        }

        return $"{request.Scheme}://{request.Host.Value}{request.PathBase.Value}{request.Path.Value}{(includeQueryString ? request.QueryString.Value : string.Empty)}";
    }
}
