// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Testing;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore;

public class PackageTests
{
    private readonly string _packageLayoutRoot;
    private readonly ITestOutputHelper _output;

    public PackageTests(ITestOutputHelper output)
    {
        // Do nothing if this is a dev build
        if (!TestData.VerifyPackageAssemblyVersions() && !SkipOnHelixAttribute.OnHelix())
        {
            return;
        }

        _output = output;
        var packageRoot = SkipOnHelixAttribute.OnHelix() ?
            Environment.GetEnvironmentVariable("HELIX_WORKITEM_ROOT") :
            TestData.GetPackagesFolder();
        _packageLayoutRoot = SkipOnHelixAttribute.OnHelix() ?
            Path.Combine(
                Environment.GetEnvironmentVariable("DOTNET_ROOT"),
                "Packages.Layout") :
            TestData.GetPackageLayoutRoot();
        var packages = Directory
                        .GetFiles(packageRoot, "*.nupkg", SearchOption.AllDirectories)
                        .Where(file => !file.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase));
        if (Directory.Exists(_packageLayoutRoot))
        {
            Directory.Delete(_packageLayoutRoot, true);
        }
        foreach (var package in packages)
        {
            var outputPath = Path.Combine(_packageLayoutRoot, Path.GetFileNameWithoutExtension(package));
            ZipFile.ExtractToDirectory(package, outputPath);
        }
    }

    [Fact]
    public void PackageAssembliesHaveExpectedAssemblyVersions()
    {
        // Do nothing if this is a dev build
        if (!TestData.VerifyPackageAssemblyVersions() && !SkipOnHelixAttribute.OnHelix())
        {
            // TODO - remove this, just verifying this doesn't get hit in CI
            Assert.True(false);
            //return;
        }

        var versionStringWithoutPrereleaseTag = TestData.GetSharedFxVersion().Split('-', 2)[0];
        var expectedVersion = Version.Parse(versionStringWithoutPrereleaseTag);

        foreach (var packageDir in Directory.GetDirectories(_packageLayoutRoot))
        {
            // Don't test the Shared Framework or Ref pack
            if (packageDir.StartsWith("Microsoft.AspNetCore.App", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var packageAssembliesDir = Path.Combine(packageDir, "lib");
            if (Directory.Exists(packageAssembliesDir))
            {
                foreach (var tfmDir in Directory.GetDirectories(packageAssembliesDir))
                {
                    var tfm = new DirectoryInfo(tfmDir).Name;
                    foreach (var assembly in Directory.GetFiles(tfmDir, "*.dll"))
                    {
                        using var fileStream = File.OpenRead(assembly);
                        using var peReader = new PEReader(fileStream, PEStreamOptions.Default);
                        var reader = peReader.GetMetadataReader(MetadataReaderOptions.Default);
                        var assemblyVersion = reader.GetAssemblyDefinition().Version;

                        // net & netstandard assembly versions should all match Major.Minor.0.0
                        // netfx assembly versions should match Major.Minor.Patch.0
                        Assert.Equal(expectedVersion.Major, assemblyVersion.Major);
                        Assert.Equal(expectedVersion.Minor, assemblyVersion.Minor);
                        if (IsNetFx(tfm))
                        {
                            Assert.Equal(expectedVersion.Build, assemblyVersion.Build);
                        }
                        else
                        {
                            Assert.Equal(0, assemblyVersion.Build);
                        }
                        Assert.Equal(0, assemblyVersion.Revision);
                    }
                }
            }
        }
    }

    private bool IsNetFx(string tfm)
    {
        return (tfm.StartsWith("net4", StringComparison.OrdinalIgnoreCase));
    }
}

