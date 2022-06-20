// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.OutputCaching.Policies;

/// <summary>
/// A type base policy.
/// </summary>
internal sealed class TypedPolicy : IOutputCachePolicy
{
    private IOutputCachePolicy? _instance;

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    private readonly Type _policyType;

    /// <summary>
    /// Creates a new instance of <see cref="TypedPolicy"/>
    /// </summary>
    /// <param name="policyType">The type of policy.</param>
    public TypedPolicy([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type policyType)
    {
        ArgumentNullException.ThrowIfNull(policyType);

        _policyType = policyType;
    }

    private IOutputCachePolicy? CreatePolicy(OutputCacheContext context)
    {
        return _instance ??= ActivatorUtilities.CreateInstance(context.Options.ApplicationServices, _policyType) as IOutputCachePolicy;
    }

    /// <inheritdoc/>
    Task IOutputCachePolicy.CacheRequestAsync(OutputCacheContext context)
    {
        return CreatePolicy(context)?.CacheRequestAsync(context) ?? Task.CompletedTask;
    }

    /// <inheritdoc/>
    Task IOutputCachePolicy.ServeFromCacheAsync(OutputCacheContext context)
    {
        return CreatePolicy(context)?.ServeFromCacheAsync(context) ?? Task.CompletedTask;
    }

    /// <inheritdoc/>
    Task IOutputCachePolicy.ServeResponseAsync(OutputCacheContext context)
    {
        return CreatePolicy(context)?.ServeResponseAsync(context) ?? Task.CompletedTask;
    }
}
