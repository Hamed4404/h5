// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.RateLimiting;

namespace Microsoft.AspNetCore.RateLimiting;

internal class TestRateLimiter : RateLimiter
{
    private readonly bool _alwaysAccept;
    private RateLimiterStatistics _statistics;

    public TestRateLimiter(bool alwaysAccept, RateLimiterStatistics statistics = null)
    {
        _alwaysAccept = alwaysAccept;
        _statistics = statistics;
    }

    public override TimeSpan? IdleDuration => throw new NotImplementedException();

    public override RateLimiterStatistics GetStatistics()
    {
        return _statistics;
    }

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        return new TestRateLimitLease(_alwaysAccept, null);
    }

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<RateLimitLease>(new TestRateLimitLease(_alwaysAccept, null));
    }
}
