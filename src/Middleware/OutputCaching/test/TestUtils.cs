// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.OutputCaching.Tests;

internal class TestUtils
{
    static TestUtils()
    {
        // Force sharding in tests
        StreamUtilities.BodySegmentSize = 10;
    }

    private static bool TestRequestDelegate(HttpContext context, string guid)
    {
        var headers = context.Response.GetTypedHeaders();
        headers.Date = DateTimeOffset.UtcNow;
        headers.Headers["X-Value"] = guid;

        if (context.Request.Method != "HEAD")
        {
            return true;
        }
        return false;
    }

    internal static async Task TestRequestDelegateWriteAsync(HttpContext context)
    {
        var uniqueId = Guid.NewGuid().ToString();
        if (TestRequestDelegate(context, uniqueId))
        {
            await context.Response.WriteAsync(uniqueId);
        }
    }

    internal static async Task TestRequestDelegateSendFileAsync(HttpContext context)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestDocument.txt");
        var uniqueId = Guid.NewGuid().ToString();
        if (TestRequestDelegate(context, uniqueId))
        {
            await context.Response.SendFileAsync(path, 0, null);
            await context.Response.WriteAsync(uniqueId);
        }
    }

    internal static Task TestRequestDelegateWrite(HttpContext context)
    {
        var uniqueId = Guid.NewGuid().ToString();
        if (TestRequestDelegate(context, uniqueId))
        {
            var feature = context.Features.Get<IHttpBodyControlFeature>();
            if (feature != null)
            {
                feature.AllowSynchronousIO = true;
            }
            context.Response.Write(uniqueId);
        }
        return Task.CompletedTask;
    }

    internal static IOutputCacheKeyProvider CreateTestKeyProvider()
    {
        return CreateTestKeyProvider(new OutputCacheOptions());
    }

    internal static IOutputCacheKeyProvider CreateTestKeyProvider(OutputCacheOptions options)
    {
        return new OutputCacheKeyProvider(new DefaultObjectPoolProvider(), Options.Create(options));
    }

    internal static IEnumerable<IHostBuilder> CreateBuildersWithOutputCaching(
        Action<IApplicationBuilder> configureDelegate = null,
        OutputCacheOptions options = null,
        Action<HttpContext> contextAction = null)
    {
        return CreateBuildersWithOutputCaching(configureDelegate, options, new RequestDelegate[]
        {
            context =>
            {
                contextAction?.Invoke(context);
                return TestRequestDelegateWrite(context);
            },
            context =>
            {
                contextAction?.Invoke(context);
                return TestRequestDelegateWriteAsync(context);
            },
            context =>
            {
                contextAction?.Invoke(context);
                return TestRequestDelegateSendFileAsync(context);
            },
        });
    }

    private static IEnumerable<IHostBuilder> CreateBuildersWithOutputCaching(
        Action<IApplicationBuilder> configureDelegate = null,
        OutputCacheOptions options = null,
        IEnumerable<RequestDelegate> requestDelegates = null)
    {
        if (configureDelegate == null)
        {
            configureDelegate = app => { };
        }
        if (requestDelegates == null)
        {
            requestDelegates = new RequestDelegate[]
            {
                    TestRequestDelegateWriteAsync,
                    TestRequestDelegateWrite
            };
        }

        foreach (var requestDelegate in requestDelegates)
        {
            // Test with in memory OutputCache
            yield return new HostBuilder()
                .ConfigureWebHost(webHostBuilder =>
                {
                    webHostBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddOutputCache(outputCachingOptions =>
                        {
                            if (options != null)
                            {
                                outputCachingOptions.MaximumBodySize = options.MaximumBodySize;
                                outputCachingOptions.UseCaseSensitivePaths = options.UseCaseSensitivePaths;
                                outputCachingOptions.SystemClock = options.SystemClock;
                                outputCachingOptions.BasePolicies = options.BasePolicies;
                                outputCachingOptions.DefaultExpirationTimeSpan = options.DefaultExpirationTimeSpan;
                                outputCachingOptions.SizeLimit = options.SizeLimit;
                            }
                            else
                            {
                                outputCachingOptions.BasePolicies = new();
                                outputCachingOptions.BasePolicies.Add(new OutputCachePolicyBuilder().Build());
                            }
                        });
                    })
                    .Configure(app =>
                    {
                        configureDelegate(app);
                        app.UseOutputCache();
                        app.Run(requestDelegate);
                    });
                });
        }
    }

    internal static OutputCacheMiddleware CreateTestMiddleware(
        RequestDelegate next = null,
        IOutputCacheStore cache = null,
        OutputCacheOptions options = null,
        TestSink testSink = null,
        IOutputCacheKeyProvider keyProvider = null
        )
    {
        if (next == null)
        {
            next = httpContext => Task.CompletedTask;
        }
        if (cache == null)
        {
            cache = new TestOutputCache();
        }
        if (options == null)
        {
            options = new OutputCacheOptions();
        }
        if (keyProvider == null)
        {
            keyProvider = new OutputCacheKeyProvider(new DefaultObjectPoolProvider(), Options.Create(options));
        }

        return new OutputCacheMiddleware(
            next,
            Options.Create(options),
            testSink == null ? (ILoggerFactory)NullLoggerFactory.Instance : new TestLoggerFactory(testSink, true),
            cache,
            keyProvider);
    }

    internal static OutputCacheContext CreateTestContext(IOutputCacheStore cache = null, OutputCacheOptions options = null)
    {
        return new OutputCacheContext(new DefaultHttpContext(), cache ?? new TestOutputCache(), options ?? Options.Create(new OutputCacheOptions()).Value, NullLogger.Instance)
        {
            EnableOutputCaching = true,
            AllowCacheStorage = true,
            AllowCacheLookup = true,
            ResponseTime = DateTimeOffset.UtcNow
        };
    }

    internal static OutputCacheContext CreateTestContext(HttpContext httpContext, IOutputCacheStore cache = null, OutputCacheOptions options = null)
    {
        return new OutputCacheContext(httpContext, cache ?? new TestOutputCache(), options ?? Options.Create(new OutputCacheOptions()).Value, NullLogger.Instance)
        {
            EnableOutputCaching = true,
            AllowCacheStorage = true,
            AllowCacheLookup = true,
            ResponseTime = DateTimeOffset.UtcNow
        };
    }

    internal static OutputCacheContext CreateTestContext(ITestSink testSink, IOutputCacheStore cache = null, OutputCacheOptions options = null)
    {
        return new OutputCacheContext(new DefaultHttpContext(), cache ?? new TestOutputCache(), options ?? Options.Create(new OutputCacheOptions()).Value, new TestLogger("OutputCachingTests", testSink, true))
        {
            EnableOutputCaching = true,
            AllowCacheStorage = true,
            AllowCacheLookup = true,
            ResponseTime = DateTimeOffset.UtcNow
        };
    }

    internal static OutputCacheContext CreateUninitializedContext(IOutputCacheStore cache = null, OutputCacheOptions options = null)
    {
        return new OutputCacheContext(new DefaultHttpContext(), cache ?? new TestOutputCache(), options ?? Options.Create(new OutputCacheOptions()).Value, NullLogger.Instance)
        {
        };
    }

    internal static void AssertLoggedMessages(IEnumerable<WriteContext> messages, params LoggedMessage[] expectedMessages)
    {
        var messageList = messages.ToList();
        Assert.Equal(expectedMessages.Length, messageList.Count);

        for (var i = 0; i < messageList.Count; i++)
        {
            Assert.Equal(expectedMessages[i].EventId, messageList[i].EventId);
            Assert.Equal(expectedMessages[i].LogLevel, messageList[i].LogLevel);
        }
    }

    public static HttpRequestMessage CreateRequest(string method, string requestUri)
    {
        return new HttpRequestMessage(new HttpMethod(method), requestUri);
    }
}

internal static class HttpResponseWritingExtensions
{
    internal static void Write(this HttpResponse response, string text)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(text);

        var data = Encoding.UTF8.GetBytes(text);
        response.Body.Write(data, 0, data.Length);
    }
}

internal class LoggedMessage
{
    internal static LoggedMessage RequestMethodNotCacheable => new LoggedMessage(1, LogLevel.Debug);
    internal static LoggedMessage RequestWithAuthorizationNotCacheable => new LoggedMessage(2, LogLevel.Debug);
    internal static LoggedMessage RequestWithNoCacheNotCacheable => new LoggedMessage(3, LogLevel.Debug);
    internal static LoggedMessage RequestWithPragmaNoCacheNotCacheable => new LoggedMessage(4, LogLevel.Debug);
    internal static LoggedMessage ExpirationMinFreshAdded => new LoggedMessage(5, LogLevel.Debug);
    internal static LoggedMessage ExpirationSharedMaxAgeExceeded => new LoggedMessage(6, LogLevel.Debug);
    internal static LoggedMessage ExpirationMustRevalidate => new LoggedMessage(7, LogLevel.Debug);
    internal static LoggedMessage ExpirationMaxStaleSatisfied => new LoggedMessage(8, LogLevel.Debug);
    internal static LoggedMessage ExpirationMaxAgeExceeded => new LoggedMessage(9, LogLevel.Debug);
    internal static LoggedMessage ExpirationExpiresExceeded => new LoggedMessage(10, LogLevel.Debug);
    internal static LoggedMessage ResponseWithoutPublicNotCacheable => new LoggedMessage(11, LogLevel.Debug);
    internal static LoggedMessage ResponseWithNoStoreNotCacheable => new LoggedMessage(12, LogLevel.Debug);
    internal static LoggedMessage ResponseWithNoCacheNotCacheable => new LoggedMessage(13, LogLevel.Debug);
    internal static LoggedMessage ResponseWithSetCookieNotCacheable => new LoggedMessage(14, LogLevel.Debug);
    internal static LoggedMessage ResponseWithVaryStarNotCacheable => new LoggedMessage(15, LogLevel.Debug);
    internal static LoggedMessage ResponseWithPrivateNotCacheable => new LoggedMessage(16, LogLevel.Debug);
    internal static LoggedMessage ResponseWithUnsuccessfulStatusCodeNotCacheable => new LoggedMessage(17, LogLevel.Debug);
    internal static LoggedMessage NotModifiedIfNoneMatchStar => new LoggedMessage(18, LogLevel.Debug);
    internal static LoggedMessage NotModifiedIfNoneMatchMatched => new LoggedMessage(19, LogLevel.Debug);
    internal static LoggedMessage NotModifiedIfModifiedSinceSatisfied => new LoggedMessage(20, LogLevel.Debug);
    internal static LoggedMessage NotModifiedServed => new LoggedMessage(21, LogLevel.Information);
    internal static LoggedMessage CachedResponseServed => new LoggedMessage(22, LogLevel.Information);
    internal static LoggedMessage GatewayTimeoutServed => new LoggedMessage(23, LogLevel.Information);
    internal static LoggedMessage NoResponseServed => new LoggedMessage(24, LogLevel.Information);
    internal static LoggedMessage VaryByRulesUpdated => new LoggedMessage(25, LogLevel.Debug);
    internal static LoggedMessage ResponseCached => new LoggedMessage(26, LogLevel.Information);
    internal static LoggedMessage ResponseNotCached => new LoggedMessage(27, LogLevel.Information);
    internal static LoggedMessage ResponseContentLengthMismatchNotCached => new LoggedMessage(28, LogLevel.Warning);
    internal static LoggedMessage ExpirationInfiniteMaxStaleSatisfied => new LoggedMessage(29, LogLevel.Debug);
    internal static LoggedMessage ExpirationExpiresExceededNoExpiration => new LoggedMessage(30, LogLevel.Debug);

    private LoggedMessage(int evenId, LogLevel logLevel)
    {
        EventId = evenId;
        LogLevel = logLevel;
    }

    internal int EventId { get; }
    internal LogLevel LogLevel { get; }
}

internal class TestResponseCachingKeyProvider : IOutputCacheKeyProvider
{
    private readonly string _key;

    public TestResponseCachingKeyProvider(string key = null)
    {
        _key = key;
    }

    public string CreateStorageKey(OutputCacheContext context)
    {
        return _key;
    }
}

internal class TestOutputCache : IOutputCacheStore
{
    private readonly IDictionary<string, byte[]> _storage = new Dictionary<string, byte[]>();
    public int GetCount { get; private set; }
    public int SetCount { get; private set; }

    public ValueTask EvictByTagAsync(string tag, CancellationToken token)
    {
        throw new NotImplementedException();
    }

    public ValueTask<byte[]> GetAsync(string key, CancellationToken token)
    {
        GetCount++;
        try
        {
            return ValueTask.FromResult(_storage[key]);
        }
        catch
        {
            return ValueTask.FromResult(default(byte[]));
        }
    }

    public ValueTask SetAsync(string key, byte[] entry, string[] tags, TimeSpan validFor, CancellationToken token)
    {
        SetCount++;
        _storage[key] = entry;

        return ValueTask.CompletedTask;
    }
}

internal class TestClock : ISystemClock
{
    public DateTimeOffset UtcNow { get; set; }
}

internal class AllowTestPolicy : IOutputCachePolicy
{
    public ValueTask CacheRequestAsync(OutputCacheContext context)
    {
        context.AllowCacheLookup = true;
        context.AllowCacheStorage = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(OutputCacheContext context)
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask ServeResponseAsync(OutputCacheContext context)
    {
        return ValueTask.CompletedTask;
    }
}
