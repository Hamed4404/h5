// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.AspNetCore.SignalR.Client.SourceGenerator
{
    internal partial class HubProxyGenerator
    {
        public class SourceGenerationSpec
        {
            public string? GetProxyNamespace;
            public string? GetProxyClassName;
            public string? GetProxyMethodName;
            public string? GetProxyTypeParameterName;
            public string? GetProxyHubConnectionParameterName;
            public string? GetProxyMethodAccessibility;
            public string? GetProxyClassAccessibility;
            public List<ClassSpec> Classes = new();
        }

        public class ClassSpec
        {
            public string FullyQualifiedInterfaceTypeName;
            public string InterfaceTypeName;
            public string ClassTypeName;
            public List<MethodSpec> Methods = new();
            public Location CallSite;
        }

        public class MethodSpec
        {
            public string Name;
            public string FullyQualifiedReturnTypeName;
            public List<ArgumentSpec> Arguments = new();
            public SupportClassification Support;
            public string? SupportHint;
            public StreamSpec Stream;
            public string? InnerReturnTypeName;
            public bool IsReturnTypeValueTask => FullyQualifiedReturnTypeName
                .StartsWith("System.Threading.Tasks.ValueTask", StringComparison.Ordinal);
        }

        [Flags]
        public enum StreamSpec
        {
            None = 0,
            ClientToServer = 1,
            ServerToClient = 2,
            AsyncEnumerable = 4,
            Bidirectional = ClientToServer | ServerToClient
        }

        public enum SupportClassification
        {
            Supported,
            UnsupportedReturnType
        }

        public class ArgumentSpec
        {
            public string Name;
            public string FullyQualifiedTypeName;
        }
    }
}
