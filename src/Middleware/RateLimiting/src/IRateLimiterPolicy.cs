// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.RateLimiting;

public interface IRateLimiterPolicy<TPartitionKey>
{
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected { get; }

    public RateLimitPartition<TPartitionKey> GetPartition(HttpContext httpContext);
}
