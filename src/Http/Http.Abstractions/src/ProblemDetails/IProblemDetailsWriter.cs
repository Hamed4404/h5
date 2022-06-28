// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// Defines a type that write a <see cref="Mvc.ProblemDetails"/>
/// payload to the current <see cref="HttpContext.Response"/>.
/// </summary>
public interface IProblemDetailsWriter
{
    /// <summary>
    /// Write a <see cref="Mvc.ProblemDetails"/> response to the current context
    /// </summary>
    /// <param name="context">The <see cref="ProblemDetailsContext"/> associated with the current request/response.</param>
    /// <returns>Flag that indicates if the response was started.</returns>
    ValueTask<bool> WriteAsync(ProblemDetailsContext context);
}
