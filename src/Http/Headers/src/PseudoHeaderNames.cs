// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Net.Http.Headers;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1802:Use literals where appropriate", Justification = "So referenceEquals works")]
internal static class PseudoHeaderNames
{
    /// <summary>Gets the <c>:authority</c> HTTP header name.</summary>
    public static readonly string Authority = ":authority";

    /// <summary>Gets the <c>:method</c> HTTP header name.</summary>
    public static readonly string Method = ":method";

    /// <summary>Gets the <c>:path</c> HTTP header name.</summary>
    public static readonly string Path = ":path";

    /// <summary>Gets the <c>:scheme</c> HTTP header name.</summary>
    public static readonly string Scheme = ":scheme";

    /// <summary>Gets the <c>:status</c> HTTP header name.</summary>
    public static readonly string Status = ":status";

    /// <summary>Gets the <c>:protocol</c> HTTP header name.</summary>
    public static readonly string Protocol = ":protocol";
}
