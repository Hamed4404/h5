// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using Microsoft.CodeAnalysis;
namespace Microsoft.AspNetCore.Http.Generators.Tests;

public class RuntimeCreationTests : RequestDelegateCreationTests
{
    protected override bool IsGeneratorEnabled { get; } = false;

    [Theory]
    [InlineData("BindAsyncWrongType")]
    [InlineData("BindAsyncFromStaticAbstractInterfaceWrongType")]
    [InlineData("InheritBindAsyncWrongType")]
    public async Task MapAction_BindAsync_WithWrongType_IsNotUsed(string bindAsyncType)
    {
        var source = $$"""
app.MapGet("/", ({{bindAsyncType}} myNotBindAsyncParam) => { });
""";
        var (result, compilation) = await RunGeneratorAsync(source);

        var ex = Assert.Throws<InvalidOperationException>(() => GetEndpointFromCompilation(compilation));
        Assert.StartsWith($"BindAsync method found on {bindAsyncType} with incorrect format.", ex.Message);
    }

    [Theory]
    [InlineData("""app.MapGet("/", () => Microsoft.FSharp.Core.ExtraTopLevelOperators.DefaultAsyncBuilder.Return("Hello"));""", "Hello")]
    [InlineData("""app.MapGet("/", () => Microsoft.FSharp.Core.ExtraTopLevelOperators.DefaultAsyncBuilder.Return(new Todo { Name = "Hello" }));""", """{"id":0,"name":"Hello","isComplete":false}""")]
    [InlineData("""app.MapGet("/", () => Microsoft.FSharp.Core.ExtraTopLevelOperators.DefaultAsyncBuilder.Return(TypedResults.Ok(new Todo { Name = "Hello" })));""", """{"id":0,"name":"Hello","isComplete":false}""")]
    [InlineData("""app.MapGet("/", () => Microsoft.FSharp.Core.ExtraTopLevelOperators.DefaultAsyncBuilder.Return(default(Microsoft.FSharp.Core.Unit)));""", "null")]
    public async Task MapAction_RuntimeCreation_FSharpAsync_IsAwaitable(string source, string expectedBody)
    {
        var (result, compilation) = await RunGeneratorAsync(source);
        var endpoint = GetEndpointFromCompilation(compilation);

        VerifyStaticEndpointModel(result, endpointModel =>
        {
            Assert.Equal("/", endpointModel.RoutePattern);
            Assert.Equal("MapGet", endpointModel.HttpMethod);
            Assert.True(endpointModel.Response.IsAwaitable);
        });

        var httpContext = CreateHttpContext();
        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, expectedBody);
    }
}
