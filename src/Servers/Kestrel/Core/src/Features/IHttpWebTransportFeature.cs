// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using Microsoft.AspNetCore.Server.Kestrel.Core.Features;

namespace Microsoft.AspNetCore.Http.Features;

/// <summary>
/// API for accepting and retrieving WebTransport sessions.
/// </summary>
public interface IHttpWebTransportFeature
{
    /// <summary>
    /// Indicates if this request is a WebTransport request.
    /// </summary>
    public bool IsWebTransportRequest { get; internal set; }

    /// <summary>
    /// Accept the session request and allow streams to start being used.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to cancel waiting for the session.</param>
    /// <returns>An instance of a WebTransportSession which will be used to control the connection.</returns>
    [RequiresPreviewFeatures("WebTransport is a preview feature")]
    public ValueTask<IWebTransportSession> AcceptAsync(CancellationToken cancellationToken);
}
