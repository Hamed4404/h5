// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http2.FlowControl;

namespace Microsoft.AspNetCore.Server.Kestrel.Microbenchmarks.Http2.FlowControl;

public class FlowControlBenchmark
{
    private readonly InputFlowControl _flowControl = new(1000, 10);
    private const int N = 100000;
    private const int Spin = 50;

    [IterationSetup]
    public void IterationSetup()
    {
        _flowControl.Reset();
    }

    [Benchmark]
    public async Task ThreadsAdvanceWithWindowUpdates()
    {
        _flowControl.Reset();
        var t1 = Task.Factory.StartNew(() =>
        {
            for (int i = 0; i < N; i++)
            {
                _flowControl.TryUpdateWindow(16, out _);
            }
            _flowControl.Abort();
        }, TaskCreationOptions.LongRunning);

        var t2 = Task.Factory.StartNew(() =>
        {
            for (int i = 0; i < N; i++)
            {
                if (_flowControl.TryAdvance(1))
                {
                    for (int j = 0; j < Spin; j++)
                    {
                    }
                }
            }
        }, TaskCreationOptions.LongRunning);

        var t3 = Task.Factory.StartNew(() =>
        {
            for (int i = 0; i < N; i++)
            {
                if (_flowControl.TryAdvance(1))
                {
                    for (int j = 0; j < Spin; j++)
                    {
                    }
                }
            }
        }, TaskCreationOptions.LongRunning);

        var t4 = Task.Factory.StartNew(() =>
        {
            for (int i = 0; i < N; i++)
            {
                if (_flowControl.TryAdvance(1))
                {
                    for (int j = 0; j < Spin; j++)
                    {
                    }
                }
            }
        });
        await Task.WhenAll(t1, t2, t3, t4);
    }
}
