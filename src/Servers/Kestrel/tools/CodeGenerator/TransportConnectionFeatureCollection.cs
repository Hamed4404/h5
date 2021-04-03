// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace CodeGenerator
{
    public class TransportConnectionFeatureCollection
    {
        public static string GenerateFile()
        {
            // NOTE: This list MUST always match the set of feature interfaces implemented by TransportConnection.
            // See also: shared/TransportConnection.FeatureCollection.cs
            var features = new[]
            {
                "IConnectionIdFeature",
                "IConnectionTransportFeature",
                "IConnectionItemsFeature",
                "IMemoryPoolFeature",
                "IConnectionLifetimeFeature",
                "IConnectionSocketFeature"
            };

            var usings = $@"
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http.Features;";

            return FeatureCollectionGenerator.GenerateFile(
                namespaceName: "Microsoft.AspNetCore.Connections",
                className: "TransportConnection",
                allFeatures: features,
                implementedFeatures: features,
                extraUsings: usings,
                fallbackFeatures: null);
        }
    }
}
