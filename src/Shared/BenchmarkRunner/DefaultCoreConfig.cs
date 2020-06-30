// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.DotNetCli;
using BenchmarkDotNet.Validators;

namespace BenchmarkDotNet.Attributes
{
    internal class DefaultCoreConfig : ManualConfig
    {
        public DefaultCoreConfig()
        {
            AddLogger(ConsoleLogger.Default);
            AddExporter(MarkdownExporter.GitHub);

            AddDiagnoser(MemoryDiagnoser.Default);
            AddColumn(StatisticColumn.OperationsPerSecond);
            AddColumnProvider(DefaultColumnProviders.Instance);

            AddValidator(JitOptimizationsValidator.FailOnError);

            AddJob(Job.Default
#if NETCOREAPP2_1
                .WithToolchain(CsProjCoreToolchain.From(NetCoreAppSettings.NetCoreApp21))
#elif NETCOREAPP3_0
                .WithToolchain(CsProjCoreToolchain.From(new NetCoreAppSettings("netcoreapp3.0", null, ".NET Core 3.0")))
#elif NETCOREAPP3_1
                .WithToolchain(CsProjCoreToolchain.From(new NetCoreAppSettings("netcoreapp3.1", null, ".NET Core 3.1")))
#elif NETCOREAPP5_0
                .WithToolchain(CsProjCoreToolchain.From(new NetCoreAppSettings("net5.0", null, ".NET Core 5.0")))
#else
#error Target frameworks need to be updated.
#endif
                .WithGcMode(new GcMode { Server = true })
                .WithStrategy(RunStrategy.Throughput));
        }
    }
}
