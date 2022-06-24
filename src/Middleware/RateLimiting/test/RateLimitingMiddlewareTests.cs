// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Microsoft.AspNetCore.RateLimiting;

public class RateLimitingMiddlewareTests : LoggedTest
{
    [Fact]
    public void Ctor_ThrowsExceptionsWhenNullArgs()
    {
        var options = CreateOptionsAccessor();
        options.Value.GlobalLimiter = new TestPartitionedRateLimiter<HttpContext>();

        Assert.Throws<ArgumentNullException>(() => new RateLimitingMiddleware(
            null,
            new NullLoggerFactory().CreateLogger<RateLimitingMiddleware>(),
            options,
            Mock.Of<IServiceProvider>()));

        Assert.Throws<ArgumentNullException>(() => new RateLimitingMiddleware(c =>
        {
            return Task.CompletedTask;
        },
        null,
        options,
        Mock.Of<IServiceProvider>()));

        Assert.Throws<ArgumentNullException>(() => new RateLimitingMiddleware(c =>
        {
            return Task.CompletedTask;
        },
        new NullLoggerFactory().CreateLogger<RateLimitingMiddleware>(),
        options,
        null));
    }

    [Fact]
    public async Task RequestsCallNextIfAccepted()
    {
        var flag = false;
        var options = CreateOptionsAccessor();
        options.Value.GlobalLimiter = new TestPartitionedRateLimiter<HttpContext>(new TestRateLimiter(true));
        var middleware = new RateLimitingMiddleware(c =>
        {
            flag = true;
            return Task.CompletedTask;
        },
        new NullLoggerFactory().CreateLogger<RateLimitingMiddleware>(),
        options,
        Mock.Of<IServiceProvider>());

        await middleware.Invoke(new DefaultHttpContext());
        Assert.True(flag);
    }

    [Fact]
    public async Task RequestRejected_CallsOnRejectedAndGives503()
    {
        var onRejectedInvoked = false;
        var options = CreateOptionsAccessor();
        options.Value.GlobalLimiter = new TestPartitionedRateLimiter<HttpContext>(new TestRateLimiter(false));
        options.Value.OnRejected = (context, token) =>
        {
            onRejectedInvoked = true;
            return ValueTask.CompletedTask;
        };

        var middleware = new RateLimitingMiddleware(c =>
        {
            return Task.CompletedTask;
        },
        new NullLoggerFactory().CreateLogger<RateLimitingMiddleware>(),
        options,
        Mock.Of<IServiceProvider>());

        var context = new DefaultHttpContext();
        await middleware.Invoke(context).DefaultTimeout();
        Assert.True(onRejectedInvoked);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
    }

    [Fact]
    public async Task RequestRejected_WinsOverDefaultStatusCode()
    {
        var onRejectedInvoked = false;
        var options = CreateOptionsAccessor();
        options.Value.GlobalLimiter = new TestPartitionedRateLimiter<HttpContext>(new TestRateLimiter(false));
        options.Value.OnRejected = (context, token) =>
        {
            onRejectedInvoked = true;
            context.HttpContext.Response.StatusCode = 429;
            return ValueTask.CompletedTask;
        };

        var middleware = new RateLimitingMiddleware(c =>
        {
            return Task.CompletedTask;
        },
        new NullLoggerFactory().CreateLogger<RateLimitingMiddleware>(),
        options,
        Mock.Of<IServiceProvider>());

        var context = new DefaultHttpContext();
        await middleware.Invoke(context).DefaultTimeout();
        Assert.True(onRejectedInvoked);
        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
    }

    [Fact]
    public async Task RequestAborted_ThrowsTaskCanceledException()
    {
        var options = CreateOptionsAccessor();
        options.Value.GlobalLimiter = new TestPartitionedRateLimiter<HttpContext>(new TestRateLimiter(false));

        var middleware = new RateLimitingMiddleware(c =>
        {
            return Task.CompletedTask;
        },
        new NullLoggerFactory().CreateLogger<RateLimitingMiddleware>(),
        options,
        Mock.Of<IServiceProvider>());

        var context = new DefaultHttpContext();
        context.RequestAborted = new CancellationToken(true);
        await Assert.ThrowsAsync<TaskCanceledException>(() => middleware.Invoke(context)).DefaultTimeout();
    }

    [Fact]
    public async Task EndpointLimiter_Rejects()
    {
        var onRejectedInvoked = false;
        var options = CreateOptionsAccessor();
        var name = "myEndpoint";
        options.Value.AddPolicy<string>(name, (context =>
        {
            return RateLimitPartition.Create<string>("myLimiter", (key =>
            {
                return new TestRateLimiter(false);
            }));
        }));
        options.Value.OnRejected = (context, token) =>
        {
            onRejectedInvoked = true;
            context.HttpContext.Response.StatusCode = 429;
            return ValueTask.CompletedTask;
        };

        var middleware = new RateLimitingMiddleware(c =>
        {
            return Task.CompletedTask;
        },
        new NullLoggerFactory().CreateLogger<RateLimitingMiddleware>(),
        options,
        Mock.Of<IServiceProvider>());

        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(c => Task.CompletedTask, new EndpointMetadataCollection(new RateLimiterMetadata(name)), "Test endpoint"));
        await middleware.Invoke(context).DefaultTimeout();
        Assert.True(onRejectedInvoked);
        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
    }

    [Fact]
    public async Task EndpointLimiterConvenienceMethod_Rejects()
    {
        var onRejectedInvoked = false;
        var options = CreateOptionsAccessor();
        var name = "myEndpoint";
        options.Value.AddFixedWindowLimiter(name, new FixedWindowRateLimiterOptions(1, QueueProcessingOrder.OldestFirst, 0, TimeSpan.Zero, autoReplenishment: false));
        options.Value.OnRejected = (context, token) =>
        {
            onRejectedInvoked = true;
            context.HttpContext.Response.StatusCode = 429;
            return ValueTask.CompletedTask;
        };

        var middleware = new RateLimitingMiddleware(c =>
        {
            return Task.CompletedTask;
        },
        new NullLoggerFactory().CreateLogger<RateLimitingMiddleware>(),
        options,
        Mock.Of<IServiceProvider>());

        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(c => Task.CompletedTask, new EndpointMetadataCollection(new RateLimiterMetadata(name)), "Test endpoint"));
        await middleware.Invoke(context).DefaultTimeout();
        Assert.False(onRejectedInvoked);
        await middleware.Invoke(context).DefaultTimeout();
        Assert.True(onRejectedInvoked);
        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
    }

    [Fact]
    public async Task EndpointLimiterRejects_EndpointOnRejectedFires()
    {
        var onRejectedInvoked = false;
        var options = CreateOptionsAccessor();
        var name = "myEndpoint";
        // This is the policy that should get used
        options.Value.AddPolicy<string>(name, new TestRateLimiterPolicy("myKey", 404, false));
        // This OnRejected should be ignored in favor of the one on the policy
        options.Value.OnRejected = (context, token) =>
        {
            onRejectedInvoked = true;
            context.HttpContext.Response.StatusCode = 429;
            return ValueTask.CompletedTask;
        };

        var middleware = new RateLimitingMiddleware(c =>
        {
            return Task.CompletedTask;
        },
        new NullLoggerFactory().CreateLogger<RateLimitingMiddleware>(),
        options,
        Mock.Of<IServiceProvider>());

        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(c => Task.CompletedTask, new EndpointMetadataCollection(new RateLimiterMetadata(name)), "Test endpoint"));
        await middleware.Invoke(context).DefaultTimeout();
        Assert.False(onRejectedInvoked);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    private IOptions<RateLimiterOptions> CreateOptionsAccessor() => Options.Create(new RateLimiterOptions());

}
