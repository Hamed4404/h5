// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Http.Generators.StaticRouteHandlerModel.Emitters;

internal static class EndpointJsonResponseEmitter
{
    internal static void EmitJsonPreparation(this EndpointResponse endpointResponse, CodeWriter codeWriter)
    {
        if (endpointResponse is { IsSerializable: true, ResponseType: {} responseType })
        {
            var typeName = responseType.ToDisplayString(EmitterConstants.DisplayFormat);

            codeWriter.WriteLine("var serviceProvider = options?.ServiceProvider ?? options?.EndpointBuilder?.ApplicationServices;");
            codeWriter.WriteLine("var serializerOptions = serviceProvider?.GetService<IOptions<JsonOptions>>()?.Value.SerializerOptions ?? new JsonOptions().SerializerOptions;");
            codeWriter.WriteLine($"var jsonTypeInfo =  (JsonTypeInfo<{typeName}>)serializerOptions.GetTypeInfo(typeof({typeName}));");
        }
    }

    internal static string EmitJsonResponse(this EndpointResponse endpointResponse)
    {
        if (endpointResponse.ResponseType != null &&
            (endpointResponse.ResponseType.IsSealed || endpointResponse.ResponseType.IsValueType))
        {
            return $"httpContext.Response.WriteAsJsonAsync(result, jsonTypeInfo);";
        }
        return $"GeneratedRouteBuilderExtensionsCore.WriteToResponseAsync(result, httpContext, jsonTypeInfo, serializerOptions);";
    }
}
