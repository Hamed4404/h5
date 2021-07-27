// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Connections;

namespace Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets
{
    /// <summary>
    /// Options for socket based connections.
    /// </summary>
    public class SocketConnectionOptions
    {
        /// <summary>
        /// The <see cref="PipeOptions"/> for socket connections used for input.
        /// </summary>
        public PipeOptions InputOptions { get; init; } = new PipeOptions();

        /// <summary>
        /// The <see cref="PipeOptions"/> for socket connections used for output.
        /// </summary>
        public PipeOptions OutputOptions { get; init; } = new PipeOptions();

        /// <summary>
        /// Set to false to enable Nagle's algorithm for all socket connections.
        /// </summary>
        /// <remarks>
        /// Defaults to true.
        /// </remarks>
        public bool DelaySocketOperations { get; init; } = false;

        /// <summary>
        /// Wait until there is data available to allocate a buffer. Setting this to false can increase throughput at the cost of increased memory usage.
        /// </summary>
        /// <remarks>
        /// Defaults to true.
        /// </remarks>
        public bool WaitForDataBeforeAllocatingBuffer { get; set; } = true;
    }
}
