// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.AspNetCore.Analyzers.RouteEmbeddedLanguage.Infrastructure;
using Microsoft.AspNetCore.App.Analyzers.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.AspNetCore.Analyzers.RouteHandlers;

using WellKnownType = WellKnownTypeData.WellKnownType;

public partial class RouteHandlerAnalyzer : DiagnosticAnalyzer
{
    private static void AtMostOneFromBodyAttribute(
        in OperationAnalysisContext context,
        WellKnownTypes wellKnownTypes,
        IMethodSymbol methodSymbol)
    {
        var fromBodyMetadataInterfaceType = wellKnownTypes.Get(WellKnownType.Microsoft_AspNetCore_Http_Metadata_IFromBodyMetadata);

        var fromBodyMetadataInterfaceParameters = methodSymbol.Parameters.Where(p => p.HasAttributeImplementingInterface(fromBodyMetadataInterfaceType));

        if (fromBodyMetadataInterfaceParameters.Count() >= 2)
        {
            foreach (var parameterSymbol in fromBodyMetadataInterfaceParameters)
            {
                if (parameterSymbol.DeclaringSyntaxReferences.Length > 0)
                {
                    var syntax = parameterSymbol.DeclaringSyntaxReferences[0].GetSyntax(context.CancellationToken);
                    var location = syntax.GetLocation();
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.AtMostOneFromBodyAttribute,
                        location
                        ));
                }

            }
        }
    }
}
