// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Tests;
using Microsoft.AspNetCore.InternalTesting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Xunit;
using Moq.Protected;
using System.IO.Pipelines;
using System.Text;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http.Connections.Client.Internal;

namespace Microsoft.AspNetCore.SignalR.Client.Tests;

public partial class HttpConnectionTests : VerifiableLoggedTest
{
    [Fact]
    public void HttpConnectionOptionsDefaults()
    {
        var httpOptions = new HttpConnectionOptions();
        Assert.Equal(1024 * 1024, httpOptions.TransportMaxBufferSize);
        Assert.Equal(1024 * 1024, httpOptions.ApplicationMaxBufferSize);
        Assert.Equal(TimeSpan.FromSeconds(5), httpOptions.CloseTimeout);
        Assert.Equal(TransferFormat.Binary, httpOptions.DefaultTransferFormat);
        Assert.Equal(HttpTransports.All, httpOptions.Transports);
    }

    [Fact]
    public void HttpConnectionOptionsNegativeBufferSizeThrows()
    {
        var httpOptions = new HttpConnectionOptions();
        Assert.Throws<ArgumentOutOfRangeException>(() => httpOptions.TransportMaxBufferSize = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => httpOptions.ApplicationMaxBufferSize = -1);
    }

    [Fact]
    public void CannotCreateConnectionWithNullUrl()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new HttpConnection(null));
        Assert.Equal("url", exception.ParamName);
    }

    [Fact]
    public void CannotCreateConnectionWithNullUrlOnOptions()
    {
        var exception = Assert.Throws<ArgumentException>(() => new HttpConnection(new HttpConnectionOptions(), NullLoggerFactory.Instance));
        Assert.Equal("httpConnectionOptions", exception.ParamName);
    }

    [Fact]
    public void CannotSetConnectionId()
    {
        var connection = new HttpConnection(new Uri("http://fakeuri.org/"));
        var exception = Assert.Throws<InvalidOperationException>(() => connection.ConnectionId = "custom conneciton ID");
        Assert.Equal("The ConnectionId is set internally and should not be set by user code.", exception.Message);
    }

    [Fact]
    public async Task HttpOptionsSetOntoHttpClientHandler()
    {
        var testHttpHandler = TestHttpMessageHandler.CreateDefault();

        var negotiateUrlTcs = new TaskCompletionSource<string>();
        testHttpHandler.OnNegotiate((request, cancellationToken) =>
        {
            negotiateUrlTcs.TrySetResult(request.RequestUri.ToString());
            return ResponseUtils.CreateResponse(HttpStatusCode.OK,
                ResponseUtils.CreateNegotiationContent());
        });

        HttpClientHandler httpClientHandler = null;

        var httpOptions = new HttpConnectionOptions();
        httpOptions.Url = new Uri("http://fakeuri.org/");
        httpOptions.HttpMessageHandlerFactory = inner =>
        {
            httpClientHandler = (HttpClientHandler)inner;
            return testHttpHandler;
        };
        httpOptions.Cookies.Add(new Cookie("Name", "Value", string.Empty, "fakeuri.org"));
        var clientCertificate = new X509Certificate(Array.Empty<byte>());
        httpOptions.ClientCertificates.Add(clientCertificate);
        httpOptions.UseDefaultCredentials = false;
        httpOptions.Credentials = Mock.Of<ICredentials>();
        httpOptions.Proxy = Mock.Of<IWebProxy>();
        httpOptions.Transports = HttpTransportType.LongPolling;

        await WithConnectionAsync(
            CreateConnection(httpOptions),
            async (connection) =>
            {
                await connection.StartAsync().DefaultTimeout();
            });

        Assert.NotNull(httpClientHandler);
        Assert.Equal(1, httpClientHandler.CookieContainer.Count);
        Assert.Single(httpClientHandler.ClientCertificates);
        Assert.Same(clientCertificate, httpClientHandler.ClientCertificates[0]);
        Assert.False(httpClientHandler.UseDefaultCredentials);
        Assert.Same(httpOptions.Proxy, httpClientHandler.Proxy);
        Assert.Same(httpOptions.Credentials, httpClientHandler.Credentials);
    }

    [Fact]
    public void HttpOptionsCannotSetNullCookieContainer()
    {
        var httpOptions = new HttpConnectionOptions();
        Assert.NotNull(httpOptions.Cookies);
        Assert.Throws<ArgumentNullException>(() => httpOptions.Cookies = null);
    }

    [Fact]
    public async Task HttpRequestAndErrorResponseLogged()
    {
        var testHttpHandler = new TestHttpMessageHandler(false);

        testHttpHandler.OnNegotiate((request, cancellationToken) => ResponseUtils.CreateResponse(HttpStatusCode.BadGateway));

        var httpOptions = new HttpConnectionOptions();
        httpOptions.Url = new Uri("http://fakeuri.org/");
        httpOptions.HttpMessageHandlerFactory = inner => testHttpHandler;

        const string loggerName = "Microsoft.AspNetCore.Http.Connections.Client.Internal.LoggingHttpMessageHandler";
        var testSink = new TestSink();
        var logger = new TestLogger(loggerName, testSink, true);

        var mockLoggerFactory = new Mock<ILoggerFactory>();
        mockLoggerFactory
            .Setup(m => m.CreateLogger(It.IsAny<string>()))
            .Returns((string categoryName) => (categoryName == loggerName) ? (ILogger)logger : NullLogger.Instance);

        try
        {
            await WithConnectionAsync(
                CreateConnection(httpOptions, loggerFactory: mockLoggerFactory.Object),
                async (connection) =>
                {
                    await connection.StartAsync().DefaultTimeout();
                });
        }
        catch
        {
            // ignore connection error
        }

        var writeList = testSink.Writes.ToList();

        Assert.Equal(2, writeList.Count);
        Assert.Equal("SendingHttpRequest", writeList[0].EventId.Name);
        Assert.Equal("UnsuccessfulHttpResponse", writeList[1].EventId.Name);
    }

    [Fact]
    public async Task Negotiation_SendsCorrectHeaders()
    {
        try
        {
            var mockHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHandler.Object);
            var options = new HttpConnectionOptions
            {
                Url = new Uri("http://fakeuri.org/"),
                Transports = HttpTransportType.WebSockets,
                SkipNegotiation = false
            };

            var loggerFactory = NullLoggerFactory.Instance;
            var mockTransportFactory = new Mock<ITransportFactory>();
            var mockTransport = new Mock<ITransport>();
            mockTransportFactory.Setup(x => x.CreateTransport(It.IsAny<HttpTransportType>(), It.IsAny<bool>()))
                                .Returns(mockTransport.Object);

            var connection = new HttpConnection(options, loggerFactory, mockTransportFactory.Object, httpClient);

            mockHandler.Protected().Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, cancellationToken) =>
            {
                Assert.Contains(new MediaTypeWithQualityHeaderValue("*/*"), request.Headers.Accept);
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"connectionId\":\"12345\",\"availableTransports\":[]}", Encoding.UTF8, "application/json")
            });

            await connection.StartAsync();
        }
        catch (Exception ex)
        {
            Assert.False(true, $"Unexpected exception: {ex.Message}");
        }
    }
}
