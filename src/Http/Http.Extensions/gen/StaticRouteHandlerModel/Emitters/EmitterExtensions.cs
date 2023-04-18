// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Analyzers.Infrastructure;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Http.RequestDelegateGenerator.StaticRouteHandlerModel;

internal static class EmitterExtensions
{
    public static string ToMessageString(this EndpointParameter endpointParameter) => endpointParameter.Source switch
    {
        EndpointParameterSource.Header => "header",
        EndpointParameterSource.Query => "query string",
        EndpointParameterSource.RouteOrQuery => "route or query string",
        EndpointParameterSource.FormBody => "form",
        EndpointParameterSource.BindAsync => endpointParameter.BindMethod == BindabilityMethod.BindAsync
            ? $"{endpointParameter.Type.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)}.BindAsync(HttpContext)"
            : $"{endpointParameter.Type.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)}.BindAsync(HttpContext, ParameterInfo)",
        _ => "unknown"
    };

    public static bool IsSerializableJsonResponse(this EndpointResponse endpointResponse, [NotNullWhen(true)] out ITypeSymbol? responseTypeSymbol)
    {
        responseTypeSymbol = null;
        if (endpointResponse is { IsSerializable: true, ResponseType: { } responseType })
        {
            responseTypeSymbol = responseType;
            return true;
        }
        return false;
    }
}
