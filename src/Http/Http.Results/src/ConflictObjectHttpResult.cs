// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// An <see cref="IResult"/> that on execution will write an object to the response
/// with Conflict (409) status code.
/// </summary>
public sealed class ConflictObjectHttpResult : IResult, IObjectHttpResult, IStatusCodeHttpResult
{
    internal ConflictObjectHttpResult(object? error)
    {
        Value = error;
    }

    /// <inheritdoc/>
    public object? Value { get; internal init; }

    /// <inheritdoc/>
    public int? StatusCode => StatusCodes.Status409Conflict;

    /// <inheritdoc/>
    public Task ExecuteAsync(HttpContext httpContext)
        => HttpResultsWriter.WriteResultAsJson(httpContext, objectHttpResult: this);
}
