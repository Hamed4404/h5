// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Http.Tests;

public class DefaultProblemDetailsWriterTest
{
    [Fact]
    public async Task WriteAsync_Works()
    {
        // Arrange
        var writer = GetWriter();
        var stream = new MemoryStream();
        var context = CreateContext(stream);
        var expectedProblem = new ProblemDetails()
        {
            Detail = "Custom Bad Request",
            Instance = "Custom Bad Request",
            Status = StatusCodes.Status400BadRequest,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1-custom",
            Title = "Custom Bad Request",
        };
        var problemDetailsContext = new ProblemDetailsContext(context)
        {
            ProblemDetails = expectedProblem
        };

        //Act
        await writer.WriteAsync(problemDetailsContext);

        //Assert
        stream.Position = 0;
        var problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(stream);
        Assert.NotNull(problemDetails);
        Assert.Equal(expectedProblem.Status, problemDetails.Status);
        Assert.Equal(expectedProblem.Type, problemDetails.Type);
        Assert.Equal(expectedProblem.Title, problemDetails.Title);
        Assert.Equal(expectedProblem.Detail, problemDetails.Detail);
        Assert.Equal(expectedProblem.Instance, problemDetails.Instance);
    }

    [Fact]
    public async Task WriteAsync_AddExtensions()
    {
        // Arrange
        var writer = GetWriter();
        var stream = new MemoryStream();
        var context = CreateContext(stream);
        var expectedProblem = new ProblemDetails();
        expectedProblem.Extensions["Extension1"] = "Extension1-Value";
        expectedProblem.Extensions["Extension2"] = "Extension2-Value";

        var problemDetailsContext = new ProblemDetailsContext(context)
        {
            ProblemDetails = expectedProblem
        };

        //Act
        await writer.WriteAsync(problemDetailsContext);

        //Assert
        stream.Position = 0;
        var problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(stream);
        Assert.NotNull(problemDetails);
        Assert.Collection(problemDetails.Extensions,
            (extension) =>
            {
                Assert.Equal("Extension1", extension.Key);
                Assert.Equal("Extension1-Value", extension.Value.ToString());
            },
            (extension) =>
            {
                Assert.Equal("Extension2", extension.Key);
                Assert.Equal("Extension2-Value", extension.Value.ToString());
            });
    }

    [Fact]
    public async Task WriteAsync_Applies_Defaults()
    {
        // Arrange
        var writer = GetWriter();
        var stream = new MemoryStream();
        var context = CreateContext(stream, StatusCodes.Status500InternalServerError);

        //Act
        await writer.WriteAsync(new ProblemDetailsContext(context));

        //Assert
        stream.Position = 0;
        var problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(stream);
        Assert.NotNull(problemDetails);
        Assert.Equal(StatusCodes.Status500InternalServerError, problemDetails.Status);
        Assert.Equal("https://tools.ietf.org/html/rfc7231#section-6.6.1", problemDetails.Type);
        Assert.Equal("An error occurred while processing your request.", problemDetails.Title);
    }

    [Fact]
    public async Task WriteAsync_Applies_CustomConfiguration()
    {
        // Arrange
        var options = new ProblemDetailsOptions()
        {
            ConfigureDetails = (context, problem) =>
            {
                problem.Status = StatusCodes.Status406NotAcceptable;
                problem.Title = "Custom Title";
                problem.Extensions["new-extension"] = new { TraceId = Guid.NewGuid() };
            }
        };
        var writer = GetWriter(options);
        var stream = new MemoryStream();
        var context = CreateContext(stream, StatusCodes.Status500InternalServerError);

        //Act
        await writer.WriteAsync(new ProblemDetailsContext(context)
        {
            ProblemDetails = new() { Status = StatusCodes.Status400BadRequest }
        });

        //Assert
        stream.Position = 0;
        var problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(stream);
        Assert.NotNull(problemDetails);
        Assert.Equal(StatusCodes.Status406NotAcceptable, problemDetails.Status);
        Assert.Equal("https://tools.ietf.org/html/rfc7231#section-6.5.1", problemDetails.Type);
        Assert.Equal("Custom Title", problemDetails.Title);
        Assert.Contains("new-extension", problemDetails.Extensions);
    }

    [Fact]
    public async Task WriteAsync_UsesStatusCode_FromProblemDetails_WhenSpecified()
    {
        // Arrange
        var writer = GetWriter();
        var stream = new MemoryStream();
        var context = CreateContext(stream, StatusCodes.Status500InternalServerError);

        //Act
        await writer.WriteAsync(new ProblemDetailsContext(context)
        {
            ProblemDetails = new() { Status = StatusCodes.Status400BadRequest }
        });

        //Assert
        stream.Position = 0;
        var problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(stream);
        Assert.NotNull(problemDetails);
        Assert.Equal(StatusCodes.Status400BadRequest, problemDetails.Status);
        Assert.Equal("https://tools.ietf.org/html/rfc7231#section-6.5.1", problemDetails.Type);
        Assert.Equal("Bad Request", problemDetails.Title);
    }

    private HttpContext CreateContext(Stream body, int statusCode = StatusCodes.Status400BadRequest)
    {
        return new DefaultHttpContext()
        {
            Response = { Body = body, StatusCode = statusCode },
            RequestServices = CreateServices(),
        };
    }

    private static IServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddTransient(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);

        return services.BuildServiceProvider();
    }

    private static DefaultProblemDetailsWriter GetWriter(ProblemDetailsOptions options = null)
    {
        options ??= new ProblemDetailsOptions();
        return new DefaultProblemDetailsWriter(Options.Create(options));
    }
}
