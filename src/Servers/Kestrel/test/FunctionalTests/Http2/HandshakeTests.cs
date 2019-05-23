// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core.Features;
using Microsoft.AspNetCore.Testing;
using Microsoft.AspNetCore.Testing.xunit;
using Microsoft.Extensions.Logging.Testing;
using Xunit;

namespace Microsoft.AspNetCore.Server.Kestrel.FunctionalTests.Http2
{
    [OSSkipCondition(OperatingSystems.MacOSX, SkipReason = "Missing SslStream ALPN support: https://github.com/dotnet/corefx/issues/30492")]
    [SkipOnHelix("https://github.com/aspnet/AspNetCore/issues/10428", Queues = "Debian.8.Amd64.Open")] // Debian 8 uses OpenSSL 1.0.1 which does not support HTTP/2
    [MinimumOSVersion(OperatingSystems.Windows, WindowsVersions.Win10)]
    public class HandshakeTests : LoggedTest
    {
        private static X509Certificate2 _x509Certificate2 = TestResources.GetTestCertificate();

        public HttpClient Client { get; set; }

        public HandshakeTests()
        {
            Client = new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            })
            {
                DefaultRequestVersion = new Version(2, 0),
            };
        }

        [ConditionalFact]
        public async Task TlsAlpnHandshakeSelectsHttp2From1and2()
        {
            using (var server = new TestServer(context =>
            {
                var tlsFeature = context.Features.Get<ITlsApplicationProtocolFeature>();
                Assert.NotNull(tlsFeature);
                Assert.True(SslApplicationProtocol.Http2.Protocol.Span.SequenceEqual(tlsFeature.ApplicationProtocol.Span),
                    "ALPN: " + tlsFeature.ApplicationProtocol.Length);

                return context.Response.WriteAsync("hello world " + context.Request.Protocol);
            }, new TestServiceContext(LoggerFactory),
            kestrelOptions =>
            {
                kestrelOptions.Listen(IPAddress.Loopback, 0, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                    listenOptions.UseHttps(_x509Certificate2);
                });
            }))
            {
                var result = await Client.GetStringAsync($"https://localhost:{server.Port}/");
                Assert.Equal("hello world HTTP/2", result);

                await server.StopAsync();
            }
        }

        [ConditionalFact]
        public async Task TlsAlpnHandshakeSelectsHttp2()
        {
            using (var server = new TestServer(context =>
            {
                var tlsFeature = context.Features.Get<ITlsApplicationProtocolFeature>();
                Assert.NotNull(tlsFeature);
                Assert.True(SslApplicationProtocol.Http2.Protocol.Span.SequenceEqual(tlsFeature.ApplicationProtocol.Span),
                    "ALPN: " + tlsFeature.ApplicationProtocol.Length);

                return context.Response.WriteAsync("hello world " + context.Request.Protocol);
            }, new TestServiceContext(LoggerFactory),
            kestrelOptions =>
            {
                kestrelOptions.Listen(IPAddress.Loopback, 0, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http2;
                    listenOptions.UseHttps(_x509Certificate2);
                });
            }))
            {
                var result = await Client.GetStringAsync($"https://localhost:{server.Port}/");
                Assert.Equal("hello world HTTP/2", result);
                await server.StopAsync();
            }
        }
    }
}
