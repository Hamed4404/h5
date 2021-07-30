// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Internal;

namespace Microsoft.AspNetCore.Components
{
    /// <summary>
    /// Provides extension methods for the <see cref="NavigationManager"/> type.
    /// </summary>
    public static class NavigationManagerExtensions
    {
        private const string EmptyQueryParameterNameExceptionMessage = "Cannot have empty query parameter names.";

        private delegate string? QueryParameterFormatter<TValue>(TValue value);

        // We don't include mappings for Nullable types because we explicitly check for null values
        // to see if the parameter should be excluded from the querystring. Therefore, we will only
        // invoke these formatters for non-null values. We also get the underlying type of any Nullable
        // types before performing lookups in this dictionary.
        private static readonly Dictionary<Type, QueryParameterFormatter<object>> _queryParameterFormatters = new()
        {
            [typeof(string)] = value => Format((string)value)!,
            [typeof(bool)] = value => Format((bool)value),
            [typeof(DateTime)] = value => Format((DateTime)value),
            [typeof(decimal)] = value => Format((decimal)value),
            [typeof(double)] = value => Format((double)value),
            [typeof(float)] = value => Format((float)value),
            [typeof(Guid)] = value => Format((Guid)value),
            [typeof(int)] = value => Format((int)value),
            [typeof(long)] = value => Format((long)value),
        };

        private static string? Format(string? value)
            => value;

        private static string Format(bool value)
            => value.ToString(CultureInfo.InvariantCulture);

        private static string? Format(bool? value)
            => value?.ToString(CultureInfo.InvariantCulture);

        private static string Format(DateTime value)
            => value.ToString(CultureInfo.InvariantCulture);

        private static string? Format(DateTime? value)
            => value?.ToString(CultureInfo.InvariantCulture);

        private static string Format(decimal value)
            => value.ToString(CultureInfo.InvariantCulture);

        private static string? Format(decimal? value)
            => value?.ToString(CultureInfo.InvariantCulture);

        private static string Format(double value)
            => value.ToString(CultureInfo.InvariantCulture);

        private static string? Format(double? value)
            => value?.ToString(CultureInfo.InvariantCulture);

        private static string Format(float value)
            => value.ToString(CultureInfo.InvariantCulture);

        private static string? Format(float? value)
            => value?.ToString(CultureInfo.InvariantCulture);

        private static string Format(Guid value)
            => value.ToString(null, CultureInfo.InvariantCulture);

        private static string? Format(Guid? value)
            => value?.ToString(null, CultureInfo.InvariantCulture);

        private static string Format(int value)
            => value.ToString(CultureInfo.InvariantCulture);

        private static string? Format(int? value)
            => value?.ToString(CultureInfo.InvariantCulture);

        private static string Format(long value)
            => value.ToString(CultureInfo.InvariantCulture);

        private static string? Format(long? value)
            => value?.ToString(CultureInfo.InvariantCulture);

        // Used for constructing a URI with a new querystring from an existing URI.
        private struct QueryStringBuilder
        {
            private readonly StringBuilder _builder;

            private bool _hasNewParameters;

            public string UriWithQueryString => _builder.ToString();

            public QueryStringBuilder(ReadOnlySpan<char> uriWithoutQueryString, int additionalCapacity = 0)
            {
                _builder = new(uriWithoutQueryString.Length + additionalCapacity);
                _builder.Append(uriWithoutQueryString);

                _hasNewParameters = false;
            }

            public void AppendParameter(ReadOnlySpan<char> encodedName, ReadOnlySpan<char> encodedValue)
            {
                if (!_hasNewParameters)
                {
                    _hasNewParameters = true;
                    _builder.Append('?');
                }
                else
                {
                    _builder.Append('&');
                }

                _builder.Append(encodedName);
                _builder.Append('=');
                _builder.Append(encodedValue);
            }
        }

        // A utility for feeding a collection of parameter values into a QueryStringBuilder.
        private readonly struct QueryParameterSource<TValue>
        {
            private readonly IEnumerator<TValue?>? _enumerator;
            private readonly QueryParameterFormatter<TValue>? _formatter;

            public string EncodedName { get; }

            // Creates an empty instance to simulate a source without any elements.
            public QueryParameterSource(string name)
            {
                if (string.IsNullOrEmpty(name))
                {
                    throw new InvalidOperationException(EmptyQueryParameterNameExceptionMessage);
                }

                EncodedName = Uri.EscapeDataString(name);

                _enumerator = default;
                _formatter = default;
            }

            public QueryParameterSource(string name, IEnumerable<TValue?> values, QueryParameterFormatter<TValue> formatter)
                : this(name)
            {
                _enumerator = values.GetEnumerator();
                _formatter = formatter;
            }

            public bool AppendNextParameter(ref QueryStringBuilder builder)
            {
                if (_enumerator is null || !_enumerator.MoveNext())
                {
                    return false;
                }

                var currentValue = _enumerator.Current;

                if (currentValue is null)
                {
                    // No-op to simulate appending a null parameter.
                    return true;
                }

                var formattedValue = _formatter!(currentValue);
                var encodedValue = Uri.EscapeDataString(formattedValue!);
                builder.AppendParameter(EncodedName, encodedValue);
                return true;
            }
        }

        // A utility for feeding an object of unknown type as one or more parameter values into
        // a QueryStringBuilder.
        private struct QueryParameterSource
        {
            private readonly QueryParameterSource<object> _source;
            private string? _encodedValue;

            public string EncodedName => _source.EncodedName;

            public QueryParameterSource(string name, object? value)
            {
                if (value is null)
                {
                    _source = new(name);
                    _encodedValue = default;
                    return;
                }

                var valueType = value.GetType();

                if (valueType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(valueType))
                {
                    // The provided value was of enumerable type, so we populate the underlying source.
                    var elementType = valueType.GetElementType()!;
                    var formatter = GetFormatterFromParameterValueType(elementType);

                    // This cast is inevitable; the values have to be boxed anyway to be formatted.
                    var values = ((IEnumerable)value).Cast<object>();

                    _source = new(name, values, formatter);
                    _encodedValue = default;
                }
                else
                {
                    // The provided value was not of enumerable type, so we leave the underlying source
                    // empty and instead cache the encoded value to be appended later.
                    var formatter = GetFormatterFromParameterValueType(valueType);
                    var formattedValue = formatter(value);
                    _source = new(name);
                    _encodedValue = Uri.EscapeDataString(formattedValue!);
                }
            }

            public bool AppendNextParameter(ref QueryStringBuilder builder)
            {
                if (_source.AppendNextParameter(ref builder))
                {
                    // The underlying source of values had elements, so there is no more work to do here.
                    return true;
                }

                // Either we've run out of elements to append or the given value was not of enumerable
                // type in the first place.

                // If the value was not of enumerable type and has not been appended, append it
                // and set it to null so we don't provide the value more than once.
                if (_encodedValue is not null)
                {
                    builder.AppendParameter(_source.EncodedName, _encodedValue);
                    _encodedValue = null;
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// added or updated.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="value">The value of the parameter to add or update.</param>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, bool value)
            => UriWithQueryParameter(navigationManager, name, Format(value));

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// added, updated, or removed.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="value">The value of the parameter to add or update.</param>
        /// <remarks>
        /// If <paramref name="value"/> is <c>null</c>, the parameter will be removed if it exists in the URI.
        /// Otherwise, it will be added or updated.
        /// </remarks>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, bool? value)
            => UriWithQueryParameter(navigationManager, name, Format(value));

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// added or updated.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="value">The value of the parameter to add or update.</param>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, DateTime value)
            => UriWithQueryParameter(navigationManager, name, Format(value));

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// added, updated, or removed.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="value">The value of the parameter to add or update.</param>
        /// <remarks>
        /// If <paramref name="value"/> is <c>null</c>, the parameter will be removed if it exists in the URI.
        /// Otherwise, it will be added or updated.
        /// </remarks>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, DateTime? value)
            => UriWithQueryParameter(navigationManager, name, Format(value));

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// added or updated.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="value">The value of the parameter to add or update.</param>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, decimal value)
            => UriWithQueryParameter(navigationManager, name, Format(value));

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// added, updated, or removed.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="value">The value of the parameter to add or update.</param>
        /// <remarks>
        /// If <paramref name="value"/> is <c>null</c>, the parameter will be removed if it exists in the URI.
        /// Otherwise, it will be added or updated.
        /// </remarks>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, decimal? value)
            => UriWithQueryParameter(navigationManager, name, Format(value));

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// added or updated.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="value">The value of the parameter to add or update.</param>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, double value)
            => UriWithQueryParameter(navigationManager, name, Format(value));

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// added, updated, or removed.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="value">The value of the parameter to add or update.</param>
        /// <remarks>
        /// If <paramref name="value"/> is <c>null</c>, the parameter will be removed if it exists in the URI.
        /// Otherwise, it will be added or updated.
        /// </remarks>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, double? value)
            => UriWithQueryParameter(navigationManager, name, Format(value));

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// added or updated.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="value">The value of the parameter to add or update.</param>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, float value)
            => UriWithQueryParameter(navigationManager, name, Format(value));

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// added, updated, or removed.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="value">The value of the parameter to add or update.</param>
        /// <remarks>
        /// If <paramref name="value"/> is <c>null</c>, the parameter will be removed if it exists in the URI.
        /// Otherwise, it will be added or updated.
        /// </remarks>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, float? value)
            => UriWithQueryParameter(navigationManager, name, Format(value));

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// added or updated.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="value">The value of the parameter to add or update.</param>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, Guid value)
            => UriWithQueryParameter(navigationManager, name, Format(value));

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// added, updated, or removed.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="value">The value of the parameter to add or update.</param>
        /// <remarks>
        /// If <paramref name="value"/> is <c>null</c>, the parameter will be removed if it exists in the URI.
        /// Otherwise, it will be added or updated.
        /// </remarks>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, Guid? value)
            => UriWithQueryParameter(navigationManager, name, Format(value));

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// added or updated.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="value">The value of the parameter to add or update.</param>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, int value)
            => UriWithQueryParameter(navigationManager, name, Format(value));

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// added, updated, or removed.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="value">The value of the parameter to add or update.</param>
        /// <remarks>
        /// If <paramref name="value"/> is <c>null</c>, the parameter will be removed if it exists in the URI.
        /// Otherwise, it will be added or updated.
        /// </remarks>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, int? value)
            => UriWithQueryParameter(navigationManager, name, Format(value));

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// added or updated.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="value">The value of the parameter to add or update.</param>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, long value)
            => UriWithQueryParameter(navigationManager, name, Format(value));

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// added, updated, or removed.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="value">The value of the parameter to add or update.</param>
        /// <remarks>
        /// If <paramref name="value"/> is <c>null</c>, the parameter will be removed if it exists in the URI.
        /// Otherwise, it will be added or updated.
        /// </remarks>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, long? value)
            => UriWithQueryParameter(navigationManager, name, Format(value));

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// updated with the provided <paramref name="values"/>.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="values">The parameter values to add or update.</param>
        /// <remarks>
        /// Any <c>null</c> entries in <paramref name="values"/> will be skipped. Existing querystring parameters not in
        /// <paramref name="values"/> will be removed from the querystring in the returned URI.
        /// </remarks>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, IEnumerable<string?> values)
            => UriWithQueryParameter(navigationManager, name, values, Format);

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// updated with the provided <paramref name="values"/>.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="values">The parameter values to add or update.</param>
        /// <remarks>
        /// Any <c>null</c> entries in <paramref name="values"/> will be skipped. Existing querystring parameters not in
        /// <paramref name="values"/> will be removed from the querystring in the returned URI.
        /// </remarks>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, IEnumerable<bool> values)
            => UriWithQueryParameter(navigationManager, name, values, Format);

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// updated with the provided <paramref name="values"/>.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="values">The parameter values to add or update.</param>
        /// <remarks>
        /// Any <c>null</c> entries in <paramref name="values"/> will be skipped. Existing querystring parameters not in
        /// <paramref name="values"/> will be removed from the querystring in the returned URI.
        /// </remarks>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, IEnumerable<bool?> values)
            => UriWithQueryParameter(navigationManager, name, values, Format);

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// updated with the provided <paramref name="values"/>.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="values">The parameter values to add or update.</param>
        /// <remarks>
        /// Any <c>null</c> entries in <paramref name="values"/> will be skipped. Existing querystring parameters not in
        /// <paramref name="values"/> will be removed from the querystring in the returned URI.
        /// </remarks>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, IEnumerable<DateTime> values)
            => UriWithQueryParameter(navigationManager, name, values, Format);

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// updated with the provided <paramref name="values"/>.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="values">The parameter values to add or update.</param>
        /// <remarks>
        /// Any <c>null</c> entries in <paramref name="values"/> will be skipped. Existing querystring parameters not in
        /// <paramref name="values"/> will be removed from the querystring in the returned URI.
        /// </remarks>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, IEnumerable<DateTime?> values)
            => UriWithQueryParameter(navigationManager, name, values, Format);

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// updated with the provided <paramref name="values"/>.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="values">The parameter values to add or update.</param>
        /// <remarks>
        /// Any <c>null</c> entries in <paramref name="values"/> will be skipped. Existing querystring parameters not in
        /// <paramref name="values"/> will be removed from the querystring in the returned URI.
        /// </remarks>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, IEnumerable<decimal> values)
            => UriWithQueryParameter(navigationManager, name, values, Format);

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// updated with the provided <paramref name="values"/>.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="values">The parameter values to add or update.</param>
        /// <remarks>
        /// Any <c>null</c> entries in <paramref name="values"/> will be skipped. Existing querystring parameters not in
        /// <paramref name="values"/> will be removed from the querystring in the returned URI.
        /// </remarks>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, IEnumerable<decimal?> values)
            => UriWithQueryParameter(navigationManager, name, values, Format);

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// updated with the provided <paramref name="values"/>.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="values">The parameter values to add or update.</param>
        /// <remarks>
        /// Any <c>null</c> entries in <paramref name="values"/> will be skipped. Existing querystring parameters not in
        /// <paramref name="values"/> will be removed from the querystring in the returned URI.
        /// </remarks>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, IEnumerable<double> values)
            => UriWithQueryParameter(navigationManager, name, values, Format);

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// updated with the provided <paramref name="values"/>.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="values">The parameter values to add or update.</param>
        /// <remarks>
        /// Any <c>null</c> entries in <paramref name="values"/> will be skipped. Existing querystring parameters not in
        /// <paramref name="values"/> will be removed from the querystring in the returned URI.
        /// </remarks>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, IEnumerable<double?> values)
            => UriWithQueryParameter(navigationManager, name, values, Format);

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// updated with the provided <paramref name="values"/>.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="values">The parameter values to add or update.</param>
        /// <remarks>
        /// Any <c>null</c> entries in <paramref name="values"/> will be skipped. Existing querystring parameters not in
        /// <paramref name="values"/> will be removed from the querystring in the returned URI.
        /// </remarks>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, IEnumerable<float> values)
            => UriWithQueryParameter(navigationManager, name, values, Format);

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// updated with the provided <paramref name="values"/>.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="values">The parameter values to add or update.</param>
        /// <remarks>
        /// Any <c>null</c> entries in <paramref name="values"/> will be skipped. Existing querystring parameters not in
        /// <paramref name="values"/> will be removed from the querystring in the returned URI.
        /// </remarks>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, IEnumerable<float?> values)
            => UriWithQueryParameter(navigationManager, name, values, Format);

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// updated with the provided <paramref name="values"/>.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="values">The parameter values to add or update.</param>
        /// <remarks>
        /// Any <c>null</c> entries in <paramref name="values"/> will be skipped. Existing querystring parameters not in
        /// <paramref name="values"/> will be removed from the querystring in the returned URI.
        /// </remarks>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, IEnumerable<Guid> values)
            => UriWithQueryParameter(navigationManager, name, values, Format);

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// updated with the provided <paramref name="values"/>.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="values">The parameter values to add or update.</param>
        /// <remarks>
        /// Any <c>null</c> entries in <paramref name="values"/> will be skipped. Existing querystring parameters not in
        /// <paramref name="values"/> will be removed from the querystring in the returned URI.
        /// </remarks>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, IEnumerable<Guid?> values)
            => UriWithQueryParameter(navigationManager, name, values, Format);

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// updated with the provided <paramref name="values"/>.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="values">The parameter values to add or update.</param>
        /// <remarks>
        /// Any <c>null</c> entries in <paramref name="values"/> will be skipped. Existing querystring parameters not in
        /// <paramref name="values"/> will be removed from the querystring in the returned URI.
        /// </remarks>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, IEnumerable<int> values)
            => UriWithQueryParameter(navigationManager, name, values, Format);

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// updated with the provided <paramref name="values"/>.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="values">The parameter values to add or update.</param>
        /// <remarks>
        /// Any <c>null</c> entries in <paramref name="values"/> will be skipped. Existing querystring parameters not in
        /// <paramref name="values"/> will be removed from the querystring in the returned URI.
        /// </remarks>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, IEnumerable<int?> values)
            => UriWithQueryParameter(navigationManager, name, values, Format);

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// updated with the provided <paramref name="values"/>.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="values">The parameter values to add or update.</param>
        /// <remarks>
        /// Any <c>null</c> entries in <paramref name="values"/> will be skipped. Existing querystring parameters not in
        /// <paramref name="values"/> will be removed from the querystring in the returned URI.
        /// </remarks>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, IEnumerable<long> values)
            => UriWithQueryParameter(navigationManager, name, values, Format);

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// updated with the provided <paramref name="values"/>.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="values">The parameter values to add or update.</param>
        /// <remarks>
        /// Any <c>null</c> entries in <paramref name="values"/> will be skipped. Existing querystring parameters not in
        /// <paramref name="values"/> will be removed from the querystring in the returned URI.
        /// </remarks>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, IEnumerable<long?> values)
            => UriWithQueryParameter(navigationManager, name, values, Format);

        /// <summary>
        /// Returns a URI that is constructed by updating <see cref="NavigationManager.Uri"/> with a single parameter
        /// added, updated, or removed.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="name">The name of the parameter to add or update.</param>
        /// <param name="value">The value of the parameter to add or update.</param>
        /// <remarks>
        /// If <paramref name="value"/> is <c>null</c>, the parameter will be removed if it exists in the URI.
        /// Otherwise, it will be added or updated.
        /// </remarks>
        public static string UriWithQueryParameter(this NavigationManager navigationManager, string name, string? value)
        {
            if (navigationManager is null)
            {
                throw new ArgumentNullException(nameof(navigationManager));
            }

            if (string.IsNullOrEmpty(name))
            {
                throw new InvalidOperationException(EmptyQueryParameterNameExceptionMessage);
            }

            var uri = navigationManager.Uri;

            return value is null
                ? UriWithoutQueryParameter(uri, name)
                : UriWithQueryParameterCore(uri, name, value);
        }

        private static string UriWithQueryParameter<TValue>(
            this NavigationManager navigationManager,
            string name,
            IEnumerable<TValue?> values,
            QueryParameterFormatter<TValue> formatter)
        {
            if (navigationManager is null)
            {
                throw new ArgumentNullException(nameof(navigationManager));
            }

            var uri = navigationManager.Uri;
            var source = new QueryParameterSource<TValue>(name, values, formatter);

            if (!TryRebuildExistingQueryFromUri(uri, out var existingQueryStringEnumerable, out var newQueryStringBuilder))
            {
                return UriWithAppendedQueryParameters(uri, ref source);
            }

            foreach (var pair in existingQueryStringEnumerable)
            {
                if (pair.EncodedName.Span.Equals(source.EncodedName, StringComparison.OrdinalIgnoreCase))
                {
                    source.AppendNextParameter(ref newQueryStringBuilder);
                }
                else
                {
                    newQueryStringBuilder.AppendParameter(pair.EncodedName.Span, pair.EncodedValue.Span);
                }
            }

            while (source.AppendNextParameter(ref newQueryStringBuilder)) ;

            return newQueryStringBuilder.UriWithQueryString;
        }

        private static string UriWithQueryParameterCore(string uri, string name, string value)
        {
            var encodedName = Uri.EscapeDataString(name);
            var encodedValue = Uri.EscapeDataString(value);

            if (!TryRebuildExistingQueryFromUri(uri, out var existingQueryStringEnumerable, out var newQueryStringBuilder))
            {
                // There was no existing query, so we can generate the new URI immediately.
                return $"{uri}?{encodedName}={encodedValue}";
            }

            var didReplace = false;
            foreach (var pair in existingQueryStringEnumerable)
            {
                if (pair.EncodedName.Span.Equals(encodedName, StringComparison.OrdinalIgnoreCase))
                {
                    didReplace = true;
                    newQueryStringBuilder.AppendParameter(encodedName, encodedValue);
                }
                else
                {
                    newQueryStringBuilder.AppendParameter(pair.EncodedName.Span, pair.EncodedValue.Span);
                }
            }

            // If there was no matching parameter, add it to the end of the query.
            if (!didReplace)
            {
                newQueryStringBuilder.AppendParameter(encodedName, encodedValue);
            }

            return newQueryStringBuilder.UriWithQueryString;
        }

        private static string UriWithoutQueryParameter(string uri, string name)
        {
            if (!TryRebuildExistingQueryFromUri(uri, out var existingQueryStringEnumerable, out var newQueryStringBuilder))
            {
                // There was no existing query, so the URI remains unchanged.
                return uri;
            }

            var encodedName = Uri.EscapeDataString(name);

            // Rebuild the query omitting parameters with a matching name.
            foreach (var pair in existingQueryStringEnumerable)
            {
                if (!pair.EncodedName.Span.Equals(encodedName, StringComparison.OrdinalIgnoreCase))
                {
                    newQueryStringBuilder.AppendParameter(pair.EncodedName.Span, pair.EncodedValue.Span);
                }
            }

            return newQueryStringBuilder.UriWithQueryString;
        }

        /// <summary>
        /// Returns a URI constructed from <see cref="NavigationManager.Uri"/> with multiple parameters
        /// added, updated, or removed.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="parameters">The values to add, update, or remove.</param>
        public static string UriWithQueryParameters(
            this NavigationManager navigationManager,
            IReadOnlyDictionary<string, object?> parameters)
            => UriWithQueryParameters(navigationManager, navigationManager.Uri, parameters);

        /// <summary>
        /// Returns a URI constructed from <paramref name="uri"/> except with multiple parameters
        /// added, updated, or removed.
        /// </summary>
        /// <param name="navigationManager">The <see cref="NavigationManager"/>.</param>
        /// <param name="uri">The URI with the query to modify.</param>
        /// <param name="parameters">The values to add, update, or remove.</param>
        public static string UriWithQueryParameters(
            this NavigationManager navigationManager,
            string uri,
            IReadOnlyDictionary<string, object?> parameters)
        {
            if (navigationManager is null)
            {
                throw new ArgumentNullException(nameof(navigationManager));
            }

            if (uri is null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            if (!TryRebuildExistingQueryFromUri(uri, out var existingQueryStringEnumerable, out var newQueryStringBuilder))
            {
                // There was no existing query, so there is no need to allocate a new dictionary to cache
                // encoded parameter values and track which parameters have been added.
                return UriWithAppendedQueryParameters(uri, parameters);
            }

            var parameterSourceCollection = CreateParameterSourceDictionary(parameters);

            // Rebuild the query, updating or removing parameters.
            foreach (var pair in existingQueryStringEnumerable)
            {
                if (parameterSourceCollection.TryGetValue(pair.EncodedName, out var source))
                {
                    if (source.AppendNextParameter(ref newQueryStringBuilder))
                    {
                        // We need to add the parameter source back into the dictionary since we're working on a copy.
                        parameterSourceCollection[pair.EncodedName] = source;
                    }
                }
                else
                {
                    newQueryStringBuilder.AppendParameter(pair.EncodedName.Span, pair.EncodedValue.Span);
                }
            }

            // Append any parameters with non-null values that did not replace existing parameters.
            foreach (var source in parameterSourceCollection.Values)
            {
                while (source.AppendNextParameter(ref newQueryStringBuilder)) ;
            }

            return newQueryStringBuilder.UriWithQueryString;
        }

        private static string UriWithAppendedQueryParameters<TValue>(
            string uriWithoutQueryString,
            ref QueryParameterSource<TValue> queryParameterSource)
        {
            var builder = new QueryStringBuilder(uriWithoutQueryString);

            while (queryParameterSource.AppendNextParameter(ref builder)) ;

            return builder.UriWithQueryString;
        }

        private static string UriWithAppendedQueryParameters(
            string uriWithoutQueryString,
            IReadOnlyDictionary<string, object?> parameters)
        {
            var builder = new QueryStringBuilder(uriWithoutQueryString);

            foreach (var (name, value) in parameters)
            {
                var source = new QueryParameterSource(name, value);
                while (source.AppendNextParameter(ref builder)) ;
            }

            return builder.UriWithQueryString;
        }

        private static Dictionary<ReadOnlyMemory<char>, QueryParameterSource> CreateParameterSourceDictionary(
            IReadOnlyDictionary<string, object?> parameters)
        {
            var parameterSources = new Dictionary<ReadOnlyMemory<char>, QueryParameterSource>(QueryParameterNameComparer.Instance);

            foreach (var (name, value) in parameters)
            {
                var parameterSource = new QueryParameterSource(name, value);
                parameterSources.Add(parameterSource.EncodedName.AsMemory(), parameterSource);
            }

            return parameterSources;
        }

        private static QueryParameterFormatter<object> GetFormatterFromParameterValueType(Type parameterValueType)
        {
            var underlyingParameterValueType = Nullable.GetUnderlyingType(parameterValueType) ?? parameterValueType;

            if (!_queryParameterFormatters.TryGetValue(underlyingParameterValueType, out var formatter))
            {
                throw new InvalidOperationException(
                    $"Cannot format query parameters with values of type '{underlyingParameterValueType}'.");
            }

            return formatter;
        }

        private static bool TryRebuildExistingQueryFromUri(
            string uri,
            out QueryStringEnumerable existingQueryStringEnumerable,
            out QueryStringBuilder newQueryStringBuilder)
        {
            var queryStartIndex = uri.IndexOf('?');

            if (queryStartIndex < 0)
            {
                existingQueryStringEnumerable = default;
                newQueryStringBuilder = default;
                return false;
            }

            var query = uri.AsMemory(queryStartIndex);
            existingQueryStringEnumerable = new(query);

            var uriWithoutQueryString = uri.AsSpan(0, queryStartIndex);
            newQueryStringBuilder = new(uriWithoutQueryString, query.Length);

            return true;
        }
    }
}
