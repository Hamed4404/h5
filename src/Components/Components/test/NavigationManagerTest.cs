// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Xunit;

namespace Microsoft.AspNetCore.Components
{
    public class NavigationManagerTest
    {
        [Theory]
        [InlineData("scheme://host/", "scheme://host/")]
        [InlineData("scheme://host:123/", "scheme://host:123/")]
        [InlineData("scheme://host/path", "scheme://host/")]
        [InlineData("scheme://host/path/", "scheme://host/path/")]
        [InlineData("scheme://host/path/page?query=string&another=here", "scheme://host/path/")]
        public void ComputesCorrectBaseUri(string baseUri, string expectedResult)
        {
            var actualResult = NavigationManager.NormalizeBaseUri(baseUri);
            Assert.Equal(expectedResult, actualResult);
        }

        [Theory]
        [InlineData("scheme://host/", "scheme://host", "")]
        [InlineData("scheme://host/", "scheme://host/", "")]
        [InlineData("scheme://host/", "scheme://host/path", "path")]
        [InlineData("scheme://host/path/", "scheme://host/path/", "")]
        [InlineData("scheme://host/path/", "scheme://host/path/more", "more")]
        [InlineData("scheme://host/path/", "scheme://host/path", "")]
        [InlineData("scheme://host/path/", "scheme://host/path#hash", "#hash")]
        [InlineData("scheme://host/path/", "scheme://host/path/#hash", "#hash")]
        [InlineData("scheme://host/path/", "scheme://host/path/more#hash", "more#hash")]
        public void ComputesCorrectValidBaseRelativePaths(string baseUri, string uri, string expectedResult)
        {
            var navigationManager = new TestNavigationManager(baseUri);

            var actualResult = navigationManager.ToBaseRelativePath(uri);
            Assert.Equal(expectedResult, actualResult);
        }

        [Theory]
        [InlineData("scheme://host/", "otherscheme://host/")]
        [InlineData("scheme://host/", "scheme://otherhost/")]
        [InlineData("scheme://host/path/", "scheme://host/")]
        public void Initialize_ThrowsForInvalidBaseRelativePaths(string baseUri, string absoluteUri)
        {
            var navigationManager = new TestNavigationManager();

            var ex = Assert.Throws<ArgumentException>(() =>
            {
                navigationManager.Initialize(baseUri, absoluteUri);
            });

            Assert.Equal(
                $"The URI '{absoluteUri}' is not contained by the base URI '{baseUri}'.",
                ex.Message);
        }

        [Theory]
        [InlineData("scheme://host/", "otherscheme://host/")]
        [InlineData("scheme://host/", "scheme://otherhost/")]
        [InlineData("scheme://host/path/", "scheme://host/")]
        public void Uri_ThrowsForInvalidBaseRelativePaths(string baseUri, string absoluteUri)
        {
            var navigationManager = new TestNavigationManager(baseUri);

            var ex = Assert.Throws<ArgumentException>(() =>
            {
                navigationManager.ToBaseRelativePath(absoluteUri);
            });

            Assert.Equal(
                $"The URI '{absoluteUri}' is not contained by the base URI '{baseUri}'.",
                ex.Message);
        }

        [Theory]
        [InlineData("scheme://host/", "otherscheme://host/")]
        [InlineData("scheme://host/", "scheme://otherhost/")]
        [InlineData("scheme://host/path/", "scheme://host/")]
        public void ToBaseRelativePath_ThrowsForInvalidBaseRelativePaths(string baseUri, string absoluteUri)
        {
            var navigationManager = new TestNavigationManager(baseUri);

            var ex = Assert.Throws<ArgumentException>(() =>
            {
                navigationManager.ToBaseRelativePath(absoluteUri);
            });

            Assert.Equal(
                $"The URI '{absoluteUri}' is not contained by the base URI '{baseUri}'.",
                ex.Message);
        }

        [Theory]
        [InlineData("scheme://host/?full%20name=Bob%20Joe&age=42", "scheme://host/?full%20name=John%20Doe&age=42")]
        [InlineData("scheme://host/?fUlL%20nAmE=Bob%20Joe&AgE=42", "scheme://host/?full%20name=John%20Doe&AgE=42")]
        [InlineData("scheme://host/?full%20name=Sally%20Smith&age=42&full%20name=Emily", "scheme://host/?full%20name=John%20Doe&age=42&full%20name=John%20Doe")]
        [InlineData("scheme://host/?full%20name=&age=42", "scheme://host/?full%20name=John%20Doe&age=42")]
        [InlineData("scheme://host/?full%20name=", "scheme://host/?full%20name=John%20Doe")]
        public void UriWithQueryParameter_ReplacesWhenParameterExists(string baseUri, string expectedUri)
        {
            var navigationManager = new TestNavigationManager(baseUri);
            var actualUri = navigationManager.UriWithQueryParameter("full name", "John Doe");

            Assert.Equal(expectedUri, actualUri);
        }

        [Theory]
        [InlineData("scheme://host/?age=42", "scheme://host/?age=42&name=John%20Doe")]
        [InlineData("scheme://host/", "scheme://host/?name=John%20Doe")]
        [InlineData("scheme://host/?", "scheme://host/?name=John%20Doe")]
        public void UriWithQueryParameter_AppendsWhenParameterDoesNotExist(string baseUri, string expectedUri)
        {
            var navigationManager = new TestNavigationManager(baseUri);
            var actualUri = navigationManager.UriWithQueryParameter("name", "John Doe");

            Assert.Equal(expectedUri, actualUri);
        }

        [Theory]
        [InlineData("scheme://host/?full%20name=Bob%20Joe&age=42", "scheme://host/?age=42")]
        [InlineData("scheme://host/?full%20name=Sally%Smith&age=42&full%20name=Emily%20Karlsen", "scheme://host/?age=42")]
        [InlineData("scheme://host/?full%20name=Sally%Smith&age=42&FuLl%20NaMe=Emily%20Karlsen", "scheme://host/?age=42")]
        [InlineData("scheme://host/?full%20name=&age=42", "scheme://host/?age=42")]
        [InlineData("scheme://host/?full%20name=", "scheme://host/")]
        [InlineData("scheme://host/", "scheme://host/")]
        public void UriWithQueryParameter_RemovesWhenParameterValueIsNull(string baseUri, string expectedUri)
        {
            var navigationManager = new TestNavigationManager(baseUri);
            var actualUri = navigationManager.UriWithQueryParameter("full name", (string)null);

            Assert.Equal(expectedUri, actualUri);
        }

        [Theory]
        [InlineData("")]
        [InlineData((string)null)]
        public void UriWithQueryParameter_ThrowsWhenNameIsNullOrEmpty(string name)
        {
            var baseUri = "scheme://host/";
            var navigationManager = new TestNavigationManager(baseUri);

            var exception = Assert.Throws<InvalidOperationException>(() => navigationManager.UriWithQueryParameter(name, "test"));
            Assert.StartsWith("Cannot have empty query parameter names.", exception.Message);
        }

        [Theory]
        [InlineData("scheme://host/?search=rugs&item%20filter=price%3Ahigh", "scheme://host/?search=rugs&item%20filter=price%3Alow&item%20filter=shipping%3Afree&item%20filter=category%3Arug")]
        [InlineData("scheme://host/?item%20filter=price%3Ahigh&search=rugs&item%20filter=shipping%3A2day", "scheme://host/?item%20filter=price%3Alow&search=rugs&item%20filter=shipping%3Afree&item%20filter=category%3Arug")]
        [InlineData("scheme://host/?item%20filter=price&item%20filter=shipping%3A2day&item%20filter=category%3Arug&item%20filter=availability%3Atoday", "scheme://host/?item%20filter=price%3Alow&item%20filter=shipping%3Afree&item%20filter=category%3Arug")]
        [InlineData("scheme://host/?item%20filter=price&iTeM%20fIlTeR=shipping%3A2day&item%20filter=category%3Arug&ItEm%20FiLtEr=availability%3Atoday", "scheme://host/?item%20filter=price%3Alow&item%20filter=shipping%3Afree&item%20filter=category%3Arug")]
        [InlineData("scheme://host/", "scheme://host/?item%20filter=price%3Alow&item%20filter=shipping%3Afree&item%20filter=category%3Arug")]
        public void UriWithQueryParameterOfTValue_ReplacesExistingQueryParameters(string baseUri, string expectedUri)
        {
            var navigationManager = new TestNavigationManager(baseUri);
            var actualUri = navigationManager.UriWithQueryParameter("item filter", new string[]
            {
                "price:low",
                "shipping:free",
                "category:rug",
            });

            Assert.Equal(expectedUri, actualUri);
        }

        [Theory]
        [InlineData("scheme://host/?search=rugs&items=8&items=42", "scheme://host/?search=rugs&items=5&items=13")]
        [InlineData("scheme://host/", "scheme://host/?items=5&items=13")]
        public void UriWithQueryParameterOfTValue_SkipsNullValues(string baseUri, string expectedUri)
        {
            var navigationManager = new TestNavigationManager(baseUri);
            var actualUri = navigationManager.UriWithQueryParameter("items", new int?[]
            {
                5,
                null,
                13,
            });

            Assert.Equal(expectedUri, actualUri);
        }

        [Theory]
        [InlineData("")]
        [InlineData((string)null)]
        public void UriWithQueryParameterOfTValue_ThrowsWhenNameIsNullOrEmpty(string name)
        {
            var baseUri = "scheme://host/";
            var navigationManager = new TestNavigationManager(baseUri);
            var values = new string[] { "test" };

            var exception = Assert.Throws<InvalidOperationException>(() => navigationManager.UriWithQueryParameter(name, values));
            Assert.StartsWith("Cannot have empty query parameter names.", exception.Message);
        }

        [Theory]
        [InlineData("scheme://host/?name=Bob%20Joe&age=42", "scheme://host/?age=25&eye%20color=green")]
        [InlineData("scheme://host/?NaMe=Bob%20Joe&AgE=42", "scheme://host/?age=25&eye%20color=green")]
        [InlineData("scheme://host/?name=Bob%20Joe&age=42&keepme=true", "scheme://host/?age=25&keepme=true&eye%20color=green")]
        [InlineData("scheme://host/?age=42&eye%20color=87", "scheme://host/?age=25&eye%20color=green")]
        [InlineData("scheme://host/?", "scheme://host/?age=25&eye%20color=green")]
        [InlineData("scheme://host/", "scheme://host/?age=25&eye%20color=green")]
        public void UriWithQueryParameters_CanAddUpdateAndRemove(string baseUri, string expectedUri)
        {
            var navigationManager = new TestNavigationManager(baseUri);
            var actualUri = navigationManager.UriWithQueryParameters(new Dictionary<string, object>
            {
                ["name"] = null,        // Remove
                ["age"] = (int?)25,     // Add/update
                ["eye color"] = "green",// Add/update
            });

            Assert.Equal(expectedUri, actualUri);
        }

        [Theory]
        [InlineData("scheme://host/?full%20name=Bob%20Joe&ping=8&ping=300", "scheme://host/?full%20name=John%20Doe&ping=35&ping=16&ping=87&ping=240")]
        [InlineData("scheme://host/?ping=8&full%20name=Bob%20Joe&ping=300", "scheme://host/?ping=35&full%20name=John%20Doe&ping=16&ping=87&ping=240")]
        [InlineData("scheme://host/?ping=8&ping=300&ping=50&ping=68&ping=42", "scheme://host/?ping=35&ping=16&ping=87&ping=240&full%20name=John%20Doe")]
        public void UriWithQueryParameters_SupportsEnumerableValues(string baseUri, string expectedUri)
        {
            var navigationManager = new TestNavigationManager(baseUri);
            var actualUri = navigationManager.UriWithQueryParameters(new Dictionary<string, object>
            {
                ["full name"] = "John Doe", // Single value
                ["ping"] = new int?[] { 35, 16, null, 87, 240 }
            });

            Assert.Equal(expectedUri, actualUri);
        }

        [Fact]
        public void UriWithQueryParameters_ThrowsWhenParameterValueTypeIsUnsupported()
        {
            var baseUri = "scheme://host/";
            var navigationManager = new TestNavigationManager(baseUri);
            var unsupportedParameterValues = new Dictionary<string, object>
            {
                ["value"] = new { Value = 3 }
            };

            var exception = Assert.Throws<InvalidOperationException>(() => navigationManager.UriWithQueryParameters(unsupportedParameterValues));
            Assert.StartsWith("Cannot format query parameters with values of type", exception.Message);
        }

        [Theory]
        [InlineData("scheme://host/")]
        [InlineData("scheme://host/?existing-param=test")]
        public void UriWithQueryParameters_ThrowsWhenAnyParameterNameIsEmpty(string baseUri)
        {
            var navigationManager = new TestNavigationManager(baseUri);
            var values = new Dictionary<string, object>
            {
                ["name1"] = "value1",
                [string.Empty] = "value2",
            };

            var exception = Assert.Throws<InvalidOperationException>(() => navigationManager.UriWithQueryParameters(values));
            Assert.StartsWith("Cannot have empty query parameter names.", exception.Message);
        }

        private class TestNavigationManager : NavigationManager
        {
            public TestNavigationManager()
            {
            }

            public TestNavigationManager(string baseUri = null, string uri = null)
            {
                Initialize(baseUri ?? "http://example.com/", uri ?? baseUri ?? "http://example.com/welcome-page");
            }

            public new void Initialize(string baseUri, string uri)
            {
                base.Initialize(baseUri, uri);
            }

            protected override void NavigateToCore(string uri, bool forceLoad)
            {
                throw new System.NotImplementedException();
            }
        }
    }
}
