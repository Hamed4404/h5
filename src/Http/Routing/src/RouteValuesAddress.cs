// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.AspNetCore.Routing;

/// <summary>
/// An address of route name and values.
/// </summary>
public class RouteValuesAddress
{
    /// <summary>
    /// Gets or sets the route name.
    /// </summary>
    public string? RouteName { get; set; }

    /// <summary>
    /// Gets or sets the route values that are explicitly specified.
    /// </summary>
    public RouteValueDictionary ExplicitValues { get; set; } = default!;

    /// <summary>
    /// Gets or sets ambient route values from the current HTTP request.
    /// </summary>
    public RouteValueDictionary? AmbientValues { get; set; }

    /// <summary>
    /// Formats the address as string "Name(ExplicitValues)" for tracing/debugging.
    /// </summary>
    public override string ToString () => $"{RouteName}(" + string.Join(',', from kv in ExplicitValues select $"{kv.Key}=[{kv.Value}]") + ")";

}
