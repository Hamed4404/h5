// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Components.Infrastructure;
using System.Diagnostics;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Internal;
using Microsoft.AspNetCore.Http.Features;
using Moq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Microsoft.AspNetCore.Components.Endpoints;

public class RazorComponentEndpointTest
{
    [Fact]
    public async Task CanRenderComponentStatically()
    {
        // Arrange
        var httpContext = GetTestHttpContext();
        var responseBody = new MemoryStream();
        httpContext.Response.Body = responseBody;

        // Act
        await RazorComponentEndpoint.RenderComponentToResponse(
            httpContext,
            RenderMode.Static,
            typeof(SimpleComponent),
            componentParameters: null,
            preventStreamingRendering: false);

        // Assert
        Assert.Equal("<h1>Hello from SimpleComponent</h1>", GetStringContent(responseBody));
    }

    [Fact]
    public async Task PerformsStreamingRendering()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        var httpContext = GetTestHttpContext();
        var responseBody = new MemoryStream();
        httpContext.Response.Body = responseBody;

        // Act/Assert 1: Emits the initial pre-quiescent output to the response
        var completionTask = RazorComponentEndpoint.RenderComponentToResponse(
            httpContext,
            RenderMode.Static,
            typeof(AsyncLoadingComponent),
            PropertyHelper.ObjectToDictionary(new { LoadingTask = tcs.Task }).AsReadOnly(),
            preventStreamingRendering: false);
        Assert.Equal("Loading task status: WaitingForActivation", GetStringContent(responseBody));

        // Assert 2: Result task remains incomplete for as long as the component's loading operation remains in flight
        // This keeps the HTTP response open
        await Task.Yield();
        Assert.False(completionTask.IsCompleted);

        // Act/Assert 3: When loading completes, it emits a streaming batch update and completes the response
        tcs.SetResult();
        await completionTask;
        Assert.Equal("Loading task status: WaitingForActivation<blazor-ssr><template blazor-component-id=\"2\">Loading task status: RanToCompletion</template></blazor-ssr>", GetStringContent(responseBody));
    }

    [Fact]
    public async Task WaitsForQuiescenceIfPreventStreamingRenderingIsTrue()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        var httpContext = GetTestHttpContext();
        var responseBody = new MemoryStream();
        httpContext.Response.Body = responseBody;

        // Act/Assert: Doesn't complete until loading finishes
        var completionTask = RazorComponentEndpoint.RenderComponentToResponse(
            httpContext,
            RenderMode.Static,
            typeof(AsyncLoadingComponent),
            PropertyHelper.ObjectToDictionary(new { LoadingTask = tcs.Task }).AsReadOnly(),
            preventStreamingRendering: true);
        await Task.Yield();
        Assert.False(completionTask.IsCompleted);

        // Act/Assert: Does complete when loading finishes
        tcs.SetResult();
        await completionTask;
        Assert.Equal("Loading task status: RanToCompletion", GetStringContent(responseBody));
    }

    [Fact]
    public async Task SupportsLayouts()
    {
        // Arrange
        var httpContext = GetTestHttpContext();
        var responseBody = new MemoryStream();
        httpContext.Response.Body = responseBody;

        // Act
        await RazorComponentEndpoint.RenderComponentToResponse(
            httpContext, RenderMode.Static, typeof(ComponentWithLayout),
            null, false);

        // Assert
        Assert.Equal("[TestParentLayout with content: [TestLayout with content: Page]]", GetStringContent(responseBody));
    }

    [Fact]
    public async Task OnNavigationBeforeResponseStarted_Redirects()
    {
        // Arrange
        var httpContext = GetTestHttpContext();

        // Act
        await RazorComponentEndpoint.RenderComponentToResponse(
            httpContext, RenderMode.Static, typeof(ComponentThatRedirectsSynchronously),
            null, false);

        // Assert
        Assert.Equal("https://test/somewhere/else", httpContext.Response.Headers.Location);
    }

    [Fact]
    public async Task OnNavigationAfterResponseStarted_WithStreamingOff_Throws()
    {
        // Arrange
        var httpContext = GetTestHttpContext();
        var responseMock = new Mock<IHttpResponseFeature>();
        responseMock.Setup(r => r.HasStarted).Returns(true);
        httpContext.Features.Set(responseMock.Object);

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => RazorComponentEndpoint.RenderComponentToResponse(
            httpContext, RenderMode.Static, typeof(ComponentThatRedirectsAsynchronously),
            null, preventStreamingRendering: true));

        // Assert
        Assert.Contains("A navigation command was attempted during prerendering after the server already started sending the response", ex.Message);
    }

    [Fact]
    public async Task OnNavigationAfterResponseStarted_WithStreamingOn_EmitsCommand()
    {
        // Arrange
        var httpContext = GetTestHttpContext();
        var responseBody = new MemoryStream();
        httpContext.Response.Body = responseBody;

        // Act
        await RazorComponentEndpoint.RenderComponentToResponse(
            httpContext, RenderMode.Static, typeof(ComponentThatRedirectsAsynchronously),
            null, preventStreamingRendering: false);

        // Assert
        Assert.Equal(
            "Some output<template blazor-type=\"redirection\">https://test/somewhere/else</template>",
            GetStringContent(responseBody));
    }

    [Fact]
    public async Task OnUnhandledExceptionBeforeResponseStarted_Throws()
    {
        // Arrange
        var httpContext = GetTestHttpContext();

        // Act
        var ex = await Assert.ThrowsAsync<InvalidTimeZoneException>(() => RazorComponentEndpoint.RenderComponentToResponse(
            httpContext, RenderMode.Static, typeof(ComponentThatThrowsSynchronously),
            null, false));

        // Assert
        Assert.Contains("Test message", ex.Message);
    }

    [Fact]
    public async Task OnUnhandledExceptionAfterResponseStarted_WithStreamingOff_Throws()
    {
        // Arrange
        var httpContext = GetTestHttpContext();

        // Act
        var ex = await Assert.ThrowsAsync<InvalidTimeZoneException>(() => RazorComponentEndpoint.RenderComponentToResponse(
            httpContext, RenderMode.Static, typeof(ComponentThatThrowsAsynchronously),
            null, preventStreamingRendering: true));

        // Assert
        Assert.Contains("Test message", ex.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnUnhandledExceptionAfterResponseStarted_WithStreamingOn_EmitsCommand(bool isDevelopmentEnvironment)
    {
        // Arrange
        var httpContext = GetTestHttpContext(isDevelopmentEnvironment ? Environments.Development : Environments.Production);
        var responseBody = new MemoryStream();
        httpContext.Response.Body = responseBody;

        var expectedResponseExceptionInfo = isDevelopmentEnvironment
            ? "System.InvalidTimeZoneException: Test message"
            : "There was an unhandled exception on the current request. For more details turn on detailed exceptions by setting 'DetailedErrors: true' in 'appSettings.Development.json'";

        // Act
        var ex = await Assert.ThrowsAsync<InvalidTimeZoneException>(() => RazorComponentEndpoint.RenderComponentToResponse(
            httpContext, RenderMode.Static, typeof(ComponentThatThrowsAsynchronously),
            null, preventStreamingRendering: false));

        // Assert
        Assert.Contains("Test message", ex.Message);
        Assert.Contains(
            $"Some output<template blazor-type=\"exception\">{expectedResponseExceptionInfo}",
            GetStringContent(responseBody));
    }

    private static string GetStringContent(MemoryStream stream)
    {
        stream.Position = 0;
        return new StreamReader(stream).ReadToEnd();
    }

    class AsyncLoadingComponent : ComponentBase
    {
        [Parameter] public Task LoadingTask { get; set; }

        protected override async Task OnInitializedAsync()
        {
            await LoadingTask;
            await Task.Delay(100); // Doesn't strictly make any difference to the test, but clarifies that arbitrary async stuff could happen here
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
            => builder.AddContent(0, $"Loading task status: {LoadingTask.Status}");
    }

    [Layout(typeof(TestLayout))]
    class ComponentWithLayout : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
            => builder.AddContent(0, $"Page");
    }

    [Layout(typeof(TestParentLayout))]
    class TestLayout : LayoutComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, $"[{nameof(TestLayout)} with content: ");
            builder.AddContent(1, Body);
            builder.AddContent(2, "]");
        }
    }

    class TestParentLayout : LayoutComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, $"[{nameof(TestParentLayout)} with content: ");
            builder.AddContent(1, Body);
            builder.AddContent(2, "]");
        }
    }

    class ComponentThatRedirectsSynchronously : ComponentBase
    {
        [Inject] private NavigationManager Nav { get; set; }

        protected override void OnInitialized()
            => Nav.NavigateTo("/somewhere/else");
    }

    class ComponentThatRedirectsAsynchronously : ComponentBase
    {
        [Inject] private NavigationManager Nav { get; set; }

        protected override async Task OnInitializedAsync()
        {
            await Task.Yield();
            Nav.NavigateTo("/somewhere/else");
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
            => builder.AddContent(0, "Some output");
    }

    class ComponentThatThrowsSynchronously : ComponentBase
    {
        protected override void OnInitialized()
            => throw new InvalidTimeZoneException("Test message");
    }

    class ComponentThatThrowsAsynchronously : ComponentBase
    {
        protected override async Task OnInitializedAsync()
        {
            await Task.Yield();
            throw new InvalidTimeZoneException("Test message");
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
            => builder.AddContent(0, "Some output");
    }

    public static DefaultHttpContext GetTestHttpContext(string environmentName = null)
    {
        var mockWebHostEnvironment = Mock.Of<IWebHostEnvironment>(
            x => x.EnvironmentName == (environmentName ?? Environments.Production));
        var serviceCollection = new ServiceCollection()
            .AddSingleton(new DiagnosticListener("test"))
            .AddSingleton<IWebHostEnvironment>(mockWebHostEnvironment)
            .AddSingleton<RazorComponentResultExecutor>()
            .AddSingleton<EndpointHtmlRenderer>()
            .AddSingleton<IComponentPrerenderer>(services => services.GetRequiredService<EndpointHtmlRenderer>())
            .AddSingleton<NavigationManager, FakeNavigationManager>()
            .AddSingleton<ServerComponentSerializer>()
            .AddSingleton<ComponentStatePersistenceManager>()
            .AddSingleton<IDataProtectionProvider, FakeDataProtectionProvider>()
            .AddLogging();
        var result = new DefaultHttpContext { RequestServices = serviceCollection.BuildServiceProvider() };
        result.Request.Scheme = "https";
        result.Request.Host = new HostString("test");
        return result;
    }

    class SimpleComponent : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "h1");
            builder.AddContent(1, "Hello from SimpleComponent");
            builder.CloseElement();
        }
    }

    class FakeDataProtectionProvider : IDataProtectionProvider
    {
        public IDataProtector CreateProtector(string purpose)
            => new FakeDataProtector();

        class FakeDataProtector : IDataProtector
        {
            public IDataProtector CreateProtector(string purpose) => throw new NotImplementedException();
            public byte[] Protect(byte[] plaintext) => throw new NotImplementedException();
            public byte[] Unprotect(byte[] protectedData) => throw new NotImplementedException();
        }
    }

    class FakeNavigationManager : NavigationManager, IHostEnvironmentNavigationManager
    {
        public new void Initialize(string baseUri, string uri)
            => base.Initialize(baseUri, uri);

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            // Equivalent to what RemoteNavigationManager would do
            var absoluteUriString = ToAbsoluteUri(uri).ToString();
            throw new NavigationException(absoluteUriString);
        }
    }
}
