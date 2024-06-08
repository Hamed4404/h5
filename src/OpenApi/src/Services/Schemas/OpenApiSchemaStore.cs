// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.OpenApi;

/// <summary>
/// Stores schemas generated by the JsonSchemaMapper for a
/// given OpenAPI document for later resolution.
/// </summary>
internal sealed class OpenApiSchemaStore
{
    private readonly ConcurrentDictionary<OpenApiSchemaKey, JsonObject> _schemas = new()
    {
        // Pre-populate OpenAPI schemas for well-defined types in ASP.NET Core.
        [new OpenApiSchemaKey(typeof(IFormFile), null)] = new JsonObject { ["type"] = "string", ["format"] = "binary" },
        [new OpenApiSchemaKey(typeof(IFormFileCollection), null)] = new JsonObject
        {
            ["type"] = "array",
            ["items"] = new JsonObject { ["type"] = "string", ["format"] = "binary" }
        },
        [new OpenApiSchemaKey(typeof(Stream), null)] = new JsonObject { ["type"] = "string", ["format"] = "binary" },
        [new OpenApiSchemaKey(typeof(PipeReader), null)] = new JsonObject { ["type"] = "string", ["format"] = "binary" },
    };

    /// <summary>
    /// Resolves the JSON schema for the given type and parameter description.
    /// </summary>
    /// <param name="key">The key associated with the generated schema.</param>
    /// <param name="valueFactory">A function used to generated the JSON object representing the schema.</param>
    /// <returns>A <see cref="JsonObject" /> representing the JSON schema associated with the key.</returns>
    public JsonObject GetOrAdd(OpenApiSchemaKey key, Func<OpenApiSchemaKey, JsonObject> valueFactory)
    {
        return _schemas.GetOrAdd(key, valueFactory);
    }
}
