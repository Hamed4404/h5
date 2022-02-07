// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Constraints;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Mvc.ApiExplorer;

public class EndpointMetadataApiDescriptionProviderTest
{
    [Fact]
    public void MultipleApiDescriptionsCreatedForMultipleHttpMethods()
    {
        var apiDescriptions = GetApiDescriptions(() => { }, "/", new string[] { "FOO", "BAR" });

        Assert.Equal(2, apiDescriptions.Count);
    }

    [Fact]
    public void ApiDescriptionNotCreatedIfNoHttpMethods()
    {
        var apiDescriptions = GetApiDescriptions(() => { }, "/", Array.Empty<string>());

        Assert.Empty(apiDescriptions);
    }

    [Fact]
    public void UsesDeclaringTypeAsControllerName()
    {
        var apiDescription = GetApiDescription(TestAction);

        var declaringTypeName = typeof(EndpointMetadataApiDescriptionProviderTest).Name;
        Assert.Equal(declaringTypeName, apiDescription.ActionDescriptor.RouteValues["controller"]);
    }

    [Fact]
    public void UsesApplicationNameAsControllerNameIfNoDeclaringType()
    {
        var apiDescription = GetApiDescription(() => { });

        Assert.Equal(nameof(EndpointMetadataApiDescriptionProviderTest), apiDescription.ActionDescriptor.RouteValues["controller"]);
    }

    [Fact]
    public void AddsRequestFormatFromMetadata()
    {
        static void AssertCustomRequestFormat(ApiDescription apiDescription)
        {
            var requestFormat = Assert.Single(apiDescription.SupportedRequestFormats);
            Assert.Equal("application/custom", requestFormat.MediaType);
            Assert.Null(requestFormat.Formatter);
        }

        AssertCustomRequestFormat(GetApiDescription(
            [Consumes("application/custom")]
        (InferredJsonClass fromBody) =>
            { }));

        AssertCustomRequestFormat(GetApiDescription(
            [Consumes("application/custom")]
        ([FromBody] int fromBody) =>
            { }));
    }

    [Fact]
    public void AddsMultipleRequestFormatsFromMetadata()
    {
        var apiDescription = GetApiDescription(
            [Consumes("application/custom0", "application/custom1")]
        (InferredJsonClass fromBody) =>
            { });

        Assert.Equal(2, apiDescription.SupportedRequestFormats.Count);

        var requestFormat0 = apiDescription.SupportedRequestFormats[0];
        Assert.Equal("application/custom0", requestFormat0.MediaType);
        Assert.Null(requestFormat0.Formatter);

        var requestFormat1 = apiDescription.SupportedRequestFormats[1];
        Assert.Equal("application/custom1", requestFormat1.MediaType);
        Assert.Null(requestFormat1.Formatter);
    }

    [Fact]
    public void AddsMultipleRequestFormatsFromMetadataWithRequestTypeAndOptionalBodyParameter()
    {
        var apiDescription = GetApiDescription(
            [Consumes(typeof(InferredJsonClass), "application/custom0", "application/custom1", IsOptional = true)]
        () =>
            { });

        Assert.Equal(2, apiDescription.SupportedRequestFormats.Count);

        var apiParameterDescription = apiDescription.ParameterDescriptions[0];
        Assert.Equal("InferredJsonClass", apiParameterDescription.Type.Name);
        Assert.False(apiParameterDescription.IsRequired);
    }

#nullable enable

    [Fact]
    public void AddsMultipleRequestFormatsFromMetadataWithRequiredBodyParameter()
    {
        var apiDescription = GetApiDescription(
            [Consumes(typeof(InferredJsonClass), "application/custom0", "application/custom1", IsOptional = false)]
        (InferredJsonClass fromBody) =>
            { });

        Assert.Equal(2, apiDescription.SupportedRequestFormats.Count);

        var apiParameterDescription = apiDescription.ParameterDescriptions[0];
        Assert.Equal("InferredJsonClass", apiParameterDescription.Type.Name);
        Assert.True(apiParameterDescription.IsRequired);
    }

#nullable disable

    [Fact]
    public void AddsJsonResponseFormatWhenFromBodyInferred()
    {
        static void AssertJsonResponse(ApiDescription apiDescription, Type expectedType)
        {
            var responseType = Assert.Single(apiDescription.SupportedResponseTypes);
            Assert.Equal(200, responseType.StatusCode);
            Assert.Equal(expectedType, responseType.Type);
            Assert.Equal(expectedType, responseType.ModelMetadata.ModelType);

            var responseFormat = Assert.Single(responseType.ApiResponseFormats);
            Assert.Equal("application/json", responseFormat.MediaType);
            Assert.Null(responseFormat.Formatter);
        }

        AssertJsonResponse(GetApiDescription(() => new InferredJsonClass()), typeof(InferredJsonClass));
        AssertJsonResponse(GetApiDescription(() => (IInferredJsonInterface)null), typeof(IInferredJsonInterface));
    }

    [Fact]
    public void AddsTextResponseFormatWhenFromBodyInferred()
    {
        var apiDescription = GetApiDescription(() => "foo");

        var responseType = Assert.Single(apiDescription.SupportedResponseTypes);
        Assert.Equal(200, responseType.StatusCode);
        Assert.Equal(typeof(string), responseType.Type);
        Assert.Equal(typeof(string), responseType.ModelMetadata.ModelType);

        var responseFormat = Assert.Single(responseType.ApiResponseFormats);
        Assert.Equal("text/plain", responseFormat.MediaType);
        Assert.Null(responseFormat.Formatter);
    }

    [Fact]
    public void AddsNoResponseFormatWhenItCannotBeInferredAndTheresNoMetadata()
    {
        static void AssertVoid(ApiDescription apiDescription)
        {
            var responseType = Assert.Single(apiDescription.SupportedResponseTypes);
            Assert.Equal(200, responseType.StatusCode);
            Assert.Equal(typeof(void), responseType.Type);
            Assert.Equal(typeof(void), responseType.ModelMetadata.ModelType);

            Assert.Empty(responseType.ApiResponseFormats);
        }

        AssertVoid(GetApiDescription(() => { }));
        AssertVoid(GetApiDescription(() => Task.CompletedTask));
        AssertVoid(GetApiDescription(() => new ValueTask()));
    }

    [Fact]
    public void AddsResponseFormatFromMetadata()
    {
        var apiDescription = GetApiDescription(
            [ProducesResponseType(typeof(TimeSpan), StatusCodes.Status201Created)]
        [Produces("application/custom")]
        () => new InferredJsonClass());

        var responseType = Assert.Single(apiDescription.SupportedResponseTypes);

        Assert.Equal(201, responseType.StatusCode);
        Assert.Equal(typeof(TimeSpan), responseType.Type);
        Assert.Equal(typeof(TimeSpan), responseType.ModelMetadata.ModelType);

        var responseFormat = Assert.Single(responseType.ApiResponseFormats);
        Assert.Equal("application/custom", responseFormat.MediaType);
    }

    [Fact]
    public void AddsMultipleResponseFormatsFromMetadataWithPoco()
    {
        var apiDescription = GetApiDescription(
            [ProducesResponseType(typeof(TimeSpan), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        () => new InferredJsonClass());

        Assert.Equal(2, apiDescription.SupportedResponseTypes.Count);

        var createdResponseType = apiDescription.SupportedResponseTypes[0];

        Assert.Equal(201, createdResponseType.StatusCode);
        Assert.Equal(typeof(TimeSpan), createdResponseType.Type);
        Assert.Equal(typeof(TimeSpan), createdResponseType.ModelMetadata.ModelType);

        var createdResponseFormat = Assert.Single(createdResponseType.ApiResponseFormats);
        Assert.Equal("application/json", createdResponseFormat.MediaType);

        var badRequestResponseType = apiDescription.SupportedResponseTypes[1];

        Assert.Equal(400, badRequestResponseType.StatusCode);
        Assert.Equal(typeof(InferredJsonClass), badRequestResponseType.Type);
        Assert.Equal(typeof(InferredJsonClass), badRequestResponseType.ModelMetadata.ModelType);

        var badRequestResponseFormat = Assert.Single(badRequestResponseType.ApiResponseFormats);
        Assert.Equal("application/json", badRequestResponseFormat.MediaType);
    }

    [Fact]
    public void AddsMultipleResponseFormatsFromMetadataWithIResult()
    {
        var apiDescription = GetApiDescription(
            [ProducesResponseType(typeof(InferredJsonClass), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        () => Results.Ok(new InferredJsonClass()));

        Assert.Equal(2, apiDescription.SupportedResponseTypes.Count);

        var createdResponseType = apiDescription.SupportedResponseTypes[0];

        Assert.Equal(201, createdResponseType.StatusCode);
        Assert.Equal(typeof(InferredJsonClass), createdResponseType.Type);
        Assert.Equal(typeof(InferredJsonClass), createdResponseType.ModelMetadata.ModelType);

        var createdResponseFormat = Assert.Single(createdResponseType.ApiResponseFormats);
        Assert.Equal("application/json", createdResponseFormat.MediaType);

        var badRequestResponseType = apiDescription.SupportedResponseTypes[1];

        Assert.Equal(400, badRequestResponseType.StatusCode);
        Assert.Equal(typeof(void), badRequestResponseType.Type);
        Assert.Equal(typeof(void), badRequestResponseType.ModelMetadata.ModelType);

        Assert.Empty(badRequestResponseType.ApiResponseFormats);
    }

    [Fact]
    public void AddsFromRouteParameterAsPath()
    {
        static void AssertPathParameter(ApiDescription apiDescription)
        {
            var param = Assert.Single(apiDescription.ParameterDescriptions);
            Assert.Equal(typeof(int), param.Type);
            Assert.Equal(typeof(int), param.ModelMetadata.ModelType);
            Assert.Equal(BindingSource.Path, param.Source);
        }

        AssertPathParameter(GetApiDescription((int foo) => { }, "/{foo}"));
        AssertPathParameter(GetApiDescription(([FromRoute] int foo) => { }));
    }

    [Fact]
    public void AddsFromRouteParameterAsPathWithCustomClassWithTryParse()
    {
        static void AssertPathParameter(ApiDescription apiDescription)
        {
            var param = Assert.Single(apiDescription.ParameterDescriptions);
            Assert.Equal(typeof(TryParseStringRecord), param.Type);
            Assert.Equal(typeof(string), param.ModelMetadata.ModelType);
            Assert.Equal(BindingSource.Path, param.Source);
        }

        AssertPathParameter(GetApiDescription((TryParseStringRecord foo) => { }, "/{foo}"));
    }

    [Fact]
    public void AddsFromRouteParameterAsPathWithPrimitiveType()
    {
        static void AssertPathParameter(ApiDescription apiDescription)
        {
            var param = Assert.Single(apiDescription.ParameterDescriptions);
            Assert.Equal(typeof(int), param.Type);
            Assert.Equal(typeof(int), param.ModelMetadata.ModelType);
            Assert.Equal(BindingSource.Path, param.Source);
        }

        AssertPathParameter(GetApiDescription((int foo) => { }, "/{foo}"));
    }

    [Fact]
    public void AddsFromRouteParameterAsPathWithNullablePrimitiveType()
    {
        static void AssertPathParameter(ApiDescription apiDescription)
        {
            var param = Assert.Single(apiDescription.ParameterDescriptions);
            Assert.Equal(typeof(int?), param.Type);
            Assert.Equal(typeof(int?), param.ModelMetadata.ModelType);
            Assert.Equal(BindingSource.Path, param.Source);
        }

        AssertPathParameter(GetApiDescription((int? foo) => { }, "/{foo}"));
    }

    [Fact]
    public void AddsFromRouteParameterAsPathWithStructTypeWithTryParse()
    {
        static void AssertPathParameter(ApiDescription apiDescription)
        {
            var param = Assert.Single(apiDescription.ParameterDescriptions);
            Assert.Equal(typeof(TryParseStringRecordStruct), param.Type);
            Assert.Equal(typeof(string), param.ModelMetadata.ModelType);
            Assert.Equal(BindingSource.Path, param.Source);
        }

        AssertPathParameter(GetApiDescription((TryParseStringRecordStruct foo) => { }, "/{foo}"));
    }

    [Fact]
    public void AddsFromQueryParameterAsQuery()
    {
        static void AssertQueryParameter(ApiDescription apiDescription)
        {
            var param = Assert.Single(apiDescription.ParameterDescriptions);
            Assert.Equal(typeof(int), param.Type);
            Assert.Equal(typeof(int), param.ModelMetadata.ModelType);
            Assert.Equal(BindingSource.Query, param.Source);
        }

        AssertQueryParameter(GetApiDescription((int foo) => { }, "/"));
        AssertQueryParameter(GetApiDescription(([FromQuery] int foo) => { }));
    }

    [Fact]
    public void AddsFromHeaderParameterAsHeader()
    {
        var apiDescription = GetApiDescription(([FromHeader] int foo) => { });
        var param = Assert.Single(apiDescription.ParameterDescriptions);

        Assert.Equal(typeof(int), param.Type);
        Assert.Equal(typeof(int), param.ModelMetadata.ModelType);
        Assert.Equal(BindingSource.Header, param.Source);
    }

    [Fact]
    public void DoesNotAddFromServiceParameterAsService()
    {
        Assert.Empty(GetApiDescription((IInferredServiceInterface foo) => { }).ParameterDescriptions);
        Assert.Empty(GetApiDescription(([FromServices] int foo) => { }).ParameterDescriptions);
        Assert.Empty(GetApiDescription((HttpContext context) => { }).ParameterDescriptions);
        Assert.Empty(GetApiDescription((HttpRequest request) => { }).ParameterDescriptions);
        Assert.Empty(GetApiDescription((HttpResponse response) => { }).ParameterDescriptions);
        Assert.Empty(GetApiDescription((ClaimsPrincipal user) => { }).ParameterDescriptions);
        Assert.Empty(GetApiDescription((CancellationToken token) => { }).ParameterDescriptions);
        Assert.Empty(GetApiDescription((BindAsyncRecord context) => { }).ParameterDescriptions);
    }

    [Fact]
    public void AddsBodyParameterInTheParameterDescription()
    {
        static void AssertBodyParameter(ApiDescription apiDescription, string expectedName, Type expectedType)
        {
            var param = Assert.Single(apiDescription.ParameterDescriptions);
            Assert.Equal(expectedName, param.Name);
            Assert.Equal(expectedType, param.Type);
            Assert.Equal(expectedType, param.ModelMetadata.ModelType);
            Assert.Equal(BindingSource.Body, param.Source);
        }

        AssertBodyParameter(GetApiDescription((InferredJsonClass foo) => { }), "foo", typeof(InferredJsonClass));
        AssertBodyParameter(GetApiDescription(([FromBody] int bar) => { }), "bar", typeof(int));
    }

    [Fact]
    public void AddsDefaultValueFromParameters()
    {
        var apiDescription = GetApiDescription(TestActionWithDefaultValue);

        var param = Assert.Single(apiDescription.ParameterDescriptions);
        Assert.Equal(42, param.DefaultValue);
    }

#nullable enable

    [Fact]
    public void AddsMultipleParameters()
    {
        var apiDescription = GetApiDescription(([FromRoute] int foo, int bar, InferredJsonClass fromBody) => { });
        Assert.Equal(3, apiDescription.ParameterDescriptions.Count);

        var fooParam = apiDescription.ParameterDescriptions[0];
        Assert.Equal("foo", fooParam.Name);
        Assert.Equal(typeof(int), fooParam.Type);
        Assert.Equal(typeof(int), fooParam.ModelMetadata.ModelType);
        Assert.Equal(BindingSource.Path, fooParam.Source);
        Assert.True(fooParam.IsRequired);

        var barParam = apiDescription.ParameterDescriptions[1];
        Assert.Equal("bar", barParam.Name);
        Assert.Equal(typeof(int), barParam.Type);
        Assert.Equal(typeof(int), barParam.ModelMetadata.ModelType);
        Assert.Equal(BindingSource.Query, barParam.Source);
        Assert.True(barParam.IsRequired);

        var fromBodyParam = apiDescription.ParameterDescriptions[2];
        Assert.Equal("fromBody", fromBodyParam.Name);
        Assert.Equal(typeof(InferredJsonClass), fromBodyParam.Type);
        Assert.Equal(typeof(InferredJsonClass), fromBodyParam.ModelMetadata.ModelType);
        Assert.Equal(BindingSource.Body, fromBodyParam.Source);
        Assert.True(fromBodyParam.IsRequired);
    }

#nullable disable

    [Fact]
    public void TestParameterIsRequired()
    {
        var apiDescription = GetApiDescription(([FromRoute] int foo, int? bar) => { });
        Assert.Equal(2, apiDescription.ParameterDescriptions.Count);

        var fooParam = apiDescription.ParameterDescriptions[0];
        Assert.Equal(typeof(int), fooParam.Type);
        Assert.Equal(typeof(int), fooParam.ModelMetadata.ModelType);
        Assert.Equal(BindingSource.Path, fooParam.Source);
        Assert.True(fooParam.IsRequired);

        var barParam = apiDescription.ParameterDescriptions[1];
        Assert.Equal(typeof(int?), barParam.Type);
        Assert.Equal(typeof(int?), barParam.ModelMetadata.ModelType);
        Assert.Equal(BindingSource.Query, barParam.Source);
        Assert.False(barParam.IsRequired);
    }

    [Fact]
    public void AddsDisplayNameFromRouteEndpoint()
    {
        var apiDescription = GetApiDescription(() => "foo", displayName: "FOO");

        Assert.Equal("FOO", apiDescription.ActionDescriptor.DisplayName);
    }

    [Fact]
    public void AddsMetadataFromRouteEndpoint()
    {
        var apiDescription = GetApiDescription([ApiExplorerSettings(IgnoreApi = true)]() => { });

        Assert.NotEmpty(apiDescription.ActionDescriptor.EndpointMetadata);

        var apiExplorerSettings = apiDescription.ActionDescriptor.EndpointMetadata
            .OfType<ApiExplorerSettingsAttribute>()
            .FirstOrDefault();

        Assert.NotNull(apiExplorerSettings);
        Assert.True(apiExplorerSettings.IgnoreApi);
    }

    [Fact]
    public void TestParameterIsRequiredForObliviousNullabilityContext()
    {
        // In an oblivious nullability context, reference type parameters without
        // annotations are optional. Value type parameters are always required.
        var apiDescription = GetApiDescription((string foo, int bar) => { });
        Assert.Equal(2, apiDescription.ParameterDescriptions.Count);

        var fooParam = apiDescription.ParameterDescriptions[0];
        Assert.Equal(typeof(string), fooParam.Type);
        Assert.Equal(typeof(string), fooParam.ModelMetadata.ModelType);
        Assert.Equal(BindingSource.Query, fooParam.Source);
        Assert.False(fooParam.IsRequired);

        var barParam = apiDescription.ParameterDescriptions[1];
        Assert.Equal(typeof(int), barParam.Type);
        Assert.Equal(typeof(int), barParam.ModelMetadata.ModelType);
        Assert.Equal(BindingSource.Query, barParam.Source);
        Assert.True(barParam.IsRequired);
    }

    [Fact]
    public void TestParameterAttributesCanBeInspected()
    {
        var apiDescription = GetApiDescription(([System.ComponentModel.Description("The name.")] string name) => { });
        Assert.Equal(1, apiDescription.ParameterDescriptions.Count);

        var nameParam = apiDescription.ParameterDescriptions[0];
        Assert.Equal(typeof(string), nameParam.Type);
        Assert.Equal(typeof(string), nameParam.ModelMetadata.ModelType);
        Assert.Equal(BindingSource.Query, nameParam.Source);
        Assert.False(nameParam.IsRequired);

        Assert.NotNull(nameParam.ParameterDescriptor);
        Assert.Equal("name", nameParam.ParameterDescriptor.Name);
        Assert.Equal(typeof(string), nameParam.ParameterDescriptor.ParameterType);

        var descriptor = Assert.IsAssignableFrom<IParameterInfoParameterDescriptor>(nameParam.ParameterDescriptor);

        Assert.NotNull(descriptor.ParameterInfo);

        var description = Assert.Single(descriptor.ParameterInfo.GetCustomAttributes<System.ComponentModel.DescriptionAttribute>());

        Assert.NotNull(description);
        Assert.Equal("The name.", description.Description);
    }

    [Fact]
    public void RespectsProducesProblemExtensionMethod()
    {
        // Arrange
        var builder = CreateBuilder();
        builder.MapGet("/api/todos", () => "").ProducesProblem(StatusCodes.Status400BadRequest);
        var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

        var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
        var hostEnvironment = new HostEnvironment
        {
            ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest)
        };
        var provider = CreateEndpointMetadataApiDescriptionProvider(endpointDataSource);

        // Act
        provider.OnProvidersExecuting(context);

        // Assert
        var apiDescription = Assert.Single(context.Results);
        var responseTypes = Assert.Single(apiDescription.SupportedResponseTypes);
        Assert.Equal(typeof(ProblemDetails), responseTypes.Type);
    }

    [Fact]
    public void RespectsProducesWithGroupNameExtensionMethod()
    {
        // Arrange
        var endpointGroupName = "SomeEndpointGroupName";
        var builder = CreateBuilder();
        builder.MapGet("/api/todos", () => "").Produces<InferredJsonClass>().WithGroupName(endpointGroupName);
        var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

        var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
        var hostEnvironment = new HostEnvironment
        {
            ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest)
        };
        var provider = CreateEndpointMetadataApiDescriptionProvider(endpointDataSource);

        // Act
        provider.OnProvidersExecuting(context);

        // Assert
        var apiDescription = Assert.Single(context.Results);
        var responseTypes = Assert.Single(apiDescription.SupportedResponseTypes);
        Assert.Equal(typeof(InferredJsonClass), responseTypes.Type);
        Assert.Equal(endpointGroupName, apiDescription.GroupName);
    }

    [Fact]
    public void RespectsExcludeFromDescription()
    {
        // Arrange
        var builder = CreateBuilder();
        builder.MapGet("/api/todos", () => "").Produces<InferredJsonClass>().ExcludeFromDescription();
        var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

        var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
        var hostEnvironment = new HostEnvironment
        {
            ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest)
        };
        var provider = CreateEndpointMetadataApiDescriptionProvider(endpointDataSource);

        // Act
        provider.OnProvidersExecuting(context);

        // Assert
        Assert.Empty(context.Results);
    }

    [Fact]
    public void HandlesProducesWithProducesProblem()
    {
        // Arrange
        var builder = CreateBuilder();
        builder.MapGet("/api/todos", () => "")
            .Produces<InferredJsonClass>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

        var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
        var hostEnvironment = new HostEnvironment
        {
            ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest)
        };
        var provider = CreateEndpointMetadataApiDescriptionProvider(endpointDataSource);

        // Act
        provider.OnProvidersExecuting(context);
        provider.OnProvidersExecuted(context);

        // Assert
        Assert.Collection(
            context.Results.SelectMany(r => r.SupportedResponseTypes).OrderBy(r => r.StatusCode),
            responseType =>
            {
                Assert.Equal(typeof(InferredJsonClass), responseType.Type);
                Assert.Equal(200, responseType.StatusCode);
                Assert.Equal(new[] { "application/json" }, GetSortedMediaTypes(responseType));
            },
            responseType =>
            {
                Assert.Equal(typeof(HttpValidationProblemDetails), responseType.Type);
                Assert.Equal(400, responseType.StatusCode);
                Assert.Equal(new[] { "application/problem+json" }, GetSortedMediaTypes(responseType));
            },
            responseType =>
            {
                Assert.Equal(typeof(ProblemDetails), responseType.Type);
                Assert.Equal(404, responseType.StatusCode);
                Assert.Equal(new[] { "application/problem+json" }, GetSortedMediaTypes(responseType));
            },
            responseType =>
            {
                Assert.Equal(typeof(ProblemDetails), responseType.Type);
                Assert.Equal(409, responseType.StatusCode);
                Assert.Equal(new[] { "application/problem+json" }, GetSortedMediaTypes(responseType));
            });
    }

    [Fact]
    public void HandleMultipleProduces()
    {
        // Arrange
        var builder = CreateBuilder();
        builder.MapGet("/api/todos", () => "")
            .Produces<InferredJsonClass>(StatusCodes.Status200OK)
            .Produces<InferredJsonClass>(StatusCodes.Status201Created);
        var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

        var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
        var hostEnvironment = new HostEnvironment
        {
            ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest)
        };
        var provider = CreateEndpointMetadataApiDescriptionProvider(endpointDataSource);

        // Act
        provider.OnProvidersExecuting(context);
        provider.OnProvidersExecuted(context);

        // Assert
        Assert.Collection(
            context.Results.SelectMany(r => r.SupportedResponseTypes).OrderBy(r => r.StatusCode),
            responseType =>
            {
                Assert.Equal(typeof(InferredJsonClass), responseType.Type);
                Assert.Equal(200, responseType.StatusCode);
                Assert.Equal(new[] { "application/json" }, GetSortedMediaTypes(responseType));
            },
            responseType =>
            {
                Assert.Equal(typeof(InferredJsonClass), responseType.Type);
                Assert.Equal(201, responseType.StatusCode);
                Assert.Equal(new[] { "application/json" }, GetSortedMediaTypes(responseType));
            });
    }

    [Fact]
    public void HandleAcceptsMetadata()
    {
        // Arrange
        var builder = CreateBuilder();
        builder.MapPost("/api/todos", () => "")
            .Accepts<string>("application/json", "application/xml");
        var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

        var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
        var hostEnvironment = new HostEnvironment
        {
            ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest)
        };
        var provider = CreateEndpointMetadataApiDescriptionProvider(endpointDataSource);

        // Act
        provider.OnProvidersExecuting(context);
        provider.OnProvidersExecuted(context);

        // Assert
        Assert.Collection(
            context.Results.SelectMany(r => r.SupportedRequestFormats),
            requestType =>
            {
                Assert.Equal("application/json", requestType.MediaType);
            },
            requestType =>
            {
                Assert.Equal("application/xml", requestType.MediaType);
            });
    }

    [Fact]
    public void HandleAcceptsMetadataWithTypeParameter()
    {
        // Arrange
        var builder = new TestEndpointRouteBuilder(new ApplicationBuilder(null));
        builder.MapPost("/api/todos", (InferredJsonClass inferredJsonClass) => "")
            .Accepts(typeof(InferredJsonClass), "application/json");
        var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

        var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
        var hostEnvironment = new HostEnvironment
        {
            ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest)
        };
        var provider = CreateEndpointMetadataApiDescriptionProvider(endpointDataSource);

        // Act
        provider.OnProvidersExecuting(context);
        provider.OnProvidersExecuted(context);

        // Assert
        var parameterDescriptions = context.Results.SelectMany(r => r.ParameterDescriptions);
        var bodyParameterDescription = parameterDescriptions.Single();
        Assert.Equal(typeof(InferredJsonClass), bodyParameterDescription.Type);
        Assert.Equal("inferredJsonClass", bodyParameterDescription.Name);
        Assert.False(bodyParameterDescription.IsRequired);
    }

    [Fact]
    public void FavorsProducesMetadataOverAttribute()
    {
        // Arrange
        var builder = CreateBuilder();
        builder.MapGet("/api/todos", [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]() => "")
            .Produces<InferredJsonClass>(StatusCodes.Status200OK);
        var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

        var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
        var hostEnvironment = new HostEnvironment
        {
            ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest)
        };
        var provider = CreateEndpointMetadataApiDescriptionProvider(endpointDataSource);

        // Act
        provider.OnProvidersExecuting(context);
        provider.OnProvidersExecuted(context);

        // Assert
        Assert.Collection(
            context.Results.SelectMany(r => r.SupportedResponseTypes).OrderBy(r => r.StatusCode),
            responseType =>
            {
                Assert.Equal(typeof(InferredJsonClass), responseType.Type);
                Assert.Equal(200, responseType.StatusCode);
                Assert.Equal(new[] { "application/json" }, GetSortedMediaTypes(responseType));
            });
    }

#nullable enable

    [Fact]
    public void HandleDefaultIAcceptsMetadataForRequiredBodyParameter()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var builder = new TestEndpointRouteBuilder(new ApplicationBuilder(serviceProvider));
        builder.MapPost("/api/todos", (InferredJsonClass inferredJsonClass) => "");
        var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

        var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
        var hostEnvironment = new HostEnvironment
        {
            ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest)
        };
        var provider = CreateEndpointMetadataApiDescriptionProvider(endpointDataSource);

        // Act
        provider.OnProvidersExecuting(context);
        provider.OnProvidersExecuted(context);

        // Assert
        var parameterDescriptions = context.Results.SelectMany(r => r.ParameterDescriptions);
        var bodyParameterDescription = parameterDescriptions.Single();
        Assert.Equal(typeof(InferredJsonClass), bodyParameterDescription.Type);
        Assert.Equal("inferredJsonClass", bodyParameterDescription.Name);
        Assert.True(bodyParameterDescription.IsRequired);

        // Assert
        var requestFormats = context.Results.SelectMany(r => r.SupportedRequestFormats);
        var defaultRequestFormat = requestFormats.Single();
        Assert.Equal("application/json", defaultRequestFormat.MediaType);
    }

    [Fact]
    public void HandleDefaultIAcceptsMetadataForOptionalBodyParameter()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var builder = new TestEndpointRouteBuilder(new ApplicationBuilder(serviceProvider));
        builder.MapPost("/api/todos", (InferredJsonClass? inferredJsonClass) => "");
        var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

        var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
        var hostEnvironment = new HostEnvironment
        {
            ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest)
        };
        var provider = CreateEndpointMetadataApiDescriptionProvider(endpointDataSource);

        // Act
        provider.OnProvidersExecuting(context);
        provider.OnProvidersExecuted(context);

        // Assert
        var parameterDescriptions = context.Results.SelectMany(r => r.ParameterDescriptions);
        var bodyParameterDescription = parameterDescriptions.Single();
        Assert.Equal(typeof(InferredJsonClass), bodyParameterDescription.Type);
        Assert.Equal("inferredJsonClass", bodyParameterDescription.Name);
        Assert.False(bodyParameterDescription.IsRequired);

        // Assert
        var requestFormats = context.Results.SelectMany(r => r.SupportedRequestFormats);
        var defaultRequestFormat = requestFormats.Single();
        Assert.Equal("application/json", defaultRequestFormat.MediaType);
    }

    [Fact]
    public void HandleIAcceptsMetadataWithConsumesAttributeAndInferredOptionalFromBodyType()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var builder = new TestEndpointRouteBuilder(new ApplicationBuilder(serviceProvider));
        builder.MapPost("/api/todos", [Consumes("application/xml")] (InferredJsonClass? inferredJsonClass) => "");
        var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

        var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
        var hostEnvironment = new HostEnvironment
        {
            ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest)
        };
        var provider = CreateEndpointMetadataApiDescriptionProvider(endpointDataSource);

        // Act
        provider.OnProvidersExecuting(context);
        provider.OnProvidersExecuted(context);

        // Assert
        var parameterDescriptions = context.Results.SelectMany(r => r.ParameterDescriptions);
        var bodyParameterDescription = parameterDescriptions.Single();
        Assert.Equal(typeof(InferredJsonClass), bodyParameterDescription.Type);
        Assert.Equal("inferredJsonClass", bodyParameterDescription.Name);
        Assert.False(bodyParameterDescription.IsRequired);

        // Assert
        var requestFormats = context.Results.SelectMany(r => r.SupportedRequestFormats);
        var defaultRequestFormat = requestFormats.Single();
        Assert.Equal("application/xml", defaultRequestFormat.MediaType);
    }

    [Fact]
    public void HandleDefaultIAcceptsMetadataForRequiredFormFileParameter()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var builder = new TestEndpointRouteBuilder(new ApplicationBuilder(serviceProvider));
        builder.MapPost("/file/upload", (IFormFile formFile) => "");
        var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

        var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
        var provider = CreateEndpointMetadataApiDescriptionProvider(endpointDataSource);

        // Act
        provider.OnProvidersExecuting(context);
        provider.OnProvidersExecuted(context);

        // Assert
        var parameterDescriptions = context.Results.SelectMany(r => r.ParameterDescriptions);
        var bodyParameterDescription = parameterDescriptions.Single();
        Assert.Equal(typeof(IFormFile), bodyParameterDescription.Type);
        Assert.Equal("formFile", bodyParameterDescription.Name);
        Assert.True(bodyParameterDescription.IsRequired);

        // Assert
        var requestFormats = context.Results.SelectMany(r => r.SupportedRequestFormats);
        var defaultRequestFormat = requestFormats.Single();
        Assert.Equal("multipart/form-data", defaultRequestFormat.MediaType);
        Assert.Null(defaultRequestFormat.Formatter);
    }

    [Fact]
    public void HandleDefaultIAcceptsMetadataForOptionalFormFileParameter()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var builder = new TestEndpointRouteBuilder(new ApplicationBuilder(serviceProvider));
        builder.MapPost("/file/upload", (IFormFile? inferredFormFile) => "");
        var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

        var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
        var provider = CreateEndpointMetadataApiDescriptionProvider(endpointDataSource);

        // Act
        provider.OnProvidersExecuting(context);
        provider.OnProvidersExecuted(context);

        // Assert
        var parameterDescriptions = context.Results.SelectMany(r => r.ParameterDescriptions);
        var bodyParameterDescription = parameterDescriptions.Single();
        Assert.Equal(typeof(IFormFile), bodyParameterDescription.Type);
        Assert.Equal("inferredFormFile", bodyParameterDescription.Name);
        Assert.False(bodyParameterDescription.IsRequired);

        // Assert
        var requestFormats = context.Results.SelectMany(r => r.SupportedRequestFormats);
        var defaultRequestFormat = requestFormats.Single();
        Assert.Equal("multipart/form-data", defaultRequestFormat.MediaType);
        Assert.Null(defaultRequestFormat.Formatter);
    }

    [Fact]
    public void AddsMultipartFormDataResponseFormatWhenFormFileSpecified()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var builder = new TestEndpointRouteBuilder(new ApplicationBuilder(serviceProvider));
        builder.MapPost("/file/upload", (IFormFile file) => Results.NoContent());
        var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

        var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
        var provider = CreateEndpointMetadataApiDescriptionProvider(endpointDataSource);

        // Act
        provider.OnProvidersExecuting(context);
        provider.OnProvidersExecuted(context);

        // Assert
        var parameterDescriptions = context.Results.SelectMany(r => r.ParameterDescriptions);
        var bodyParameterDescription = parameterDescriptions.Single();
        Assert.Equal(typeof(IFormFile), bodyParameterDescription.Type);
        Assert.Equal("file", bodyParameterDescription.Name);
        Assert.True(bodyParameterDescription.IsRequired);

        // Assert
        var requestFormats = context.Results.SelectMany(r => r.SupportedRequestFormats);
        var defaultRequestFormat = requestFormats.Single();
        Assert.Equal("multipart/form-data", defaultRequestFormat.MediaType);
        Assert.Null(defaultRequestFormat.Formatter);
    }

    [Fact]
    public void HasMultipleRequestFormatsWhenFormFileSpecifiedWithConsumedAttribute()
    {
        var apiDescription = GetApiDescription(
            [Consumes("application/custom0", "application/custom1")] (IFormFile file) => Results.NoContent());

        Assert.Equal(2, apiDescription.SupportedRequestFormats.Count);

        var requestFormat0 = apiDescription.SupportedRequestFormats[0];
        Assert.Equal("application/custom0", requestFormat0.MediaType);
        Assert.Null(requestFormat0.Formatter);

        var requestFormat1 = apiDescription.SupportedRequestFormats[1];
        Assert.Equal("application/custom1", requestFormat1.MediaType);
        Assert.Null(requestFormat1.Formatter);
    }

    [Fact]
    public void TestIsRequiredFromFormFile()
    {
        var apiDescription0 = GetApiDescription((IFormFile fromFile) => { });
        var apiDescription1 = GetApiDescription((IFormFile? fromFile) => { });
        Assert.Equal(1, apiDescription0.ParameterDescriptions.Count);
        Assert.Equal(1, apiDescription1.ParameterDescriptions.Count);

        var fromFileParam0 = apiDescription0.ParameterDescriptions[0];
        Assert.Equal(typeof(IFormFile), fromFileParam0.Type);
        Assert.Equal(typeof(IFormFile), fromFileParam0.ModelMetadata.ModelType);
        Assert.Equal(BindingSource.FormFile, fromFileParam0.Source);
        Assert.True(fromFileParam0.IsRequired);

        var fromFileParam1 = apiDescription1.ParameterDescriptions[0];
        Assert.Equal(typeof(IFormFile), fromFileParam1.Type);
        Assert.Equal(typeof(IFormFile), fromFileParam1.ModelMetadata.ModelType);
        Assert.Equal(BindingSource.FormFile, fromFileParam1.Source);
        Assert.False(fromFileParam1.IsRequired);
    }

    [Fact]
    public void AddsFromFormParameterAsFormFile()
    {
        static void AssertFormFileParameter(ApiDescription apiDescription, Type expectedType, string expectedName)
        {
            var param = Assert.Single(apiDescription.ParameterDescriptions);
            Assert.Equal(expectedType, param.Type);
            Assert.Equal(expectedType, param.ModelMetadata.ModelType);
            Assert.Equal(BindingSource.FormFile, param.Source);
            Assert.Equal(expectedName, param.Name);
        }

        AssertFormFileParameter(GetApiDescription((IFormFile file) => { }), typeof(IFormFile), "file");
        AssertFormFileParameter(GetApiDescription(([FromForm(Name = "file_name")] IFormFile file) => { }), typeof(IFormFile), "file_name");
    }

    [Fact]
    public void AddsMultipartFormDataResponseFormatWhenFormFileCollectionSpecified()
    {
        AssertFormFileCollection((IFormFileCollection files) => Results.NoContent(), "files");
        AssertFormFileCollection(([FromForm] IFormFileCollection uploads) => Results.NoContent(), "uploads");

        static void AssertFormFileCollection(Delegate handler, string expectedName)
        {
            // Arrange
            var services = new ServiceCollection();
            var serviceProvider = services.BuildServiceProvider();
            var builder = new TestEndpointRouteBuilder(new ApplicationBuilder(serviceProvider));
            builder.MapPost("/file/upload", handler);
            var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

            var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
            var provider = CreateEndpointMetadataApiDescriptionProvider(endpointDataSource);

            // Act
            provider.OnProvidersExecuting(context);
            provider.OnProvidersExecuted(context);

            // Assert
            var parameterDescriptions = context.Results.SelectMany(r => r.ParameterDescriptions);
            var bodyParameterDescription = parameterDescriptions.Single();
            Assert.Equal(typeof(IFormFileCollection), bodyParameterDescription.Type);
            Assert.Equal(expectedName, bodyParameterDescription.Name);
            Assert.True(bodyParameterDescription.IsRequired);

            var requestFormats = context.Results.SelectMany(r => r.SupportedRequestFormats);
            var defaultRequestFormat = requestFormats.Single();
            Assert.Equal("multipart/form-data", defaultRequestFormat.MediaType);
            Assert.Null(defaultRequestFormat.Formatter);
        }
    }

#nullable restore

    [Fact]
    public void ProducesRouteInfoOnlyForRouteParameters()
    {
        var builder = CreateBuilder();
        string GetName(int fromQuery, string name = "default") => $"Hello {name}!";
        builder.MapGet("/api/todos/{name}", GetName);
        var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

        var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
        var hostEnvironment = new HostEnvironment
        {
            ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest)
        };
        var provider = new EndpointMetadataApiDescriptionProvider(
            endpointDataSource,
            hostEnvironment,
            new DefaultParameterPolicyFactory(Options.Create(new RouteOptions()), new TestServiceProvider()),
            new ServiceProviderIsService());

        // Act
        provider.OnProvidersExecuting(context);

        // Assert
        var apiDescription = Assert.Single(context.Results);
        Assert.Collection(apiDescription.ParameterDescriptions,
            parameter =>
            {
                Assert.Equal("fromQuery", parameter.Name);
                Assert.Null(parameter.RouteInfo);
            },
            parameter =>
            {
                Assert.Equal("name", parameter.Name);
                Assert.NotNull(parameter.RouteInfo);
                Assert.Empty(parameter.RouteInfo!.Constraints);
                Assert.True(parameter.RouteInfo!.IsOptional);
                Assert.Equal("default", parameter.RouteInfo!.DefaultValue);
            });
    }

    [Fact]
    public void HandlesEndpointWithRouteConstraints()
    {
        var builder = CreateBuilder();
        builder.MapGet("/api/todos/{name:minlength(8):guid:maxlength(20)}", (string name) => "");
        var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

        var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
        var hostEnvironment = new HostEnvironment
        {
            ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest)
        };
        var provider = CreateEndpointMetadataApiDescriptionProvider(endpointDataSource);

        // Act
        provider.OnProvidersExecuting(context);

        // Assert
        var apiDescription = Assert.Single(context.Results);
        var parameter = Assert.Single(apiDescription.ParameterDescriptions);
        Assert.NotNull(parameter.RouteInfo);
        Assert.Collection(parameter.RouteInfo!.Constraints,
            constraint => Assert.IsType<MinLengthRouteConstraint>(constraint),
            constraint => Assert.IsType<GuidRouteConstraint>(constraint),
            constraint => Assert.IsType<MaxLengthRouteConstraint>(constraint));
    }

    [Fact]
    public void HandlesEndpointWithDescriptionAndSummary_WithExtensionMethods()
    {
        var builder = CreateBuilder();
        builder.MapGet("/api/todos/{id}", (int id) => "").WithDescription("A description").WithSummary("A summary");

        var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

        var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
        var hostEnvironment = new HostEnvironment
        {
            ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest)
        };
        var provider = CreateEndpointMetadataApiDescriptionProvider(endpointDataSource);

        // Act
        provider.OnProvidersExecuting(context);

        // Assert
        var apiDescription = Assert.Single(context.Results);
        Assert.NotEmpty(apiDescription.ActionDescriptor.EndpointMetadata);

        var descriptionMetadata = apiDescription.ActionDescriptor.EndpointMetadata.OfType<IDescriptionMetadata>().SingleOrDefault();
        Assert.NotNull(descriptionMetadata);
        Assert.Equal("A description", descriptionMetadata.Description);

        var summaryMetadata = apiDescription.ActionDescriptor.EndpointMetadata.OfType<ISummaryMetadata>().SingleOrDefault();
        Assert.NotNull(summaryMetadata);
        Assert.Equal("A summary", summaryMetadata.Summary);
    }

    [Fact]
    public void HandlesEndpointWithDescriptionAndSummary_WithAttributes()
    {
        var builder = CreateBuilder();
        builder.MapGet("/api/todos/{id}", [Summary("A summary")] [Http.Description("A description")] (int id) => "");

        var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

        var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
        var hostEnvironment = new HostEnvironment
        {
            ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest)
        };
        var provider = CreateEndpointMetadataApiDescriptionProvider(endpointDataSource);

        // Act
        provider.OnProvidersExecuting(context);

        // Assert
        var apiDescription = Assert.Single(context.Results);
        Assert.NotEmpty(apiDescription.ActionDescriptor.EndpointMetadata);

        var descriptionMetadata = apiDescription.ActionDescriptor.EndpointMetadata.OfType<IDescriptionMetadata>().SingleOrDefault();
        Assert.NotNull(descriptionMetadata);
        Assert.Equal("A description", descriptionMetadata.Description);

        var summaryMetadata = apiDescription.ActionDescriptor.EndpointMetadata.OfType<ISummaryMetadata>().SingleOrDefault();
        Assert.NotNull(summaryMetadata);
        Assert.Equal("A summary", summaryMetadata.Summary);
    }

    [Fact]
    public void HandlesResponseWithDescription_ViaExtensionMethod()
    {
        var builder = CreateBuilder();
        builder.MapGet("/api/todos/{id}", (int id) => "").Produces(StatusCodes.Status200OK, "This is a response description", typeof(Todo));

        var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

        var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
        var hostEnvironment = new HostEnvironment
        {
            ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest)
        };
        var provider = CreateEndpointMetadataApiDescriptionProvider(endpointDataSource);

        // Act
        provider.OnProvidersExecuting(context);

        // Assert
        var apiDescription = Assert.Single(context.Results);
        var apiResponse = Assert.Single(apiDescription.SupportedResponseTypes);
        Assert.Equal("This is a response description", apiResponse.Description);
    }

    [Fact]
    public void HandlesParameterWithDescription_ViaAttributes()
    {
        var builder = CreateBuilder();
        builder.MapGet("/api/todos/{id}", ([Http.Description("A description for the parameter")] int id) => "");

        var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

        var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
        var hostEnvironment = new HostEnvironment
        {
            ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest)
        };
        var provider = CreateEndpointMetadataApiDescriptionProvider(endpointDataSource);

        // Act
        provider.OnProvidersExecuting(context);

        // Assert
        var apiDescription = Assert.Single(context.Results);
        var apiParameterDescription = Assert.Single(apiDescription.ParameterDescriptions);
        Assert.Equal("A description for the parameter", apiParameterDescription.Description);
    }

    [Fact]
    public void HandleRequestBodyWithExample_ViaExtensionMethod()
    {
        var builder = CreateBuilder();
        builder
            .MapPost("/api/todos", (Todo todo) => "")
            .WithParameterExample("todo", "A todo", "A quick description of the todo", new Todo() { Id = 0, Title = "foo" });

        var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

        var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
        var hostEnvironment = new HostEnvironment
        {
            ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest)
        };
        var provider = CreateEndpointMetadataApiDescriptionProvider(endpointDataSource);

        // Act
        provider.OnProvidersExecuting(context);

        // Assert
        var apiDescription = Assert.Single(context.Results);
        var parameter = Assert.Single(apiDescription.ParameterDescriptions);
        var example = Assert.Single(parameter.Examples);
        Assert.Equal("A quick description of the todo", example.Description);
        Assert.Equal("A todo", example.Summary);
        var todo = Assert.IsType<Todo>(example.Value);
        Assert.Equal(0, todo.Id);
    }

    [Fact]
    public void CanHandleParameterWithExample_WithAttribute()
    {
        var builder = CreateBuilder();
        builder.MapPost("/api/todos", (
            [Example("A number", "A detailed description of number", 2)]
            int id,
            [Example("A bool", "A detailed description of bool", true)]
            bool name
        ) => "");

        var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

        var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
        var hostEnvironment = new HostEnvironment
        {
            ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest)
        };
        var provider = CreateEndpointMetadataApiDescriptionProvider(endpointDataSource);

        // Act
        provider.OnProvidersExecuting(context);

        // Assert
        var apiDescription = Assert.Single(context.Results);
        Assert.Collection(apiDescription.ParameterDescriptions,
            intParam =>
            {
                var example = Assert.Single(intParam.Examples);
                Assert.Equal("A detailed description of number", example.Description);
                Assert.Equal("A number", example.Summary);
                var value = Assert.IsType<int>(example.Value);
                Assert.Equal(2, value);
            },
            boolParam =>
            {
                var example = Assert.Single(boolParam.Examples);
                Assert.Equal("A detailed description of bool", example.Description);
                Assert.Equal("A bool", example.Summary);
                var value = Assert.IsType<bool>(example.Value);
                Assert.True(value);
            }
        );
    }

    [Fact]
    public void CanHandleParameterWithExample_ExtensionMethod()
    {
        var date = new DateTime();
        var guid = new Guid();
        var builder = CreateBuilder();
        builder.MapPost("/api/todos", (DateTime startDate, Guid id) => "")
            .WithParameterExample("startDate", "A date", "A description of a date", date)
            .WithParameterExample("id", "An ID", "A GUID", guid);

        var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

        var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
        var hostEnvironment = new HostEnvironment
        {
            ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest)
        };
        var provider = CreateEndpointMetadataApiDescriptionProvider(endpointDataSource);

        // Act
        provider.OnProvidersExecuting(context);

        // Assert
        var apiDescription = Assert.Single(context.Results);
        Assert.Collection(apiDescription.ParameterDescriptions,
            intParam =>
            {
                var example = Assert.Single(intParam.Examples);
                Assert.Equal("A date", example.Summary);
                Assert.Equal("A description of a date", example.Description);
                var value = Assert.IsType<DateTime>(example.Value);
                Assert.Equal(date, value);
            },
            boolParam =>
            {
                var example = Assert.Single(boolParam.Examples);
                Assert.Equal("An ID", example.Summary);
                Assert.Equal("A GUID", example.Description);
                var value = Assert.IsType<Guid>(example.Value);
                Assert.Equal(guid, value);
            }
        );
    }

    [Fact]
    public void CanHandleResponseWithExample()
    {
        var builder = CreateBuilder();
        builder.MapGet("/api/todos", (int id) => "").WithResponseExample("A todo", "A todo description", new Todo() { Id = 1, Title = "foo" });

        var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

        var endpointDataSource = builder.DataSources.OfType<EndpointDataSource>().Single();
        var hostEnvironment = new HostEnvironment
        {
            ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest)
        };
        var provider = CreateEndpointMetadataApiDescriptionProvider(endpointDataSource);

        // Act
        provider.OnProvidersExecuting(context);

        // Assert
        var apiDescription = Assert.Single(context.Results);
        var response = Assert.Single(apiDescription.SupportedResponseTypes);
        var example = Assert.Single(response.Examples);
        Assert.Equal("A todo description", example.Description);
        Assert.Equal("A todo", example.Summary);
        var todo = Assert.IsType<Todo>(example.Value);
        Assert.Equal(1, todo.Id);
    }

    private static IEnumerable<string> GetSortedMediaTypes(ApiResponseType apiResponseType)
    {
        return apiResponseType.ApiResponseFormats
            .OrderBy(format => format.MediaType)
            .Select(format => format.MediaType);
    }

    private static IList<ApiDescription> GetApiDescriptions(
        Delegate action,
        string pattern = null,
        IEnumerable<string> httpMethods = null,
        string displayName = null)
    {
        var methodInfo = action.Method;
        var attributes = methodInfo.GetCustomAttributes();
        var context = new ApiDescriptionProviderContext(Array.Empty<ActionDescriptor>());

        var httpMethodMetadata = new HttpMethodMetadata(httpMethods ?? new[] { "GET" });
        var metadataItems = new List<object>(attributes) { methodInfo, httpMethodMetadata };
        var endpointMetadata = new EndpointMetadataCollection(metadataItems.ToArray());
        var routePattern = RoutePatternFactory.Parse(pattern ?? "/");

        var endpoint = new RouteEndpoint(httpContext => Task.CompletedTask, routePattern, 0, endpointMetadata, displayName);
        var endpointDataSource = new DefaultEndpointDataSource(endpoint);

        var provider = CreateEndpointMetadataApiDescriptionProvider(endpointDataSource);

        provider.OnProvidersExecuting(context);
        provider.OnProvidersExecuted(context);

        return context.Results;
    }

    private static EndpointMetadataApiDescriptionProvider CreateEndpointMetadataApiDescriptionProvider(EndpointDataSource endpointDataSource) => new EndpointMetadataApiDescriptionProvider(
            endpointDataSource,
            new HostEnvironment { ApplicationName = nameof(EndpointMetadataApiDescriptionProviderTest) },
            new DefaultParameterPolicyFactory(Options.Create(new RouteOptions()), new TestServiceProvider()),
            new ServiceProviderIsService());

    private static TestEndpointRouteBuilder CreateBuilder() =>
        new TestEndpointRouteBuilder(new ApplicationBuilder(new TestServiceProvider()));

    private static ApiDescription GetApiDescription(Delegate action, string pattern = null, string displayName = null) =>
        Assert.Single(GetApiDescriptions(action, pattern, displayName: displayName));

    private static void TestAction()
    {
    }

    private static void TestActionWithDefaultValue(int foo = 42)
    {
    }

    private class InferredJsonClass
    {
    }

    private interface IInferredServiceInterface
    {
    }

    private interface IInferredJsonInterface
    {
    }

    private class Todo
    {
        public int Id { get; set; }
        public string Title { get; set; }
    }

    private class ServiceProviderIsService : IServiceProviderIsService
    {
        public bool IsService(Type serviceType) => serviceType == typeof(IInferredServiceInterface);
    }

    private class HostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; }
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
    }

    private class TestEndpointRouteBuilder : IEndpointRouteBuilder
    {
        public TestEndpointRouteBuilder(IApplicationBuilder applicationBuilder)
        {
            ApplicationBuilder = applicationBuilder ?? throw new ArgumentNullException(nameof(applicationBuilder));
            DataSources = new List<EndpointDataSource>();
        }

        public IApplicationBuilder ApplicationBuilder { get; }

        public IApplicationBuilder CreateApplicationBuilder() => ApplicationBuilder.New();

        public ICollection<EndpointDataSource> DataSources { get; }

        public IServiceProvider ServiceProvider => ApplicationBuilder.ApplicationServices;
    }

    private record TryParseStringRecord(int Value)
    {
        public static bool TryParse(string value, out TryParseStringRecord result) =>
            throw new NotImplementedException();
    }

    private record struct TryParseStringRecordStruct(int Value)
    {
        public static bool TryParse(string value, out TryParseStringRecordStruct result) =>
            throw new NotImplementedException();
    }

    private record BindAsyncRecord(int Value)
    {
        public static ValueTask<BindAsyncRecord> BindAsync(HttpContext context, ParameterInfo parameter) =>
            throw new NotImplementedException();
        public static bool TryParse(string value, out BindAsyncRecord result) =>
            throw new NotImplementedException();
    }

    private class TestServiceProvider : IServiceProvider
    {
        public void Dispose()
        {
        }

        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(IOptions<RouteHandlerOptions>))
            {
                return Options.Create(new RouteHandlerOptions());
            }

            return null;
        }
    }
}
