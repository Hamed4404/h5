// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets.Internal;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets
{
    /// <summary>
    /// A factory for socket based connections contexts.
    /// </summary>
    public sealed class SocketConnectionContextFactory : IDisposable
    {
        private readonly MemoryPool<byte> _memoryPool;
        private readonly SocketConnectionFactoryOptions _options;
        private readonly SocketsTrace _trace;
        private readonly int _settingsCount;
        private readonly QueueSettings[] _settings;

        // long to prevent overflow
        private long _settingsIndex;

        /// <summary>
        /// Creates the <see cref="SocketConnectionContextFactory"/>.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="logger">The logger.</param>
        public SocketConnectionContextFactory(SocketConnectionFactoryOptions options, ILogger logger)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _options = options;
            _trace = new SocketsTrace(logger);
            _memoryPool = _options.MemoryPoolFactory();
            _settingsCount = _options.IOQueueCount;

            var maxReadBufferSize = _options.MaxReadBufferSize ?? 0;
            var maxWriteBufferSize = _options.MaxWriteBufferSize ?? 0;
            var applicationScheduler = options.UnsafePreferInlineScheduling ? PipeScheduler.Inline : PipeScheduler.ThreadPool;

            if (_settingsCount > 0)
            {
                _settings = new QueueSettings[_settingsCount];

                for (var i = 0; i < _settingsCount; i++)
                {
                    var transportScheduler = options.UnsafePreferInlineScheduling ? PipeScheduler.Inline : new IOQueue();
                    // https://github.com/aspnet/KestrelHttpServer/issues/2573
                    var awaiterScheduler = OperatingSystem.IsWindows() ? transportScheduler : PipeScheduler.Inline;

                    _settings[i] = new QueueSettings()
                    {
                        Scheduler = transportScheduler,
                        InputOptions = new PipeOptions(_memoryPool, applicationScheduler, transportScheduler, maxReadBufferSize, maxReadBufferSize / 2, useSynchronizationContext: false),
                        OutputOptions = new PipeOptions(_memoryPool, transportScheduler, applicationScheduler, maxWriteBufferSize, maxWriteBufferSize / 2, useSynchronizationContext: false),
                        SocketSenderPool = new SocketSenderPool(awaiterScheduler)
                    };
                }
            }
            else
            {
                var transportScheduler = options.UnsafePreferInlineScheduling ? PipeScheduler.Inline : PipeScheduler.ThreadPool;
                // https://github.com/aspnet/KestrelHttpServer/issues/2573
                var awaiterScheduler = OperatingSystem.IsWindows() ? transportScheduler : PipeScheduler.Inline;
                _settings = new QueueSettings[]
                {
                    new QueueSettings()
                    {
                        Scheduler = transportScheduler,
                        InputOptions = new PipeOptions(_memoryPool, applicationScheduler, transportScheduler, maxReadBufferSize, maxReadBufferSize / 2, useSynchronizationContext: false),
                        OutputOptions = new PipeOptions(_memoryPool, transportScheduler, applicationScheduler, maxWriteBufferSize, maxWriteBufferSize / 2, useSynchronizationContext: false),
                        SocketSenderPool = new SocketSenderPool(awaiterScheduler)
                    }
                };
                _settingsCount = 1;
            }
        }

        /// <summary>
        /// Create a <see cref="ConnectionContext"/> for a socket.
        /// </summary>
        /// <param name="socket">The socket for the connection.</param>
        /// <returns></returns>
        public ConnectionContext Create(Socket socket)
        {
            var setting = _settings[Interlocked.Increment(ref _settingsIndex) % _settingsCount];

            var connection = new SocketConnection(socket,
                _memoryPool,
                setting.Scheduler,
                _trace,
                setting.SocketSenderPool,
                setting.InputOptions,
                setting.OutputOptions,
                waitForData: _options.WaitForDataBeforeAllocatingBuffer);

            connection.Start();
            return connection;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // Dispose the memory pool
            _memoryPool.Dispose();

            // Dispose any pooled senders
            foreach (var setting in _settings)
            {
                setting.SocketSenderPool.Dispose();
            }
        }

        private class QueueSettings
        {
            public PipeScheduler Scheduler { get; init; } = default!;
            public PipeOptions InputOptions { get; init; } = default!;
            public PipeOptions OutputOptions { get; init; } = default!;
            public SocketSenderPool SocketSenderPool { get; init; } = default!;
        }
    }
}
