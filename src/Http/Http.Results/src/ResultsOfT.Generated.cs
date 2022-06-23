// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// This file is generated by a tool. See: src/Http/Http.Results/tools/ResultsOfTGenerator

using Microsoft.AspNetCore.Http.Metadata;

namespace Microsoft.AspNetCore.Http.HttpResults;

/// <summary>
/// An <see cref="IResult"/> that could be one of two different <see cref="IResult"/> types. On execution will
/// execute the underlying <see cref="IResult"/> instance that was actually returned by the HTTP endpoint.
/// </summary>
/// <remarks>
/// An instance of this type cannot be created explicitly. Use the implicit cast operators to create an instance
/// from an instance of one of the declared type arguments, e.g.
/// <code>Results&lt;Ok, BadRequest&gt; result = TypedResults.Ok();</code>
/// </remarks>
/// <typeparam name="TResult1">The first result type.</typeparam>
/// <typeparam name="TResult2">The second result type.</typeparam>
public sealed class Results<TResult1, TResult2> : IResult, IEndpointMetadataProvider
    where TResult1 : IResult
    where TResult2 : IResult
{
    // Use implicit cast operators to create an instance
    private Results(IResult activeResult)
    {
        Result = activeResult;
    }

    /// <summary>
    /// Gets the actual <see cref="IResult"/> returned by the <see cref="Endpoint"/> route handler delegate.
    /// </summary>
    public IResult Result { get; }

    /// <inheritdoc/>
    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (Result is null)
        {
            throw new InvalidOperationException("The IResult assigned to the Result property must not be null.");
        }

        return Result.ExecuteAsync(httpContext);
    }

    /// <summary>
    /// Converts the <typeparamref name="TResult1"/> to a <see cref="Results{TResult1, TResult2}" />.
    /// </summary>
    /// <param name="result">The result.</param>
    public static implicit operator Results<TResult1, TResult2>(TResult1 result) => new(result);

    /// <summary>
    /// Converts the <typeparamref name="TResult2"/> to a <see cref="Results{TResult1, TResult2}" />.
    /// </summary>
    /// <param name="result">The result.</param>
    public static implicit operator Results<TResult1, TResult2>(TResult2 result) => new(result);

    /// <inheritdoc/>
    static void IEndpointMetadataProvider.PopulateMetadata(EndpointMetadataContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ResultsOfTHelper.PopulateMetadataIfTargetIsIEndpointMetadataProvider<TResult1>(context);
        ResultsOfTHelper.PopulateMetadataIfTargetIsIEndpointMetadataProvider<TResult2>(context);
    }
}

/// <summary>
/// An <see cref="IResult"/> that could be one of three different <see cref="IResult"/> types. On execution will
/// execute the underlying <see cref="IResult"/> instance that was actually returned by the HTTP endpoint.
/// </summary>
/// <remarks>
/// An instance of this type cannot be created explicitly. Use the implicit cast operators to create an instance
/// from an instance of one of the declared type arguments, e.g.
/// <code>Results&lt;Ok, BadRequest&gt; result = TypedResults.Ok();</code>
/// </remarks>
/// <typeparam name="TResult1">The first result type.</typeparam>
/// <typeparam name="TResult2">The second result type.</typeparam>
/// <typeparam name="TResult3">The third result type.</typeparam>
public sealed class Results<TResult1, TResult2, TResult3> : IResult, IEndpointMetadataProvider
    where TResult1 : IResult
    where TResult2 : IResult
    where TResult3 : IResult
{
    // Use implicit cast operators to create an instance
    private Results(IResult activeResult)
    {
        Result = activeResult;
    }

    /// <summary>
    /// Gets the actual <see cref="IResult"/> returned by the <see cref="Endpoint"/> route handler delegate.
    /// </summary>
    public IResult Result { get; }

    /// <inheritdoc/>
    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (Result is null)
        {
            throw new InvalidOperationException("The IResult assigned to the Result property must not be null.");
        }

        return Result.ExecuteAsync(httpContext);
    }

    /// <summary>
    /// Converts the <typeparamref name="TResult1"/> to a <see cref="Results{TResult1, TResult2, TResult3}" />.
    /// </summary>
    /// <param name="result">The result.</param>
    public static implicit operator Results<TResult1, TResult2, TResult3>(TResult1 result) => new(result);

    /// <summary>
    /// Converts the <typeparamref name="TResult2"/> to a <see cref="Results{TResult1, TResult2, TResult3}" />.
    /// </summary>
    /// <param name="result">The result.</param>
    public static implicit operator Results<TResult1, TResult2, TResult3>(TResult2 result) => new(result);

    /// <summary>
    /// Converts the <typeparamref name="TResult3"/> to a <see cref="Results{TResult1, TResult2, TResult3}" />.
    /// </summary>
    /// <param name="result">The result.</param>
    public static implicit operator Results<TResult1, TResult2, TResult3>(TResult3 result) => new(result);

    /// <inheritdoc/>
    static void IEndpointMetadataProvider.PopulateMetadata(EndpointMetadataContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ResultsOfTHelper.PopulateMetadataIfTargetIsIEndpointMetadataProvider<TResult1>(context);
        ResultsOfTHelper.PopulateMetadataIfTargetIsIEndpointMetadataProvider<TResult2>(context);
        ResultsOfTHelper.PopulateMetadataIfTargetIsIEndpointMetadataProvider<TResult3>(context);
    }
}

/// <summary>
/// An <see cref="IResult"/> that could be one of four different <see cref="IResult"/> types. On execution will
/// execute the underlying <see cref="IResult"/> instance that was actually returned by the HTTP endpoint.
/// </summary>
/// <remarks>
/// An instance of this type cannot be created explicitly. Use the implicit cast operators to create an instance
/// from an instance of one of the declared type arguments, e.g.
/// <code>Results&lt;Ok, BadRequest&gt; result = TypedResults.Ok();</code>
/// </remarks>
/// <typeparam name="TResult1">The first result type.</typeparam>
/// <typeparam name="TResult2">The second result type.</typeparam>
/// <typeparam name="TResult3">The third result type.</typeparam>
/// <typeparam name="TResult4">The fourth result type.</typeparam>
public sealed class Results<TResult1, TResult2, TResult3, TResult4> : IResult, IEndpointMetadataProvider
    where TResult1 : IResult
    where TResult2 : IResult
    where TResult3 : IResult
    where TResult4 : IResult
{
    // Use implicit cast operators to create an instance
    private Results(IResult activeResult)
    {
        Result = activeResult;
    }

    /// <summary>
    /// Gets the actual <see cref="IResult"/> returned by the <see cref="Endpoint"/> route handler delegate.
    /// </summary>
    public IResult Result { get; }

    /// <inheritdoc/>
    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (Result is null)
        {
            throw new InvalidOperationException("The IResult assigned to the Result property must not be null.");
        }

        return Result.ExecuteAsync(httpContext);
    }

    /// <summary>
    /// Converts the <typeparamref name="TResult1"/> to a <see cref="Results{TResult1, TResult2, TResult3, TResult4}" />.
    /// </summary>
    /// <param name="result">The result.</param>
    public static implicit operator Results<TResult1, TResult2, TResult3, TResult4>(TResult1 result) => new(result);

    /// <summary>
    /// Converts the <typeparamref name="TResult2"/> to a <see cref="Results{TResult1, TResult2, TResult3, TResult4}" />.
    /// </summary>
    /// <param name="result">The result.</param>
    public static implicit operator Results<TResult1, TResult2, TResult3, TResult4>(TResult2 result) => new(result);

    /// <summary>
    /// Converts the <typeparamref name="TResult3"/> to a <see cref="Results{TResult1, TResult2, TResult3, TResult4}" />.
    /// </summary>
    /// <param name="result">The result.</param>
    public static implicit operator Results<TResult1, TResult2, TResult3, TResult4>(TResult3 result) => new(result);

    /// <summary>
    /// Converts the <typeparamref name="TResult4"/> to a <see cref="Results{TResult1, TResult2, TResult3, TResult4}" />.
    /// </summary>
    /// <param name="result">The result.</param>
    public static implicit operator Results<TResult1, TResult2, TResult3, TResult4>(TResult4 result) => new(result);

    /// <inheritdoc/>
    static void IEndpointMetadataProvider.PopulateMetadata(EndpointMetadataContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ResultsOfTHelper.PopulateMetadataIfTargetIsIEndpointMetadataProvider<TResult1>(context);
        ResultsOfTHelper.PopulateMetadataIfTargetIsIEndpointMetadataProvider<TResult2>(context);
        ResultsOfTHelper.PopulateMetadataIfTargetIsIEndpointMetadataProvider<TResult3>(context);
        ResultsOfTHelper.PopulateMetadataIfTargetIsIEndpointMetadataProvider<TResult4>(context);
    }
}

/// <summary>
/// An <see cref="IResult"/> that could be one of five different <see cref="IResult"/> types. On execution will
/// execute the underlying <see cref="IResult"/> instance that was actually returned by the HTTP endpoint.
/// </summary>
/// <remarks>
/// An instance of this type cannot be created explicitly. Use the implicit cast operators to create an instance
/// from an instance of one of the declared type arguments, e.g.
/// <code>Results&lt;Ok, BadRequest&gt; result = TypedResults.Ok();</code>
/// </remarks>
/// <typeparam name="TResult1">The first result type.</typeparam>
/// <typeparam name="TResult2">The second result type.</typeparam>
/// <typeparam name="TResult3">The third result type.</typeparam>
/// <typeparam name="TResult4">The fourth result type.</typeparam>
/// <typeparam name="TResult5">The fifth result type.</typeparam>
public sealed class Results<TResult1, TResult2, TResult3, TResult4, TResult5> : IResult, IEndpointMetadataProvider
    where TResult1 : IResult
    where TResult2 : IResult
    where TResult3 : IResult
    where TResult4 : IResult
    where TResult5 : IResult
{
    // Use implicit cast operators to create an instance
    private Results(IResult activeResult)
    {
        Result = activeResult;
    }

    /// <summary>
    /// Gets the actual <see cref="IResult"/> returned by the <see cref="Endpoint"/> route handler delegate.
    /// </summary>
    public IResult Result { get; }

    /// <inheritdoc/>
    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (Result is null)
        {
            throw new InvalidOperationException("The IResult assigned to the Result property must not be null.");
        }

        return Result.ExecuteAsync(httpContext);
    }

    /// <summary>
    /// Converts the <typeparamref name="TResult1"/> to a <see cref="Results{TResult1, TResult2, TResult3, TResult4, TResult5}" />.
    /// </summary>
    /// <param name="result">The result.</param>
    public static implicit operator Results<TResult1, TResult2, TResult3, TResult4, TResult5>(TResult1 result) => new(result);

    /// <summary>
    /// Converts the <typeparamref name="TResult2"/> to a <see cref="Results{TResult1, TResult2, TResult3, TResult4, TResult5}" />.
    /// </summary>
    /// <param name="result">The result.</param>
    public static implicit operator Results<TResult1, TResult2, TResult3, TResult4, TResult5>(TResult2 result) => new(result);

    /// <summary>
    /// Converts the <typeparamref name="TResult3"/> to a <see cref="Results{TResult1, TResult2, TResult3, TResult4, TResult5}" />.
    /// </summary>
    /// <param name="result">The result.</param>
    public static implicit operator Results<TResult1, TResult2, TResult3, TResult4, TResult5>(TResult3 result) => new(result);

    /// <summary>
    /// Converts the <typeparamref name="TResult4"/> to a <see cref="Results{TResult1, TResult2, TResult3, TResult4, TResult5}" />.
    /// </summary>
    /// <param name="result">The result.</param>
    public static implicit operator Results<TResult1, TResult2, TResult3, TResult4, TResult5>(TResult4 result) => new(result);

    /// <summary>
    /// Converts the <typeparamref name="TResult5"/> to a <see cref="Results{TResult1, TResult2, TResult3, TResult4, TResult5}" />.
    /// </summary>
    /// <param name="result">The result.</param>
    public static implicit operator Results<TResult1, TResult2, TResult3, TResult4, TResult5>(TResult5 result) => new(result);

    /// <inheritdoc/>
    static void IEndpointMetadataProvider.PopulateMetadata(EndpointMetadataContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ResultsOfTHelper.PopulateMetadataIfTargetIsIEndpointMetadataProvider<TResult1>(context);
        ResultsOfTHelper.PopulateMetadataIfTargetIsIEndpointMetadataProvider<TResult2>(context);
        ResultsOfTHelper.PopulateMetadataIfTargetIsIEndpointMetadataProvider<TResult3>(context);
        ResultsOfTHelper.PopulateMetadataIfTargetIsIEndpointMetadataProvider<TResult4>(context);
        ResultsOfTHelper.PopulateMetadataIfTargetIsIEndpointMetadataProvider<TResult5>(context);
    }
}

/// <summary>
/// An <see cref="IResult"/> that could be one of six different <see cref="IResult"/> types. On execution will
/// execute the underlying <see cref="IResult"/> instance that was actually returned by the HTTP endpoint.
/// </summary>
/// <remarks>
/// An instance of this type cannot be created explicitly. Use the implicit cast operators to create an instance
/// from an instance of one of the declared type arguments, e.g.
/// <code>Results&lt;Ok, BadRequest&gt; result = TypedResults.Ok();</code>
/// </remarks>
/// <typeparam name="TResult1">The first result type.</typeparam>
/// <typeparam name="TResult2">The second result type.</typeparam>
/// <typeparam name="TResult3">The third result type.</typeparam>
/// <typeparam name="TResult4">The fourth result type.</typeparam>
/// <typeparam name="TResult5">The fifth result type.</typeparam>
/// <typeparam name="TResult6">The sixth result type.</typeparam>
public sealed class Results<TResult1, TResult2, TResult3, TResult4, TResult5, TResult6> : IResult, IEndpointMetadataProvider
    where TResult1 : IResult
    where TResult2 : IResult
    where TResult3 : IResult
    where TResult4 : IResult
    where TResult5 : IResult
    where TResult6 : IResult
{
    // Use implicit cast operators to create an instance
    private Results(IResult activeResult)
    {
        Result = activeResult;
    }

    /// <summary>
    /// Gets the actual <see cref="IResult"/> returned by the <see cref="Endpoint"/> route handler delegate.
    /// </summary>
    public IResult Result { get; }

    /// <inheritdoc/>
    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (Result is null)
        {
            throw new InvalidOperationException("The IResult assigned to the Result property must not be null.");
        }

        return Result.ExecuteAsync(httpContext);
    }

    /// <summary>
    /// Converts the <typeparamref name="TResult1"/> to a <see cref="Results{TResult1, TResult2, TResult3, TResult4, TResult5, TResult6}" />.
    /// </summary>
    /// <param name="result">The result.</param>
    public static implicit operator Results<TResult1, TResult2, TResult3, TResult4, TResult5, TResult6>(TResult1 result) => new(result);

    /// <summary>
    /// Converts the <typeparamref name="TResult2"/> to a <see cref="Results{TResult1, TResult2, TResult3, TResult4, TResult5, TResult6}" />.
    /// </summary>
    /// <param name="result">The result.</param>
    public static implicit operator Results<TResult1, TResult2, TResult3, TResult4, TResult5, TResult6>(TResult2 result) => new(result);

    /// <summary>
    /// Converts the <typeparamref name="TResult3"/> to a <see cref="Results{TResult1, TResult2, TResult3, TResult4, TResult5, TResult6}" />.
    /// </summary>
    /// <param name="result">The result.</param>
    public static implicit operator Results<TResult1, TResult2, TResult3, TResult4, TResult5, TResult6>(TResult3 result) => new(result);

    /// <summary>
    /// Converts the <typeparamref name="TResult4"/> to a <see cref="Results{TResult1, TResult2, TResult3, TResult4, TResult5, TResult6}" />.
    /// </summary>
    /// <param name="result">The result.</param>
    public static implicit operator Results<TResult1, TResult2, TResult3, TResult4, TResult5, TResult6>(TResult4 result) => new(result);

    /// <summary>
    /// Converts the <typeparamref name="TResult5"/> to a <see cref="Results{TResult1, TResult2, TResult3, TResult4, TResult5, TResult6}" />.
    /// </summary>
    /// <param name="result">The result.</param>
    public static implicit operator Results<TResult1, TResult2, TResult3, TResult4, TResult5, TResult6>(TResult5 result) => new(result);

    /// <summary>
    /// Converts the <typeparamref name="TResult6"/> to a <see cref="Results{TResult1, TResult2, TResult3, TResult4, TResult5, TResult6}" />.
    /// </summary>
    /// <param name="result">The result.</param>
    public static implicit operator Results<TResult1, TResult2, TResult3, TResult4, TResult5, TResult6>(TResult6 result) => new(result);

    /// <inheritdoc/>
    static void IEndpointMetadataProvider.PopulateMetadata(EndpointMetadataContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ResultsOfTHelper.PopulateMetadataIfTargetIsIEndpointMetadataProvider<TResult1>(context);
        ResultsOfTHelper.PopulateMetadataIfTargetIsIEndpointMetadataProvider<TResult2>(context);
        ResultsOfTHelper.PopulateMetadataIfTargetIsIEndpointMetadataProvider<TResult3>(context);
        ResultsOfTHelper.PopulateMetadataIfTargetIsIEndpointMetadataProvider<TResult4>(context);
        ResultsOfTHelper.PopulateMetadataIfTargetIsIEndpointMetadataProvider<TResult5>(context);
        ResultsOfTHelper.PopulateMetadataIfTargetIsIEndpointMetadataProvider<TResult6>(context);
    }
}
