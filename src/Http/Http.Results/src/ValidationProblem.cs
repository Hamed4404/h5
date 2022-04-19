// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Http.HttpResults;

/// <summary>
/// An <see cref="IResult"/> that on execution will write Problem Details
/// HTTP API responses based on https://tools.ietf.org/html/rfc7807
/// </summary>
public sealed class ValidationProblem : IResult, IEndpointMetadataProvider
{
    internal ValidationProblem(HttpValidationProblemDetails problemDetails)
    {
        ArgumentNullException.ThrowIfNull(problemDetails, nameof(problemDetails));
        if (problemDetails is { Status: not null and not StatusCodes.Status400BadRequest })
        {
            throw new ArgumentException($"{nameof(ValidationProblem)} only supports a 400 Bad Request response status code.", nameof(problemDetails));
        }

        ProblemDetails = problemDetails;
        HttpResultsHelper.ApplyProblemDetailsDefaults(ProblemDetails, statusCode: StatusCodes.Status400BadRequest);
    }

    /// <summary>
    /// Gets the <see cref="HttpValidationProblemDetails"/> instance.
    /// </summary>
    public HttpValidationProblemDetails ProblemDetails { get; }

    /// <summary>
    /// Gets the value for the <c>Content-Type</c> header: <c>application/problem+json</c>.
    /// </summary>
    public string ContentType => "application/problem+json";

    /// <summary>
    /// Gets the HTTP status code: <see cref="StatusCodes.Status400BadRequest"/>
    /// </summary>
    public int StatusCode => StatusCodes.Status400BadRequest;

    /// <inheritdoc/>
    public Task ExecuteAsync(HttpContext httpContext)
    {
        var loggerFactory = httpContext.RequestServices.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(typeof(ValidationProblem));

        HttpResultsHelper.Log.WritingResultAsStatusCode(logger, StatusCode);
        httpContext.Response.StatusCode = StatusCode;

        return HttpResultsHelper.WriteResultAsJsonAsync(
                httpContext,
                logger,
                value: ProblemDetails,
                ContentType);
    }

    /// <inheritdoc/>
    static void IEndpointMetadataProvider.PopulateMetadata(EndpointMetadataContext context)
    {
        context.EndpointMetadata.Add(new ProducesResponseTypeMetadata(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json"));
    }
}
