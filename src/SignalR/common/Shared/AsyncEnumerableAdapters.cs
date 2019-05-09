// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.SignalR.Internal
{
    // True-internal because this is a weird and tricky class to use :)
    internal static class AsyncEnumerableAdapters
    {
#if NETCOREAPP3_0
        public static IAsyncEnumerable<object> MakeCancelableAsyncEnumerable<T>(IAsyncEnumerable<T> asyncEnumerable, CancellationToken cancellationToken = default)
        {
            return new CancelableAsyncEnumerable<T>(asyncEnumerable, cancellationToken);
        }

        public static IAsyncEnumerable<T> MakeCancelableTypedAsyncEnumerable<T>(IAsyncEnumerable<T> asyncEnumerable, CancellationTokenSource cts)
        {
            return new CancelableTypedAsyncEnumerable<T>(asyncEnumerable, cts);
        }

        public static async IAsyncEnumerable<object> MakeAsyncEnumerableFromChannel<T>(ChannelReader<T> channel, CancellationToken cancellationToken = default)
        {
            await foreach (var item in channel.ReadAllAsync(cancellationToken))
            {
                yield return item;
            }
        }

        private class CancelableTypedAsyncEnumerable<TResult> : IAsyncEnumerable<TResult>
        {
            private readonly IAsyncEnumerable<TResult> _asyncEnumerable;
            private readonly CancellationTokenSource _cts;

            public CancelableTypedAsyncEnumerable(IAsyncEnumerable<TResult> asyncEnumerable, CancellationTokenSource cts)
            {
                _asyncEnumerable = asyncEnumerable;
                _cts = cts;
            }

            public IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                var enumerator = _asyncEnumerable.GetAsyncEnumerator();
                if (cancellationToken.CanBeCanceled)
                {
                    var registration = cancellationToken.Register((ctsState) =>
                    {
                        ((CancellationTokenSource)ctsState).Cancel();
                    }, _cts);
                }

                return enumerator;
            }
        }

        /// <summary>Converts an IAsyncEnumerable of T to an IAsyncEnumerable of object.</summary>
        private class CancelableAsyncEnumerable<T> : IAsyncEnumerable<object>
        {
            private readonly IAsyncEnumerable<T> _asyncEnumerable;
            private readonly CancellationToken _cancellationToken;

            public CancelableAsyncEnumerable(IAsyncEnumerable<T> asyncEnumerable, CancellationToken cancellationToken)
            {
                _asyncEnumerable = asyncEnumerable;
                _cancellationToken = cancellationToken;
            }

            public IAsyncEnumerator<object> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                var enumeratorOfT = _asyncEnumerable.GetAsyncEnumerator(_cancellationToken);
                return enumeratorOfT as IAsyncEnumerator<object> ?? new BoxedAsyncEnumerator(enumeratorOfT);
            }

            private class BoxedAsyncEnumerator : IAsyncEnumerator<object>
            {
                private IAsyncEnumerator<T> _asyncEnumerator;

                public BoxedAsyncEnumerator(IAsyncEnumerator<T> asyncEnumerator)
                {
                    _asyncEnumerator = asyncEnumerator;
                }

                public object Current => _asyncEnumerator.Current;

                public ValueTask<bool> MoveNextAsync()
                {
                    return _asyncEnumerator.MoveNextAsync();
                }

                public ValueTask DisposeAsync()
                {
                    return _asyncEnumerator.DisposeAsync();
                }
            }
        }
#endif
    }
}
