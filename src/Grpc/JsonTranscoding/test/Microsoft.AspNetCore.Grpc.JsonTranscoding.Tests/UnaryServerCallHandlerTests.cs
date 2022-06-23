// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Google.Api;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;
using Grpc.AspNetCore.Server;
using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Shared;
using Grpc.Shared.Server;
using Grpc.Tests.Shared;
using Microsoft.AspNetCore.Grpc.JsonTranscoding.Internal.CallHandlers;
using Microsoft.AspNetCore.Grpc.JsonTranscoding.Internal.Json;
using Microsoft.AspNetCore.Grpc.JsonTranscoding.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Testing;
using Microsoft.Extensions.Primitives;
using Transcoding;
using Xunit.Abstractions;
using MethodOptions = Grpc.Shared.Server.MethodOptions;
using Type = System.Type;

namespace Microsoft.AspNetCore.Grpc.JsonTranscoding.Tests;

public class UnaryServerCallHandlerTests : LoggedTest
{
    public UnaryServerCallHandlerTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task HandleCallAsync_MatchingRouteValue_SetOnRequestMessage()
    {
        // Arrange
        HelloRequest? request = null;
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
        {
            request = r;
            return Task.FromResult(new HelloReply { Message = $"Hello {r.Name}" });
        };

        var routeParameterDescriptors = new Dictionary<string, List<FieldDescriptor>>
        {
            ["name"] = new List<FieldDescriptor>(new[] { HelloRequest.Descriptor.FindFieldByNumber(HelloRequest.NameFieldNumber) }),
            ["sub.subfield"] = new List<FieldDescriptor>(new[]
            {
                HelloRequest.Descriptor.FindFieldByNumber(HelloRequest.SubFieldNumber),
                HelloRequest.Types.SubMessage.Descriptor.FindFieldByNumber(HelloRequest.Types.SubMessage.SubfieldFieldNumber)
            })
        };
        var descriptorInfo = TestHelpers.CreateDescriptorInfo(routeParameterDescriptors: routeParameterDescriptors);
        var unaryServerCallHandler = CreateCallHandler(invoker, descriptorInfo: descriptorInfo);
        var httpContext = TestHelpers.CreateHttpContext();
        httpContext.Request.RouteValues["name"] = "TestName!";
        httpContext.Request.RouteValues["sub.subfield"] = "Subfield!";

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.Equal(200, httpContext.Response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", httpContext.Response.ContentType);

        Assert.NotNull(request);
        Assert.Equal("TestName!", request!.Name);
        Assert.Equal("Subfield!", request!.Sub.Subfield);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var responseJson = JsonDocument.Parse(httpContext.Response.Body);
        Assert.Equal("Hello TestName!", responseJson.RootElement.GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("TestName!")]
    [InlineData("")]
    public async Task HandleCallAsync_ResponseBodySet_ResponseReturned(string name)
    {
        // Arrange
        HelloRequest? request = null;
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
        {
            request = r;
            return Task.FromResult(new HelloReply { Message = r.Name });
        };

        var routeParameterDescriptors = new Dictionary<string, List<FieldDescriptor>>
        {
            ["name"] = new List<FieldDescriptor>(new[] { HelloRequest.Descriptor.FindFieldByNumber(HelloRequest.NameFieldNumber) })
        };
        var descriptorInfo = TestHelpers.CreateDescriptorInfo(
            responseBodyDescriptor: HelloReply.Descriptor.FindFieldByNumber(HelloReply.MessageFieldNumber),
            routeParameterDescriptors: routeParameterDescriptors);
        var unaryServerCallHandler = CreateCallHandler(
            invoker,
            descriptorInfo);
        var httpContext = TestHelpers.CreateHttpContext();
        httpContext.Request.RouteValues["name"] = name;

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.NotNull(request);
        Assert.Equal(name, request!.Name);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var responseJson = JsonDocument.Parse(httpContext.Response.Body);
        Assert.Equal(name, responseJson.RootElement.GetString());
    }

    [Fact]
    public async Task HandleCallAsync_NullProperty_ResponseReturned()
    {
        // Arrange
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
        {
            return Task.FromResult(new HelloReply { NullableMessage = null });
        };

        var routeParameterDescriptors = new Dictionary<string, List<FieldDescriptor>>
        {
            ["name"] = new List<FieldDescriptor>(new[] { HelloRequest.Descriptor.FindFieldByNumber(HelloRequest.NameFieldNumber) })
        };
        var descriptorInfo = TestHelpers.CreateDescriptorInfo(
            responseBodyDescriptor: HelloReply.Descriptor.FindFieldByNumber(HelloReply.NullableMessageFieldNumber),
            routeParameterDescriptors: routeParameterDescriptors);
        var unaryServerCallHandler = CreateCallHandler(
            invoker,
            descriptorInfo: descriptorInfo);
        var httpContext = TestHelpers.CreateHttpContext();
        httpContext.Request.RouteValues["name"] = "Doesn't matter";

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var sr = new StreamReader(httpContext.Response.Body);
        var content = sr.ReadToEnd();

        Assert.Equal("null", content);
    }

    [Fact]
    public async Task HandleCallAsync_ResponseBodySetToRepeatedField_ArrayReturned()
    {
        // Arrange
        HelloRequest? request = null;
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
        {
            request = r;
            return Task.FromResult(new HelloReply { Values = { "One", "Two", "" } });
        };

        var unaryServerCallHandler = CreateCallHandler(
            invoker,
            descriptorInfo: TestHelpers.CreateDescriptorInfo(responseBodyDescriptor: HelloReply.Descriptor.FindFieldByNumber(HelloReply.ValuesFieldNumber)));
        var httpContext = TestHelpers.CreateHttpContext();

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.NotNull(request);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var responseJson = JsonDocument.Parse(httpContext.Response.Body);
        Assert.Equal(JsonValueKind.Array, responseJson.RootElement.ValueKind);
        Assert.Equal("One", responseJson.RootElement[0].GetString());
        Assert.Equal("Two", responseJson.RootElement[1].GetString());
        Assert.Equal("", responseJson.RootElement[2].GetString());
    }

    [Fact]
    public async Task HandleCallAsync_RootBodySet_SetOnRequestMessage()
    {
        // Arrange
        HelloRequest? request = null;
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
        {
            request = r;
            return Task.FromResult(new HelloReply { Message = $"Hello {r.Name}" });
        };

        var unaryServerCallHandler = CreateCallHandler(
            invoker,
            descriptorInfo: TestHelpers.CreateDescriptorInfo(bodyDescriptor: HelloRequest.Descriptor));
        var httpContext = TestHelpers.CreateHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonFormatter.Default.Format(new HelloRequest
        {
            Name = "TestName!"
        })));
        httpContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["name"] = "QueryStringTestName!",
            ["sub.subfield"] = "QueryStringTestSubfield!"
        });
        httpContext.Request.ContentType = "application/json";

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.NotNull(request);
        Assert.Equal("TestName!", request!.Name);
        Assert.Null(request!.Sub);
    }

    [Fact]
    public async Task HandleCallAsync_SubBodySet_SetOnRequestMessage()
    {
        // Arrange
        HelloRequest? request = null;
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
        {
            request = r;
            return Task.FromResult(new HelloReply { Message = $"Hello {r.Name}" });
        };

        ServiceDescriptorHelpers.TryResolveDescriptors(HelloRequest.Descriptor, new[] { "sub" }, out var bodyFieldDescriptors);

        var descriptorInfo = TestHelpers.CreateDescriptorInfo(
            bodyDescriptor: HelloRequest.Types.SubMessage.Descriptor,
            bodyFieldDescriptors: bodyFieldDescriptors);
        var unaryServerCallHandler = CreateCallHandler(
            invoker,
            descriptorInfo);
        var httpContext = TestHelpers.CreateHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonFormatter.Default.Format(new HelloRequest.Types.SubMessage
        {
            Subfield = "Subfield!"
        })));
        httpContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["name"] = "QueryStringTestName!",
            ["sub.subfield"] = "QueryStringTestSubfield!",
            ["sub.subfields"] = "QueryStringTestSubfields!"
        });
        httpContext.Request.ContentType = "application/json";

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.NotNull(request);
        Assert.Equal("QueryStringTestName!", request!.Name);
        Assert.Equal("Subfield!", request!.Sub.Subfield);
        Assert.Empty(request!.Sub.Subfields);
    }

    [Fact]
    public async Task HandleCallAsync_SubRepeatedBodySet_SetOnRequestMessage()
    {
        // Arrange
        HelloRequest? request = null;
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
        {
            request = r;
            return Task.FromResult(new HelloReply { Message = $"Hello {r.Name}" });
        };

        ServiceDescriptorHelpers.TryResolveDescriptors(HelloRequest.Descriptor, new[] { "repeated_strings" }, out var bodyFieldDescriptors);

        var descriptorInfo = TestHelpers.CreateDescriptorInfo(
            bodyDescriptor: HelloRequest.Types.SubMessage.Descriptor,
            bodyDescriptorRepeated: true,
            bodyFieldDescriptors: bodyFieldDescriptors);
        var unaryServerCallHandler = CreateCallHandler(
            invoker,
            descriptorInfo);
        var httpContext = TestHelpers.CreateHttpContext();

        var sdf = new RepeatedField<string>
        {
            "One",
            "Two",
            "Three"
        };

        var sw = new StringWriter();
        JsonFormatter.Default.WriteValue(sw, sdf);

        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(sw.ToString()));
        httpContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["name"] = "QueryStringTestName!",
            ["sub.subfield"] = "QueryStringTestSubfield!",
            ["sub.subfields"] = "QueryStringTestSubfields!"
        });
        httpContext.Request.ContentType = "application/json";

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.NotNull(request);
        Assert.Equal("QueryStringTestName!", request!.Name);
        Assert.Equal("QueryStringTestSubfield!", request!.Sub.Subfield);
        Assert.Equal(3, request!.RepeatedStrings.Count);
        Assert.Equal("One", request!.RepeatedStrings[0]);
        Assert.Equal("Two", request!.RepeatedStrings[1]);
        Assert.Equal("Three", request!.RepeatedStrings[2]);
    }

    [Fact]
    public async Task HandleCallAsync_SubSubRepeatedBodySet_SetOnRequestMessage()
    {
        // Arrange
        HelloRequest? request = null;
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
        {
            request = r;
            return Task.FromResult(new HelloReply { Message = $"Hello {r.Name}" });
        };

        ServiceDescriptorHelpers.TryResolveDescriptors(HelloRequest.Descriptor, new[] { "sub", "subfields" }, out var bodyFieldDescriptors);

        var descriptorInfo = TestHelpers.CreateDescriptorInfo(
            bodyDescriptor: HelloRequest.Types.SubMessage.Descriptor,
            bodyDescriptorRepeated: true,
            bodyFieldDescriptors: bodyFieldDescriptors);
        var unaryServerCallHandler = CreateCallHandler(
            invoker,
            descriptorInfo);
        var httpContext = TestHelpers.CreateHttpContext();

        var sdf = new RepeatedField<string>
        {
            "One",
            "Two",
            "Three"
        };

        var sw = new StringWriter();
        JsonFormatter.Default.WriteValue(sw, sdf);

        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(sw.ToString()));
        httpContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["name"] = "QueryStringTestName!",
            ["sub.subfield"] = "QueryStringTestSubfield!" // Not bound because query can't be applied to fields that are covered by body
        });
        httpContext.Request.ContentType = "application/json";

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.NotNull(request);
        Assert.Equal("QueryStringTestName!", request!.Name);
        Assert.Equal("QueryStringTestSubfield!", request!.Sub.Subfield);
        Assert.Equal(3, request!.Sub.Subfields.Count);
    }

    [Fact]
    public async Task HandleCallAsync_MatchingQueryStringValues_SetOnRequestMessage()
    {
        // Arrange
        HelloRequest? request = null;
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
        {
            request = r;
            return Task.FromResult(new HelloReply());
        };

        var unaryServerCallHandler = CreateCallHandler(invoker);
        var httpContext = TestHelpers.CreateHttpContext();
        httpContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["name"] = "TestName!",
            ["sub.subfield"] = "TestSubfield!"
        });

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.NotNull(request);
        Assert.Equal("TestName!", request!.Name);
        Assert.Equal("TestSubfield!", request!.Sub.Subfield);
    }

    [Fact]
    public async Task HandleCallAsync_SuccessfulResponse_DefaultValuesInResponseJson()
    {
        // Arrange
        HelloRequest? request = null;
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
        {
            request = r;
            return Task.FromResult(new HelloReply());
        };

        var unaryServerCallHandler = CreateCallHandler(invoker);
        var httpContext = TestHelpers.CreateHttpContext();
        httpContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["name"] = "TestName!"
        });

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.NotNull(request);
        Assert.Equal("TestName!", request!.Name);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var responseJson = JsonDocument.Parse(httpContext.Response.Body);
        Assert.Equal("", responseJson.RootElement.GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("{malformed_json}", "Request JSON payload is not correctly formatted.")]
    [InlineData("[malformed_json]", "Request JSON payload is not correctly formatted.")]
    [InlineData("[1]", "Request JSON payload is not correctly formatted.")]
    [InlineData("1", "Request JSON payload is not correctly formatted.")]
    [InlineData("null", "Unable to deserialize null to HelloRequest.")]
    [InlineData("{\"name\": 1234}", "Request JSON payload is not correctly formatted.")]
    public async Task HandleCallAsync_MalformedRequestBody_BadRequestReturned(string json, string expectedError)
    {
        // Arrange
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
        {
            return Task.FromResult(new HelloReply());
        };

        var unaryServerCallHandler = CreateCallHandler(
            invoker,
            descriptorInfo: TestHelpers.CreateDescriptorInfo(bodyDescriptor: HelloRequest.Descriptor));
        var httpContext = TestHelpers.CreateHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        httpContext.Request.ContentType = "application/json";
        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.Equal(400, httpContext.Response.StatusCode);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var responseJson = JsonDocument.Parse(httpContext.Response.Body);
        Assert.Equal(expectedError, responseJson.RootElement.GetProperty("message").GetString());
        Assert.Equal(expectedError, responseJson.RootElement.GetProperty("error").GetString());
        Assert.Equal((int)StatusCode.InvalidArgument, responseJson.RootElement.GetProperty("code").GetInt32());
    }

    [Theory]
    [InlineData("{malformed_json}", "Request JSON payload is not correctly formatted.")]
    [InlineData("[malformed_json]", "Request JSON payload is not correctly formatted.")]
    [InlineData("1", "Request JSON payload is not correctly formatted.")]
    [InlineData("null", "Unable to deserialize null to List`1.")]
    [InlineData("{\"name\": 1234}", "Request JSON payload is not correctly formatted.")]
    public async Task HandleCallAsync_MalformedRequestBody_RepeatedBody_BadRequestReturned(string json, string expectedError)
    {
        // Arrange
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
        {
            return Task.FromResult(new HelloReply());
        };

        ServiceDescriptorHelpers.TryResolveDescriptors(HelloRequest.Descriptor, new[] { "repeated_strings" }, out var bodyFieldDescriptors);

        var descriptorInfo = TestHelpers.CreateDescriptorInfo(
            bodyDescriptor: HelloRequest.Types.SubMessage.Descriptor,
            bodyDescriptorRepeated: true,
            bodyFieldDescriptors: bodyFieldDescriptors);
        var unaryServerCallHandler = CreateCallHandler(
            invoker,
            descriptorInfo);
        var httpContext = TestHelpers.CreateHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        httpContext.Request.ContentType = "application/json";
        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.Equal(400, httpContext.Response.StatusCode);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var responseJson = JsonDocument.Parse(httpContext.Response.Body);
        Assert.Equal(expectedError, responseJson.RootElement.GetProperty("message").GetString());
        Assert.Equal(expectedError, responseJson.RootElement.GetProperty("error").GetString());
        Assert.Equal((int)StatusCode.InvalidArgument, responseJson.RootElement.GetProperty("code").GetInt32());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("text/html")]
    public async Task HandleCallAsync_BadContentType_BadRequestReturned(string contentType)
    {
        // Arrange
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
        {
            return Task.FromResult(new HelloReply());
        };

        var unaryServerCallHandler = CreateCallHandler(
            invoker,
            descriptorInfo: TestHelpers.CreateDescriptorInfo(bodyDescriptor: HelloRequest.Descriptor));
        var httpContext = TestHelpers.CreateHttpContext();
        httpContext.Request.Body = new MemoryStream("{}"u8.ToArray());
        httpContext.Request.ContentType = contentType;
        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.Equal(400, httpContext.Response.StatusCode);

        var expectedError = $"Unable to read the request as JSON because the request content type '{contentType}' is not a known JSON content type.";
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var responseJson = JsonDocument.Parse(httpContext.Response.Body);
        Assert.Equal(expectedError, responseJson.RootElement.GetProperty("message").GetString());
        Assert.Equal(expectedError, responseJson.RootElement.GetProperty("error").GetString());
        Assert.Equal((int)StatusCode.InvalidArgument, responseJson.RootElement.GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task HandleCallAsync_RpcExceptionReturned_StatusReturned()
    {
        // Arrange
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
        {
            return Task.FromException<HelloReply>(new RpcException(new Status(StatusCode.Unauthenticated, "Detail!"), "Message!"));
        };

        var unaryServerCallHandler = CreateCallHandler(invoker);
        var httpContext = TestHelpers.CreateHttpContext();

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.Equal(401, httpContext.Response.StatusCode);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var responseJson = JsonDocument.Parse(httpContext.Response.Body);
        Assert.Equal("Detail!", responseJson.RootElement.GetProperty("message").GetString());
        Assert.Equal("Detail!", responseJson.RootElement.GetProperty("error").GetString());
        Assert.Equal((int)StatusCode.Unauthenticated, responseJson.RootElement.GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task HandleCallAsync_RpcExceptionThrown_StatusReturned()
    {
        // Arrange
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Detail!"), "Message!");
        };

        var unaryServerCallHandler = CreateCallHandler(invoker);
        var httpContext = TestHelpers.CreateHttpContext();

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.Equal(401, httpContext.Response.StatusCode);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var responseJson = JsonDocument.Parse(httpContext.Response.Body);
        Assert.Equal("Detail!", responseJson.RootElement.GetProperty("message").GetString());
        Assert.Equal("Detail!", responseJson.RootElement.GetProperty("error").GetString());
        Assert.Equal((int)StatusCode.Unauthenticated, responseJson.RootElement.GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task HandleCallAsync_StatusSet_StatusReturned()
    {
        // Arrange
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
        {
            c.Status = new Status(StatusCode.Unauthenticated, "Detail!");
            return Task.FromResult(new HelloReply());
        };

        var unaryServerCallHandler = CreateCallHandler(invoker);
        var httpContext = TestHelpers.CreateHttpContext();

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.Equal(401, httpContext.Response.StatusCode);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var responseJson = JsonDocument.Parse(httpContext.Response.Body);
        Assert.Equal(@"Detail!", responseJson.RootElement.GetProperty("message").GetString());
        Assert.Equal(@"Detail!", responseJson.RootElement.GetProperty("error").GetString());
        Assert.Equal((int)StatusCode.Unauthenticated, responseJson.RootElement.GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task HandleCallAsync_HttpBodyRequest_RawRequestAvailable()
    {
        // Arrange
        string? requestContentType = null;
        byte[]? requestData = null;
        UnaryServerMethod<JsonTranscodingGreeterService, HttpBody, HelloReply> invoker = (s, r, c) =>
        {
            requestContentType = r.ContentType;
            requestData = r.Data.ToByteArray();

            var responseXml = XDocument.Load(new MemoryStream(requestData));
            var name = (string)responseXml.Element("name")!;

            return Task.FromResult(new HelloReply { Message = $"Hello {name}!" });
        };

        var unaryServerCallHandler = CreateCallHandler(
            invoker,
            CreateServiceMethod("HttpRequestBody", HttpBody.Parser, HelloReply.Parser),
            descriptorInfo: TestHelpers.CreateDescriptorInfo(bodyDescriptor: HttpBody.Descriptor));
        var requestContent = new XDocument(new XElement("name", "World")).ToString();

        var httpContext = TestHelpers.CreateHttpContext();
        httpContext.Request.ContentType = "application/xml";
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(requestContent));

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.Equal("application/xml", requestContentType);
        Assert.Equal(requestContent, Encoding.UTF8.GetString(requestData!));

        Assert.Equal(200, httpContext.Response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", httpContext.Response.ContentType);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var responseJson = JsonDocument.Parse(httpContext.Response.Body);
        Assert.Equal(@"Hello World!", responseJson.RootElement.GetProperty("message").GetString());
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(16 * 1024, false)]
    [InlineData(16 * 1024, true)]
    [InlineData(1024 * 1024, false)]
    [InlineData(1024 * 1024, true)]
    public async Task HandleCallAsync_HttpBodyRequestLarge_RawRequestAvailable(int requestSize, bool sendContentLength)
    {
        // Arrange
        string? requestContentType = null;
        byte[]? requestData = null;
        UnaryServerMethod<JsonTranscodingGreeterService, HttpBody, HelloReply> invoker = (s, r, c) =>
        {
            requestContentType = r.ContentType;
            requestData = r.Data.ToByteArray();

            return Task.FromResult(new HelloReply { Message = $"Hello {requestData.Length}!" });
        };

        var unaryServerCallHandler = CreateCallHandler(
            invoker,
            CreateServiceMethod("HttpRequestBody", HttpBody.Parser, HelloReply.Parser),
            descriptorInfo: TestHelpers.CreateDescriptorInfo(bodyDescriptor: HttpBody.Descriptor));

        var httpContext = TestHelpers.CreateHttpContext();
        httpContext.Request.ContentType = "application/octet-stream";

        var requestContent = new byte[requestSize];
        for (var i = 0; i < requestContent.Length; i++)
        {
            requestContent[i] = (byte)(i % 10);
        }
        httpContext.Request.Body = new MemoryStream(requestContent);
        if (sendContentLength)
        {
            httpContext.Request.ContentLength = requestSize;
        }

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.Equal("application/octet-stream", requestContentType);
        Assert.Equal(requestContent, requestData);

        Assert.Equal(200, httpContext.Response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", httpContext.Response.ContentType);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var responseJson = JsonDocument.Parse(httpContext.Response.Body);
        Assert.Equal($"Hello {requestContent.Length}!", responseJson.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task HandleCallAsync_NullBody_WrapperType_Error()
    {
        // Arrange
        UnaryServerMethod<JsonTranscodingGreeterService, Int32Value, HelloReply> invoker = (s, r, c) =>
        {
            return Task.FromResult(new HelloReply());
        };

        var unaryServerCallHandler = CreateCallHandler(
            invoker,
            CreateServiceMethod("Int32ValueBody", Int32Value.Parser, HelloReply.Parser),
            descriptorInfo: TestHelpers.CreateDescriptorInfo(bodyDescriptor: Int32Value.Descriptor));

        var httpContext = TestHelpers.CreateHttpContext();
        httpContext.Request.ContentType = "application/json";
        httpContext.Request.Body = new MemoryStream("null"u8.ToArray());

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var responseJson = JsonDocument.Parse(httpContext.Response.Body);
        Assert.Equal("Unable to deserialize null to Int32Value.", responseJson.RootElement.GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("null", null)]
    [InlineData("1", 1.0f)]
    [InlineData("1.1", 1.1f)]
    [InlineData(@"""NaN""", float.NaN)]
    public async Task HandleCallAsync_NestedWrapperType_Success(string requestJson, float? expectedValue)
    {
        // Arrange
        var tcs = new TaskCompletionSource<float?>(TaskCreationOptions.RunContinuationsAsynchronously);
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
        {
            tcs.SetResult(r.Wrappers.FloatValue);
            return Task.FromResult(new HelloReply());
        };

        Assert.True(ServiceDescriptorHelpers.TryResolveDescriptors(HelloRequest.Descriptor, new[] { "wrappers", "float_value" }, out var bodyFieldDescriptors));

        var descriptorInfo = TestHelpers.CreateDescriptorInfo(
            bodyDescriptor: FloatValue.Descriptor,
            bodyFieldDescriptors: bodyFieldDescriptors);
        var unaryServerCallHandler = CreateCallHandler(
            invoker,
            descriptorInfo);

        var httpContext = TestHelpers.CreateHttpContext();
        httpContext.Request.ContentType = "application/json";
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(requestJson));

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        var value = await tcs.Task.DefaultTimeout();
        Assert.Equal(expectedValue, value);
    }

    [Fact]
    public async Task HandleCallAsync_HttpBodyRequest_NoBody_RawRequestAvailable()
    {
        // Arrange
        string? requestContentType = null;
        byte[]? requestData = null;
        UnaryServerMethod<JsonTranscodingGreeterService, HttpBody, HelloReply> invoker = (s, r, c) =>
        {
            requestContentType = r.ContentType;
            requestData = r.Data.ToByteArray();

            return Task.FromResult(new HelloReply());
        };

        var unaryServerCallHandler = CreateCallHandler(
            invoker,
            CreateServiceMethod("HttpRequestBody", HttpBody.Parser, HelloReply.Parser),
            descriptorInfo: TestHelpers.CreateDescriptorInfo(bodyDescriptor: HttpBody.Descriptor));
        var requestContent = new XDocument(new XElement("name", "World")).ToString();

        var httpContext = TestHelpers.CreateHttpContext();

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.Equal("", requestContentType);
        Assert.Empty(requestData!);
    }

    [Fact]
    public async Task HandleCallAsync_SubHttpBodyRequest_RawRequestAvailable()
    {
        // Arrange
        HttpBodySubField? request = null;
        UnaryServerMethod<JsonTranscodingGreeterService, HttpBodySubField, HelloReply> invoker = (s, r, c) =>
        {
            request = r;
            return Task.FromResult(new HelloReply { Message = $"Hello {r.Name}" });
        };

        ServiceDescriptorHelpers.TryResolveDescriptors(HttpBodySubField.Descriptor, new[] { "sub" }, out var bodyFieldDescriptors);

        var descriptorInfo = TestHelpers.CreateDescriptorInfo(
            bodyDescriptor: HttpBody.Descriptor,
            bodyFieldDescriptors: bodyFieldDescriptors);
        var unaryServerCallHandler = CreateCallHandler(
            invoker,
            CreateServiceMethod("HttpRequestBody", HttpBodySubField.Parser, HelloReply.Parser),
            descriptorInfo);
        var requestContent = new XDocument(new XElement("name", "World")).ToString();

        var httpContext = TestHelpers.CreateHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(requestContent));
        httpContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["name"] = "QueryStringTestName!"
        });

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.NotNull(request);
        Assert.Equal("QueryStringTestName!", request!.Name);
        Assert.Equal("", request!.Sub.ContentType);
        Assert.Equal(requestContent, Encoding.UTF8.GetString(request!.Sub.Data.ToByteArray()));
    }

    [Fact]
    public async Task HandleCallAsync_NestedSubHttpBodyRequest_RawRequestAvailable()
    {
        // Arrange
        NestedHttpBodySubField? request = null;
        UnaryServerMethod<JsonTranscodingGreeterService, NestedHttpBodySubField, HelloReply> invoker = (s, r, c) =>
        {
            request = r;
            return Task.FromResult(new HelloReply { Message = $"Hello {r.Name}" });
        };

        ServiceDescriptorHelpers.TryResolveDescriptors(NestedHttpBodySubField.Descriptor, new[] { "sub", "sub" }, out var bodyFieldDescriptors);

        var descriptorInfo = TestHelpers.CreateDescriptorInfo(
            bodyDescriptor: HttpBody.Descriptor,
            bodyFieldDescriptors: bodyFieldDescriptors);
        var unaryServerCallHandler = CreateCallHandler(
            invoker,
            CreateServiceMethod("HttpRequestBody", NestedHttpBodySubField.Parser, HelloReply.Parser),
            descriptorInfo);
        var requestContent = new XDocument(new XElement("name", "World")).ToString();

        var httpContext = TestHelpers.CreateHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(requestContent));
        httpContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["name"] = "QueryStringTestName!",
            ["sub.name"] = "SubQueryStringTestName!"
        });

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.NotNull(request);
        Assert.Equal("QueryStringTestName!", request!.Name);
        Assert.Equal("SubQueryStringTestName!", request!.Sub.Name);
        Assert.Equal("", request!.Sub.Sub.ContentType);
        Assert.Equal(requestContent, Encoding.UTF8.GetString(request!.Sub.Sub.Data.ToByteArray()));
    }

    [Fact]
    public async Task HandleCallAsync_HttpBodyResponse_BodyReturned()
    {
        // Arrange
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HttpBody> invoker = (s, r, c) =>
        {
            return Task.FromResult(new HttpBody
            {
                ContentType = "application/xml",
                Data = ByteString.CopyFrom("<message>Hello world</message>"u8)
            });
        };

        var unaryServerCallHandler = CreateCallHandler(
            invoker,
            CreateServiceMethod("HttpResponseBody", HelloRequest.Parser, HttpBody.Parser));

        var httpContext = TestHelpers.CreateHttpContext();

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.Equal(200, httpContext.Response.StatusCode);
        Assert.Equal("application/xml", httpContext.Response.ContentType);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseXml = XDocument.Load(httpContext.Response.Body);
        Assert.Equal(@"Hello world", (string)responseXml.Element("message")!);
    }

    [Fact]
    public async Task HandleCallAsync_UserState_HttpContextInUserState()
    {
        object? requestHttpContext = null;

        // Arrange
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
        {
            c.UserState.TryGetValue("__HttpContext", out requestHttpContext);
            return Task.FromResult(new HelloReply());
        };

        var unaryServerCallHandler = CreateCallHandler(invoker);
        var httpContext = TestHelpers.CreateHttpContext();

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.Equal(httpContext, requestHttpContext);
    }

    [Fact]
    public async Task HandleCallAsync_HasInterceptor_InterceptorCalled()
    {
        object? interceptorRun = null;

        // Arrange
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
        {
            c.UserState.TryGetValue("IntercepterRun", out interceptorRun);
            return Task.FromResult(new HelloReply());
        };

        var interceptors = new List<(Type Type, object[] Args)>();
        interceptors.Add((typeof(TestInterceptor), Args: Array.Empty<object>()));

        var unaryServerCallHandler = CreateCallHandler(invoker, interceptors: interceptors);
        var httpContext = TestHelpers.CreateHttpContext();

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.True((bool)interceptorRun!);
    }

    public class TestInterceptor : Interceptor
    {
        public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
        {
            context.UserState["IntercepterRun"] = true;
            return base.UnaryServerHandler(request, context, continuation);
        }
    }

    [Fact]
    public async Task HandleCallAsync_GetHostAndMethodAndPeer_MatchHandler()
    {
        string? peer = null;
        string? host = null;
        string? method = null;

        // Arrange
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
        {
            peer = c.Peer;
            host = c.Host;
            method = c.Method;
            return Task.FromResult(new HelloReply());
        };

        var unaryServerCallHandler = CreateCallHandler(invoker);
        var httpContext = TestHelpers.CreateHttpContext();

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.Equal("ipv4:127.0.0.1:0", peer);
        Assert.Equal("localhost", host);
        Assert.Equal("/ServiceName/TestMethodName", method);
    }

    [Fact]
    public async Task HandleCallAsync_ExceptionThrown_StatusReturned()
    {
        // Arrange
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
        {
            throw new InvalidOperationException("Exception!");
        };

        var unaryServerCallHandler = CreateCallHandler(invoker);
        var httpContext = TestHelpers.CreateHttpContext();

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.Equal(500, httpContext.Response.StatusCode);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var responseJson = JsonDocument.Parse(httpContext.Response.Body);
        Assert.Equal("Exception was thrown by handler.", responseJson.RootElement.GetProperty("message").GetString());
        Assert.Equal("Exception was thrown by handler.", responseJson.RootElement.GetProperty("error").GetString());
        Assert.Equal((int)StatusCode.Unknown, responseJson.RootElement.GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task HandleCallAsync_MatchingRepeatedQueryStringValues_SetOnRequestMessage()
    {
        // Arrange
        HelloRequest? request = null;
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
        {
            request = r;
            return Task.FromResult(new HelloReply());
        };

        var unaryServerCallHandler = CreateCallHandler(invoker);
        var httpContext = TestHelpers.CreateHttpContext();
        httpContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["sub.subfields"] = new StringValues(new[] { "TestSubfields1!", "TestSubfields2!" })
        });

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.NotNull(request);
        Assert.Equal(2, request!.Sub.Subfields.Count);
        Assert.Equal("TestSubfields1!", request!.Sub.Subfields[0]);
        Assert.Equal("TestSubfields2!", request!.Sub.Subfields[1]);
    }

    [Fact]
    public async Task HandleCallAsync_DataTypes_SetOnRequestMessage()
    {
        // Arrange
        HelloRequest? request = null;
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
        {
            request = r;
            return Task.FromResult(new HelloReply());
        };

        var unaryServerCallHandler = CreateCallHandler(invoker);
        var httpContext = TestHelpers.CreateHttpContext();
        httpContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["data.single_int32"] = "1",
            ["data.single_int64"] = "2",
            ["data.single_uint32"] = "3",
            ["data.single_uint64"] = "4",
            ["data.single_sint32"] = "5",
            ["data.single_sint64"] = "6",
            ["data.single_fixed32"] = "7",
            ["data.single_fixed64"] = "8",
            ["data.single_sfixed32"] = "9",
            ["data.single_sfixed64"] = "10",
            ["data.single_float"] = "11.1",
            ["data.single_double"] = "12.1",
            ["data.single_bool"] = "true",
            ["data.single_string"] = "A string",
            ["data.single_bytes"] = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
            ["data.single_enum"] = "FOO",
            ["data.single_message.subfield"] = "Nested string"
        });

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.NotNull(request);
        Assert.Equal(1, request!.Data.SingleInt32);
        Assert.Equal(2, request!.Data.SingleInt64);
        Assert.Equal((uint)3, request!.Data.SingleUint32);
        Assert.Equal((ulong)4, request!.Data.SingleUint64);
        Assert.Equal(5, request!.Data.SingleSint32);
        Assert.Equal(6, request!.Data.SingleSint64);
        Assert.Equal((uint)7, request!.Data.SingleFixed32);
        Assert.Equal((ulong)8, request!.Data.SingleFixed64);
        Assert.Equal(9, request!.Data.SingleSfixed32);
        Assert.Equal(10, request!.Data.SingleSfixed64);
        Assert.Equal(11.1, request!.Data.SingleFloat, 3);
        Assert.Equal(12.1, request!.Data.SingleDouble, 3);
        Assert.True(request!.Data.SingleBool);
        Assert.Equal("A string", request!.Data.SingleString);
        Assert.Equal(new byte[] { 1, 2, 3 }, request!.Data.SingleBytes.ToByteArray());
        Assert.Equal(HelloRequest.Types.DataTypes.Types.NestedEnum.Foo, request!.Data.SingleEnum);
        Assert.Equal("Nested string", request!.Data.SingleMessage.Subfield);
    }

    [Fact]
    public async Task HandleCallAsync_GetHttpContext_ReturnValue()
    {
        HttpContext? httpContext = null;
        var request = await ExecuteUnaryHandler(handler: (r, c) =>
        {
            httpContext = c.GetHttpContext();
            return Task.FromResult(new HelloReply());
        });

        // Assert
        Assert.NotNull(httpContext);
    }

    [Fact]
    public async Task HandleCallAsync_ServerCallContextFeature_ReturnValue()
    {
        IServerCallContextFeature? feature = null;
        var request = await ExecuteUnaryHandler(handler: (r, c) =>
        {
            feature = c.GetHttpContext().Features.Get<IServerCallContextFeature>();
            return Task.FromResult(new HelloReply());
        });

        // Assert
        Assert.NotNull(feature);
    }

    [Theory]
    [InlineData("0", HelloRequest.Types.DataTypes.Types.NestedEnum.Unspecified)]
    [InlineData("1", HelloRequest.Types.DataTypes.Types.NestedEnum.Foo)]
    [InlineData("2", HelloRequest.Types.DataTypes.Types.NestedEnum.Bar)]
    [InlineData("3", HelloRequest.Types.DataTypes.Types.NestedEnum.Baz)]
    [InlineData("-1", HelloRequest.Types.DataTypes.Types.NestedEnum.Neg)]
    public async Task HandleCallAsync_IntegerEnum_SetOnRequestMessage(string value, HelloRequest.Types.DataTypes.Types.NestedEnum expectedEnum)
    {
        var request = await ExecuteUnaryHandler(httpContext =>
        {
            httpContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
            {
                ["data.single_enum"] = value
            });
        });

        // Assert
        Assert.Equal(expectedEnum, request.Data.SingleEnum);
    }

    [Theory]
    [InlineData("99")]
    [InlineData("INVALID")]
    public async Task HandleCallAsync_InvalidEnum_Error(string value)
    {
        await ExecuteUnaryHandler(httpContext =>
        {
            httpContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
            {
                ["data.single_enum"] = value
            });
        });

        var exceptionWrite = TestSink.Writes.Single(w => w.EventId.Name == "RpcConnectionError");
        Assert.Equal($"Error status code 'InvalidArgument' with detail 'Invalid value '{value}' for enum type NestedEnum.' raised.", exceptionWrite.Message);
    }

    private async Task<HelloRequest> ExecuteUnaryHandler(
        Action<HttpContext>? configureHttpContext = null,
        Func<HelloRequest, ServerCallContext, Task<HelloReply>>? handler = null)
    {
        // Arrange
        HelloRequest? request = null;
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
        {
            request = r;
            return handler != null ? handler(r, c) : Task.FromResult(new HelloReply());
        };

        var unaryServerCallHandler = CreateCallHandler(invoker);
        var httpContext = TestHelpers.CreateHttpContext();
        configureHttpContext?.Invoke(httpContext);

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);
        return request!;
    }

    [Fact]
    public async Task HandleCallAsync_Wrappers_SetOnRequestMessage()
    {
        // Arrange
        HelloRequest? request = null;
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
        {
            request = r;
            return Task.FromResult(new HelloReply());
        };

        var unaryServerCallHandler = CreateCallHandler(invoker);
        var httpContext = TestHelpers.CreateHttpContext();
        httpContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["wrappers.string_value"] = "1",
            ["wrappers.int32_value"] = "2",
            ["wrappers.int64_value"] = "3",
            ["wrappers.float_value"] = "4.1",
            ["wrappers.double_value"] = "5.1",
            ["wrappers.bool_value"] = "true",
            ["wrappers.uint32_value"] = "7",
            ["wrappers.uint64_value"] = "8",
            ["wrappers.bytes_value"] = Convert.ToBase64String(new byte[] { 1, 2, 3 })
        });

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.NotNull(request);
        Assert.Equal("1", request!.Wrappers.StringValue);
        Assert.Equal(2, request!.Wrappers.Int32Value);
        Assert.Equal(3, request!.Wrappers.Int64Value);
        Assert.Equal(4.1, request!.Wrappers.FloatValue.GetValueOrDefault(), 3);
        Assert.Equal(5.1, request!.Wrappers.DoubleValue.GetValueOrDefault(), 3);
        Assert.Equal(true, request!.Wrappers.BoolValue);
        Assert.Equal((uint)7, request!.Wrappers.Uint32Value.GetValueOrDefault());
        Assert.Equal((ulong)8, request!.Wrappers.Uint64Value.GetValueOrDefault());
        Assert.Equal(new byte[] { 1, 2, 3 }, request!.Wrappers.BytesValue.ToByteArray());
    }

    [Fact]
    public async Task HandleCallAsync_Any_Success()
    {
        // Arrange
        HelloRequest? request = null;
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
        {
            request = r;
            return Task.FromResult(new HelloReply
            {
                AnyMessage = Any.Pack(new StringValue { Value = "A value!" })
            });
        };

        var typeRegistry = TypeRegistry.FromMessages(StringValue.Descriptor, Int32Value.Descriptor);
        var jsonFormatter = new JsonFormatter(new JsonFormatter.Settings(formatDefaultValues: true, typeRegistry));

        var unaryServerCallHandler = CreateCallHandler(
            invoker,
            descriptorInfo: TestHelpers.CreateDescriptorInfo(bodyDescriptor: HelloRequest.Descriptor),
            jsonTranscodingOptions: new GrpcJsonTranscodingOptions
            {
                TypeRegistry = typeRegistry
            });
        var httpContext = TestHelpers.CreateHttpContext();
        var requestJson = jsonFormatter.Format(new HelloRequest
        {
            Name = "Test",
            AnyMessage = Any.Pack(new Int32Value { Value = 123 })
        });
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(requestJson));
        httpContext.Request.ContentType = "application/json";

        // Act
        await unaryServerCallHandler.HandleCallAsync(httpContext);

        // Assert
        Assert.NotNull(request);
        Assert.Equal("Test", request!.Name);
        Assert.Equal("type.googleapis.com/google.protobuf.Int32Value", request!.AnyMessage.TypeUrl);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var responseJson = JsonDocument.Parse(httpContext.Response.Body);

        var anyMessage = responseJson.RootElement.GetProperty("anyMessage");
        Assert.Equal("type.googleapis.com/google.protobuf.StringValue", anyMessage.GetProperty("@type").GetString());
        Assert.Equal("A value!", anyMessage.GetProperty("value").GetString());
    }

    private UnaryServerCallHandler<JsonTranscodingGreeterService, HelloRequest, HelloReply> CreateCallHandler(
        UnaryServerMethod<JsonTranscodingGreeterService, HelloRequest, HelloReply> invoker,
        CallHandlerDescriptorInfo? descriptorInfo = null,
        List<(Type Type, object[] Args)>? interceptors = null,
        GrpcJsonTranscodingOptions? jsonTranscodingOptions = null)
    {
        return CreateCallHandler(
            invoker,
            CreateServiceMethod("TestMethodName", HelloRequest.Parser, HelloReply.Parser),
            descriptorInfo,
            interceptors,
            jsonTranscodingOptions);
    }

    private UnaryServerCallHandler<JsonTranscodingGreeterService, TRequest, TResponse> CreateCallHandler<TRequest, TResponse>(
        UnaryServerMethod<JsonTranscodingGreeterService, TRequest, TResponse> invoker,
        Method<TRequest, TResponse> method,
        CallHandlerDescriptorInfo? descriptorInfo = null,
        List<(Type Type, object[] Args)>? interceptors = null,
        GrpcJsonTranscodingOptions? jsonTranscodingOptions = null)
        where TRequest : class, IMessage<TRequest>
        where TResponse : class, IMessage<TResponse>
    {
        var serviceOptions = new GrpcServiceOptions();
        if (interceptors != null)
        {
            foreach (var interceptor in interceptors)
            {
                serviceOptions.Interceptors.Add(interceptor.Type, interceptor.Args ?? Array.Empty<object>());
            }
        }

        var unaryServerCallInvoker = new UnaryServerMethodInvoker<JsonTranscodingGreeterService, TRequest, TResponse>(
            invoker,
            method,
            MethodOptions.Create(new[] { serviceOptions }),
            new TestGrpcServiceActivator<JsonTranscodingGreeterService>());

        var jsonContext = new JsonContext(
            jsonTranscodingOptions?.JsonSettings ?? new GrpcJsonSettings(),
            jsonTranscodingOptions?.TypeRegistry ?? TypeRegistry.Empty);

        return new UnaryServerCallHandler<JsonTranscodingGreeterService, TRequest, TResponse>(
            unaryServerCallInvoker,
            LoggerFactory,
            descriptorInfo ?? TestHelpers.CreateDescriptorInfo(),
            JsonConverterHelper.CreateSerializerOptions(jsonContext));
    }

    public static Marshaller<TMessage> GetMarshaller<TMessage>(MessageParser<TMessage> parser) where TMessage : IMessage<TMessage> =>
        Marshallers.Create<TMessage>(r => r.ToByteArray(), data => parser.ParseFrom(data));

    public static readonly Method<HelloRequest, HelloReply> ServiceMethod = CreateServiceMethod("MethodName", HelloRequest.Parser, HelloReply.Parser);

    public static Method<TRequest, TResponse> CreateServiceMethod<TRequest, TResponse>(string methodName, MessageParser<TRequest> requestParser, MessageParser<TResponse> responseParser)
         where TRequest : IMessage<TRequest>
         where TResponse : IMessage<TResponse>
    {
        return new Method<TRequest, TResponse>(MethodType.Unary, "ServiceName", methodName, GetMarshaller(requestParser), GetMarshaller(responseParser));
    }
}
