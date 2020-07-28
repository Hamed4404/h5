// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure;
using Microsoft.AspNetCore.Server.Kestrel.InMemory.FunctionalTests.TestTransport;
using Microsoft.AspNetCore.Server.Kestrel.Tests;
using Microsoft.AspNetCore.Testing;
using Xunit;

namespace Microsoft.AspNetCore.Server.Kestrel.InMemory.FunctionalTests
{
    public class ConnectionLimitTests : LoggedTest
    {
        [Fact]
        public async Task ResetsCountWhenConnectionClosed()
        {
            var requestTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releasedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var lockedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var counter = new EventRaisingResourceCounter(ResourceCounter.Quota(1));
            counter.OnLock += (s, e) => lockedTcs.TrySetResult(e);
            counter.OnRelease += (s, e) => releasedTcs.TrySetResult();

            await using (var server = CreateServerWithMaxConnections(async context =>
            {
                await context.Response.WriteAsync("Hello");
                await requestTcs.Task;
            }, counter))
            {
                using (var connection = server.CreateConnection())
                {
                    await connection.SendEmptyGetAsKeepAlive(); ;
                    await connection.Receive("HTTP/1.1 200 OK");
                    Assert.True(await lockedTcs.Task.DefaultTimeout());
                    requestTcs.TrySetResult();
                }

                await server.StopAsync();
            }

            await releasedTcs.Task.DefaultTimeout();
        }

        [Fact]
        public async Task UpgradedConnectionsCountsAgainstDifferentLimit()
        {
            await using (var server = CreateServerWithMaxConnections(async context =>
            {
                var feature = context.Features.Get<IHttpUpgradeFeature>();
                if (feature.IsUpgradableRequest)
                {
                    var stream = await feature.UpgradeAsync();
                    // keep it running until aborted
                    while (!context.RequestAborted.IsCancellationRequested)
                    {
                        await Task.Delay(100);
                    }
                }
            }, max: 1))
            {
                using (var disposables = new DisposableStack<InMemoryConnection>())
                {
                    var upgraded = server.CreateConnection();
                    disposables.Push(upgraded);

                    await upgraded.SendEmptyGetWithUpgrade();
                    await upgraded.Receive("HTTP/1.1 101");
                    // once upgraded, normal connection limit is decreased to allow room for more "normal" connections

                    var connection = server.CreateConnection();
                    disposables.Push(connection);

                    await connection.SendEmptyGetAsKeepAlive();
                    await connection.Receive("HTTP/1.1 200 OK");

                    using (var rejected = server.CreateConnection())
                    {
                        try
                        {
                            // this may throw IOException, depending on how fast Kestrel closes the socket
                            await rejected.SendEmptyGetAsKeepAlive();
                        }
                        catch { }

                        // connection should close without sending any data
                        await rejected.WaitForConnectionClose();
                    }
                }

                await server.StopAsync();
            }
        }

        [Fact]
        public async Task RejectsConnectionsWhenLimitReached()
        {
            const int max = 10;
            var requestTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            await using (var server = CreateServerWithMaxConnections(async context =>
            {
                await context.Response.WriteAsync("Hello");
                await requestTcs.Task;
            }, max))
            {
                using (var disposables = new DisposableStack<InMemoryConnection>())
                {
                    for (var i = 0; i < max; i++)
                    {
                        var connection = server.CreateConnection();
                        disposables.Push(connection);

                        await connection.SendEmptyGetAsKeepAlive();
                        await connection.Receive("HTTP/1.1 200 OK");
                    }

                    // limit has been reached
                    for (var i = 0; i < 10; i++)
                    {
                        using (var connection = server.CreateConnection())
                        {
                            try
                            {
                                // this may throw IOException, depending on how fast Kestrel closes the socket
                                await connection.SendEmptyGetAsKeepAlive();
                            }
                            catch { }

                            // connection should close without sending any data
                            await connection.WaitForConnectionClose();
                        }
                    }

                    requestTcs.TrySetResult();
                }

                await server.StopAsync();
            }
        }

        [Fact]
        public async Task ConnectionCountingReturnsToZero()
        {
            const int count = 100;
            var opened = 0;
            var closed = 0;
            var openedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var closedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var counter = new EventRaisingResourceCounter(ResourceCounter.Quota(uint.MaxValue));

            counter.OnLock += (o, e) =>
            {
                if (e && Interlocked.Increment(ref opened) >= count)
                {
                    openedTcs.TrySetResult();
                }
            };

            counter.OnRelease += (o, e) =>
            {
                if (Interlocked.Increment(ref closed) >= count)
                {
                    closedTcs.TrySetResult();
                }
            };

            await using (var server = CreateServerWithMaxConnections(_ => Task.CompletedTask, counter))
            {
                // open a bunch of connections in parallel
                Parallel.For(0, count, async i =>
                {
                    try
                    {
                        using (var connection = server.CreateConnection())
                        {
                            await connection.SendEmptyGetAsKeepAlive();
                            await connection.Receive("HTTP/1.1 200");
                        }
                    }
                    catch (Exception ex)
                    {
                        openedTcs.TrySetException(ex);
                    }
                });

                // wait until resource counter has called lock for each connection
                await openedTcs.Task.TimeoutAfter(TimeSpan.FromSeconds(120));
                // wait until resource counter has released all normal connections
                await closedTcs.Task.TimeoutAfter(TimeSpan.FromSeconds(120));
                Assert.Equal(count, opened);
                Assert.Equal(count, closed);

                await server.StopAsync();
            }
        }

        private TestServer CreateServerWithMaxConnections(RequestDelegate app, long max)
        {
            var serviceContext = new TestServiceContext(LoggerFactory);
            serviceContext.ServerOptions.Limits.MaxConcurrentConnections = max;
            return new TestServer(app, serviceContext);
        }

        private TestServer CreateServerWithMaxConnections(RequestDelegate app, ResourceCounter concurrentConnectionCounter)
        {
            var serviceContext = new TestServiceContext(LoggerFactory);

            var listenOptions = new ListenOptions(new IPEndPoint(IPAddress.Loopback, 0));
            listenOptions.Use(next =>
            {
                var middleware = new ConnectionLimitMiddleware<ConnectionContext>(c => next(c), concurrentConnectionCounter, serviceContext.Log);
                return middleware.OnConnectionAsync;
            });

            return new TestServer(app, serviceContext, listenOptions);
        }
    }
}
