﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace RepoTasks
{
    public class CreateTimestampFreePackages : Task
    {
        /// <summary>
        /// The packages to produce time stamp free versions for.
        /// </summary>
        [Required]
        public ITaskItem[] PackagesWithTimestamp { get; set; }

        /// <summary>
        /// Packages that already have a timestamp free version that were generated by some other method. i.e. templates packages.
        /// </summary>
        public ITaskItem[] PackagesAlreadyTimestampFree { get; set; }

        /// <summary>
        /// The directory to output time stamp free packages to.
        /// </summary>
        [Required]
        public string OutputDirectory { get; set; }

        [Output]
        public ITaskItem[] PackagesWithoutTimestamp { get; set; }

        public override bool Execute()
        {
            var packageIds = GetKnownPackageIds();
            var packagesAlreadyTimestampFree = GetPackagesAlreadyTimestampFree();

            var output = new List<ITaskItem>();
            foreach (var item in packageIds)
            {
                var packageWithoutTimestampPath = CreateTimeStampFreePackage(packageIds, packagesAlreadyTimestampFree, item.Key, item.Value);
                Log.LogMessage($"Creating timestamp free version at {packageWithoutTimestampPath} from {item.Key}.");

                output.Add(new TaskItem(packageWithoutTimestampPath));
            }

            PackagesWithoutTimestamp = output.ToArray();
            return true;
        }

        private Dictionary<string, string> GetKnownPackageIds()
        {
            var packages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var package in PackagesWithTimestamp)
            {
                packages.Add(ReadPackageIdentity(package.ItemSpec).Id, package.ItemSpec);
            }

            return packages;
        }

        private Dictionary<string, string> GetPackagesAlreadyTimestampFree()
        {
            var packages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (PackagesAlreadyTimestampFree == null)
            {
                return packages;
            }

            foreach (var package in PackagesAlreadyTimestampFree)
            {
                packages.Add(ReadPackageIdentity(package.ItemSpec).Id, package.ItemSpec);
            }

            return packages;
        }

        private string CreateTimeStampFreePackage(Dictionary<string, string> knownPackageIds, Dictionary<string, string> packagesAlreadyTimestampFree, string packageId, string packagePath)
        {
            string targetPath;
            if (packagesAlreadyTimestampFree.TryGetValue(packageId, out var packageWithoutTimestamp))
            {
                targetPath = Path.Combine(OutputDirectory, Path.GetFileName(packageWithoutTimestamp));
                File.Copy(packageWithoutTimestamp, targetPath, overwrite: true);
                return targetPath;
            }

            targetPath = Path.Combine(OutputDirectory, Path.GetFileName(packagePath));
            File.Copy(packagePath, targetPath, overwrite: true);

            PackageIdentity updatedIdentity;

            using (var fileStream = File.Open(targetPath, FileMode.Open))
            using (var package = new ZipArchive(fileStream, ZipArchiveMode.Update))
            {
                var packageReader = new PackageArchiveReader(packagePath);
                var identity = packageReader.GetIdentity();
                var updatedVersion = new NuGetVersion(StripBuildVersion(identity.Version));

                updatedIdentity = new PackageIdentity(identity.Id, updatedVersion);

                var nuspecFile = packageReader.GetNuspecFile();
                using (var stream = package.OpenFile(nuspecFile))
                {
                    var reader = Manifest.ReadFrom(stream, validateSchema: true);
                    stream.Position = 0;
                    var packageBuilder = new PackageBuilder(stream, basePath: null);
                    packageBuilder.Version = updatedVersion;
                    var updatedGroups = new List<PackageDependencyGroup>();

                    foreach (var group in packageBuilder.DependencyGroups)
                    {
                        var packages = new List<PackageDependency>();
                        var updatedGroup = new PackageDependencyGroup(group.TargetFramework, packages);
                        foreach (var dependency in group.Packages)
                        {
                            PackageDependency dependencyToAdd;
                            if (knownPackageIds.ContainsKey(dependency.Id))
                            {
                                dependencyToAdd = UpdateDependency(identity, dependency);
                            }
                            else
                            {
                                dependencyToAdd = dependency;
                            }

                            packages.Add(dependencyToAdd);
                        }

                        updatedGroups.Add(updatedGroup);
                    }

                    packageBuilder.DependencyGroups.Clear();
                    packageBuilder.DependencyGroups.AddRange(updatedGroups);

                    var updatedManifest = Manifest.Create(packageBuilder);
                    stream.Position = 0;
                    stream.SetLength(0);
                    updatedManifest.Save(stream);
                }

                // Metapackage needs to update manifest files
                if (identity.Id.Equals("Microsoft.AspNetCore.All", StringComparison.OrdinalIgnoreCase))
                {
                    var entry = package.GetEntry($"build/aspnetcore-store-{identity.Version}.xml");
                    var releaseLabel = identity.Version.Release;
                    var updatedReleaseLabel = Utilities.GetNoTimestampReleaseLabel(releaseLabel);
                    using (var entryStream = new StreamReader(entry.Open()))
                    using (var writer = new StreamWriter(package.CreateEntry($"build/aspnetcore-store-{updatedIdentity.Version.ToNormalizedString()}.xml").Open()))
                    {
                            writer.Write(entryStream.ReadToEnd().Replace($"-{releaseLabel}", string.IsNullOrEmpty(updatedReleaseLabel) ? updatedReleaseLabel : $"-{updatedReleaseLabel}"));
                    }
                    entry.Delete();
                }
            }

            var updatedTargetPath = Path.Combine(OutputDirectory, updatedIdentity.Id + '.' + updatedIdentity.Version.ToNormalizedString() + ".nupkg");
            if (File.Exists(updatedTargetPath))
            {
                File.Delete(updatedTargetPath);
            }
            File.Move(targetPath, updatedTargetPath);
            return updatedTargetPath;
        }

        private static PackageIdentity ReadPackageIdentity(string filePath)
        {
            using (var reader = new PackageArchiveReader(filePath))
            {
                return reader.GetIdentity();
            }
        }

        private static PackageDependency UpdateDependency(PackageIdentity id, PackageDependency dependency)
        {
            if (!dependency.VersionRange.HasLowerBound)
            {
                throw new Exception($"Dependency {dependency} for {id} does not have a lower bound.");
            }

            if (dependency.VersionRange.HasUpperBound)
            {
                throw new Exception($"Dependency {dependency} for {id} has an upper bound.");
            }

            var minVersion = StripBuildVersion(dependency.VersionRange.MinVersion);
            return new PackageDependency(dependency.Id, new VersionRange(minVersion));
        }

        private static NuGetVersion StripBuildVersion(NuGetVersion version)
        {
            return new NuGetVersion(version.Version, Utilities.GetNoTimestampReleaseLabel(version.Release));
        }
    }
}
