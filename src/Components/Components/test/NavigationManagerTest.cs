// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.AspNetCore.Components.Routing;

namespace Microsoft.AspNetCore.Components;

public class NavigationManagerTest
{
    // Nothing should exceed the timeout in a successful run of the the tests, this is just here to catch
    // failures.
    private static readonly TimeSpan Timeout = Debugger.IsAttached ? System.Threading.Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(10);

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
    public void GetUriWithQueryParameter_ReplacesWhenParameterExists(string baseUri, string expectedUri)
    {
        var navigationManager = new TestNavigationManager(baseUri);
        var actualUri = navigationManager.GetUriWithQueryParameter("full name", "John Doe");

        Assert.Equal(expectedUri, actualUri);
    }

    [Theory]
    [InlineData("scheme://host/?age=42", "scheme://host/?age=42&name=John%20Doe")]
    [InlineData("scheme://host/", "scheme://host/?name=John%20Doe")]
    [InlineData("scheme://host/?", "scheme://host/?name=John%20Doe")]
    public void GetUriWithQueryParameter_AppendsWhenParameterDoesNotExist(string baseUri, string expectedUri)
    {
        var navigationManager = new TestNavigationManager(baseUri);
        var actualUri = navigationManager.GetUriWithQueryParameter("name", "John Doe");

        Assert.Equal(expectedUri, actualUri);
    }

    [Theory]
    [InlineData("scheme://host/?full%20name=Bob%20Joe&age=42", "scheme://host/?age=42")]
    [InlineData("scheme://host/?full%20name=Sally%Smith&age=42&full%20name=Emily%20Karlsen", "scheme://host/?age=42")]
    [InlineData("scheme://host/?full%20name=Sally%Smith&age=42&FuLl%20NaMe=Emily%20Karlsen", "scheme://host/?age=42")]
    [InlineData("scheme://host/?full%20name=&age=42", "scheme://host/?age=42")]
    [InlineData("scheme://host/?full%20name=", "scheme://host/")]
    [InlineData("scheme://host/", "scheme://host/")]
    public void GetUriWithQueryParameter_RemovesWhenParameterValueIsNull(string baseUri, string expectedUri)
    {
        var navigationManager = new TestNavigationManager(baseUri);
        var actualUri = navigationManager.GetUriWithQueryParameter("full name", (string)null);

        Assert.Equal(expectedUri, actualUri);
    }

    [Theory]
    [InlineData("")]
    [InlineData((string)null)]
    public void GetUriWithQueryParameter_ThrowsWhenNameIsNullOrEmpty(string name)
    {
        var baseUri = "scheme://host/";
        var navigationManager = new TestNavigationManager(baseUri);

        var exception = Assert.Throws<InvalidOperationException>(() => navigationManager.GetUriWithQueryParameter(name, "test"));
        Assert.StartsWith("Cannot have empty query parameter names.", exception.Message);
    }

    [Theory]
    [InlineData("scheme://host/?name=Bob%20Joe&age=42", "scheme://host/?age=25&eye%20color=green")]
    [InlineData("scheme://host/?NaMe=Bob%20Joe&AgE=42", "scheme://host/?age=25&eye%20color=green")]
    [InlineData("scheme://host/?name=Bob%20Joe&age=42&keepme=true", "scheme://host/?age=25&keepme=true&eye%20color=green")]
    [InlineData("scheme://host/?age=42&eye%20color=87", "scheme://host/?age=25&eye%20color=green")]
    [InlineData("scheme://host/?", "scheme://host/?age=25&eye%20color=green")]
    [InlineData("scheme://host/", "scheme://host/?age=25&eye%20color=green")]
    public void GetUriWithQueryParameters_CanAddUpdateAndRemove(string baseUri, string expectedUri)
    {
        var navigationManager = new TestNavigationManager(baseUri);
        var actualUri = navigationManager.GetUriWithQueryParameters(new Dictionary<string, object>
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
    public void GetUriWithQueryParameters_SupportsEnumerableValues(string baseUri, string expectedUri)
    {
        var navigationManager = new TestNavigationManager(baseUri);
        var actualUri = navigationManager.GetUriWithQueryParameters(new Dictionary<string, object>
        {
            ["full name"] = "John Doe", // Single value
            ["ping"] = new int?[] { 35, 16, null, 87, 240 }
        });

        Assert.Equal(expectedUri, actualUri);
    }

    [Fact]
    public void GetUriWithQueryParameters_ThrowsWhenParameterValueTypeIsUnsupported()
    {
        var baseUri = "scheme://host/";
        var navigationManager = new TestNavigationManager(baseUri);
        var unsupportedParameterValues = new Dictionary<string, object>
        {
            ["value"] = new { Value = 3 }
        };

        var exception = Assert.Throws<InvalidOperationException>(() => navigationManager.GetUriWithQueryParameters(unsupportedParameterValues));
        Assert.StartsWith("Cannot format query parameters with values of type", exception.Message);
    }

    [Theory]
    [InlineData("scheme://host/")]
    [InlineData("scheme://host/?existing-param=test")]
    public void GetUriWithQueryParameters_ThrowsWhenAnyParameterNameIsEmpty(string baseUri)
    {
        var navigationManager = new TestNavigationManager(baseUri);
        var values = new Dictionary<string, object>
        {
            ["name1"] = "value1",
            [string.Empty] = "value2",
        };

        var exception = Assert.Throws<InvalidOperationException>(() => navigationManager.GetUriWithQueryParameters(values));
        Assert.StartsWith("Cannot have empty query parameter names.", exception.Message);
    }

    [Fact]
    public void LocationChangingHandlers_CanContinueTheNavigationSynchronously_WhenOneHandlerIsRegistered()
    {
        // Arrange
        var baseUri = "scheme://host/";
        var navigationManager = new TestNavigationManager(baseUri);
        navigationManager.AddLocationChangingHandler(HandleLocationChanging);

        // Act
        var navigation1 = navigationManager.RunNotifyfLocationChangingAsync($"{baseUri}/subdir1", null, false);

        // Assert
        Assert.True(navigation1.IsCompletedSuccessfully);
        Assert.True(navigation1.Result);

        static ValueTask HandleLocationChanging(LocationChangingContext context)
        {
            return ValueTask.CompletedTask;
        };
    }

    [Fact]
    public void LocationChangingHandlers_CanContinueTheNavigationSynchronously_WhenMultipleHandlersAreRegistered()
    {
        // Arrange
        var baseUri = "scheme://host/";
        var navigationManager = new TestNavigationManager(baseUri);
        var initialHandlerCount = 3;
        var completedHandlerCount = 0;

        // Act
        for (var i = 0; i < initialHandlerCount; i++)
        {
            navigationManager.AddLocationChangingHandler(HandleLocationChanging);
        }

        var navigation1 = navigationManager.RunNotifyfLocationChangingAsync($"{baseUri}/subdir1", null, false);

        // Assert
        Assert.True(navigation1.IsCompletedSuccessfully);
        Assert.True(navigation1.Result);
        Assert.Equal(initialHandlerCount, completedHandlerCount);

        ValueTask HandleLocationChanging(LocationChangingContext context)
        {
            completedHandlerCount++;
            return ValueTask.CompletedTask;
        };
    }

    [Fact]
    public async Task LocationChangingHandlers_CanContinueTheNavigationAsynchronously_WhenOneHandlerIsRegistered()
    {
        // Arrange
        var baseUri = "scheme://host/";
        var navigationManager = new TestNavigationManager(baseUri);
        navigationManager.AddLocationChangingHandler(HandleLocationChanging);

        // Act
        var navigation1 = navigationManager.RunNotifyfLocationChangingAsync($"{baseUri}/subdir1", null, false);
        var navigation1Result = await navigation1.WaitAsync(Timeout);

        // Assert
        Assert.True(navigation1.IsCompletedSuccessfully);
        Assert.True(navigation1Result);

        static async ValueTask HandleLocationChanging(LocationChangingContext context)
        {
            await Task.Delay(1000);
        };
    }

    [Fact]
    public async Task LocationChangingHandlers_CanContinueTheNavigationAsynchronously_WhenMultipleHandlersAreRegistered()
    {
        // Arrange
        var baseUri = "scheme://host/";
        var navigationManager = new TestNavigationManager(baseUri);
        var initialHandlerCount = 3;
        var completedHandlerCount = 0;

        // Act
        for (var i = 0; i < initialHandlerCount; i++)
        {
            navigationManager.AddLocationChangingHandler(HandleLocationChanging);
        }

        var navigation1 = navigationManager.RunNotifyfLocationChangingAsync($"{baseUri}/subdir1", null, false);
        var navigation1Result = await navigation1.WaitAsync(Timeout);

        // Assert
        Assert.True(navigation1.IsCompletedSuccessfully);
        Assert.True(navigation1Result);
        Assert.Equal(initialHandlerCount, completedHandlerCount);

        async ValueTask HandleLocationChanging(LocationChangingContext context)
        {
            await Task.Delay(1000);
            Interlocked.Increment(ref completedHandlerCount);
        };
    }

    [Fact]
    public void LocationChangingHandlers_CanCancelTheNavigationSynchronously_WhenOneHandlerIsRegistered()
    {
        // Arrange
        var baseUri = "scheme://host/";
        var navigationManager = new TestNavigationManager(baseUri);

        navigationManager.AddLocationChangingHandler(HandleLocationChanging);

        // Act
        var navigation1 = navigationManager.RunNotifyfLocationChangingAsync($"{baseUri}/subdir1", null, false);

        // Assert
        Assert.True(navigation1.IsCompletedSuccessfully);
        Assert.False(navigation1.Result);

        static ValueTask HandleLocationChanging(LocationChangingContext context)
        {
            context.PreventNavigation();
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public void LocationChangingHandlers_CanCancelTheNavigationSynchronously_WhenMultipleHandlersAreRegistered()
    {
        // Arrange
        var baseUri = "scheme://host/";
        var navigationManager = new TestNavigationManager(baseUri);

        navigationManager.AddLocationChangingHandler(HandleLocationChanging_AllowNavigation);
        navigationManager.AddLocationChangingHandler(HandleLocationChanging_AllowNavigation);
        navigationManager.AddLocationChangingHandler(HandleLocationChanging_PreventNavigation);

        // Act
        var navigation1 = navigationManager.RunNotifyfLocationChangingAsync($"{baseUri}/subdir1", null, false);

        // Assert
        Assert.True(navigation1.IsCompletedSuccessfully);
        Assert.False(navigation1.Result);

        static ValueTask HandleLocationChanging_AllowNavigation(LocationChangingContext context)
            => ValueTask.CompletedTask;

        static ValueTask HandleLocationChanging_PreventNavigation(LocationChangingContext context)
        {
            context.PreventNavigation();
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async void LocationChangingHandlers_CanCancelTheNavigationAsynchronously_WhenOneHandlerIsRegistered()
    {
        // Arrange
        var baseUri = "scheme://host/";
        var navigationManager = new TestNavigationManager(baseUri);

        navigationManager.AddLocationChangingHandler(HandleLocationChanging);

        // Act
        var navigation1 = navigationManager.RunNotifyfLocationChangingAsync($"{baseUri}/subdir1", null, false);
        var navigation1Result = await navigation1.WaitAsync(Timeout);

        // Assert
        Assert.True(navigation1.IsCompletedSuccessfully);
        Assert.False(navigation1Result);

        static async ValueTask HandleLocationChanging(LocationChangingContext context)
        {
            await Task.Delay(1000);
            context.PreventNavigation();
        }
    }

    [Fact]
    public async void LocationChangingHandlers_CanCancelTheNavigationAsynchronously_WhenMultipleHandlersAreRegistered()
    {
        // Arrange
        var baseUri = "scheme://host/";
        var navigationManager = new TestNavigationManager(baseUri);
        var blockNavigationHandlerCount = 2;
        var canceledBlockNavigationHandlerCount = 0;
        var tcs = new TaskCompletionSource();

        for (var i = 0; i < blockNavigationHandlerCount; i++)
        {
            navigationManager.AddLocationChangingHandler(HandleLocationChanging_BlockNavigation);
        }

        navigationManager.AddLocationChangingHandler(HandleLocationChanging_PreventNavigation);

        // Act
        var navigation1 = navigationManager.RunNotifyfLocationChangingAsync($"{baseUri}/subdir1", null, false);
        var navigation1Result = await navigation1.WaitAsync(Timeout);

        await tcs.Task.WaitAsync(Timeout);

        // Assert
        Assert.True(navigation1.IsCompletedSuccessfully);
        Assert.False(navigation1.Result);
        Assert.Equal(blockNavigationHandlerCount, canceledBlockNavigationHandlerCount);

        async ValueTask HandleLocationChanging_BlockNavigation(LocationChangingContext context)
        {
            try
            {
                await Task.Delay(System.Threading.Timeout.Infinite, context.CancellationToken);
            }
            catch (TaskCanceledException ex)
            {
                if (ex.CancellationToken == context.CancellationToken)
                {
                    lock (navigationManager)
                    {
                        canceledBlockNavigationHandlerCount++;

                        if (canceledBlockNavigationHandlerCount == blockNavigationHandlerCount)
                        {
                            tcs.SetResult();
                        }
                    }
                }
            }
        }

        static async ValueTask HandleLocationChanging_PreventNavigation(LocationChangingContext context)
        {
            await Task.Delay(1000);
            context.PreventNavigation();
        }
    }

    [Fact]
    public async Task LocationChangingHandlers_AreCanceledBySuccessiveNavigations_WhenOneHandlerIsRegistered()
    {
        // Arrange
        var baseUri = "scheme://host/";
        var navigationManager = new TestNavigationManager(baseUri);
        var canceledHandlerTaskIds = new HashSet<string>();
        var tcs = new TaskCompletionSource();

        // Act
        navigationManager.AddLocationChangingHandler(HandleLocationChanging);
        var navigation1 = navigationManager.RunNotifyfLocationChangingAsync($"{baseUri}/subdir1", null, false);
        navigationManager.RemoveLocationChangingHandler(HandleLocationChanging);
        var navigation2 = navigationManager.RunNotifyfLocationChangingAsync($"{baseUri}/subdir2", null, false);

        await tcs.Task.WaitAsync(Timeout);

        // Assert
        Assert.True(navigation1.IsCompletedSuccessfully);
        Assert.False(navigation1.Result);

        Assert.True(navigation2.IsCompletedSuccessfully);
        Assert.True(navigation2.Result);

        async ValueTask HandleLocationChanging(LocationChangingContext context)
        {
            try
            {
                await Task.Delay(System.Threading.Timeout.Infinite, context.CancellationToken);
            }
            catch (TaskCanceledException e)
            {
                if (e.CancellationToken == context.CancellationToken)
                {
                    tcs.SetResult();
                }

                throw;
            }
        };
    }

    [Fact]
    public async Task LocationChangingHandlers_AreCanceledBySuccessiveNavigations_WhenMultipleHandlersAreRegistered()
    {
        // Arrange
        var baseUri = "scheme://host/";
        var navigationManager = new TestNavigationManager(baseUri);
        var canceledHandlerTaskIds = new HashSet<string>();
        var initialHandlerCount = 3;
        var expectedCanceledHandlerCount = 6; // 3 handlers canceled 2 times
        var canceledHandlerCount = 0;
        var completedHandlerCount = 0;
        var tcs = new TaskCompletionSource();

        // Act
        for (var i = 0; i < initialHandlerCount; i++)
        {
            navigationManager.AddLocationChangingHandler(HandleLocationChanging);
        }

        // These two navigations get canceled
        var navigation1 = navigationManager.RunNotifyfLocationChangingAsync($"{baseUri}/subdir1", null, false);
        var navigation2 = navigationManager.RunNotifyfLocationChangingAsync($"{baseUri}/subdir2", null, false);

        for (var i = 0; i < initialHandlerCount; i++)
        {
            navigationManager.RemoveLocationChangingHandler(HandleLocationChanging);
        }

        // This navigation continues without getting canceled
        var navigation3 = navigationManager.RunNotifyfLocationChangingAsync($"{baseUri}/subdir3", null, false);

        await tcs.Task.WaitAsync(Timeout);

        // Assert
        Assert.True(navigation1.IsCompletedSuccessfully);
        Assert.False(navigation1.Result);

        Assert.True(navigation2.IsCompletedSuccessfully);
        Assert.False(navigation2.Result);

        Assert.True(navigation3.IsCompletedSuccessfully);
        Assert.True(navigation3.Result);

        Assert.Equal(expectedCanceledHandlerCount, canceledHandlerCount);
        Assert.Equal(0, completedHandlerCount);

        async ValueTask HandleLocationChanging(LocationChangingContext context)
        {
            try
            {
                await Task.Delay(System.Threading.Timeout.Infinite, context.CancellationToken);
                Interlocked.Increment(ref completedHandlerCount);
            }
            catch (TaskCanceledException e)
            {
                if (e.CancellationToken == context.CancellationToken)
                {
                    lock (navigationManager)
                    {
                        canceledHandlerCount++;

                        if (canceledHandlerCount == expectedCanceledHandlerCount)
                        {
                            tcs.SetResult();
                        }
                    }
                }

                throw;
            }
        };
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

        public async Task<bool> RunNotifyfLocationChangingAsync(string uri, string state, bool isNavigationIntercepted)
            => await NotifyLocationChangingAsync(uri, state, isNavigationIntercepted);

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            throw new System.NotImplementedException();
        }

        protected override void SetNavigationLockState(bool value)
        {
        }
    }
}
