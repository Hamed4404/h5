// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Http.Metadata;

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// Specifies a description for the endpoint in <see cref="Endpoint.Metadata"/>.
/// </summary>
/// <remarks>
/// The OpenAPI specification supports a description attribute on operations and parameters that
/// can be used to annotate endpoints with detailed, multiline descriptors of their behavior.
/// behavior.
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Delegate | AttributeTargets.Parameter, Inherited = false, AllowMultiple = false)]
public sealed class DescriptionAttribute : Attribute, IDescriptionMetadata
{
    /// <summary>
    /// Initializes an instance of the <see cref="DescriptionAttribute"/>.
    /// </summary>
    /// <param name="description">The description associated with the endpoint or parameter.</param>
    public DescriptionAttribute(string description)
    {
        Description = description;
    }

    /// <inheritdoc />
    public string Description { get; }
}
