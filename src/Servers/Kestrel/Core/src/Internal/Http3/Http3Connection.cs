// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Experimental;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http3
{
    internal class Http3Connection : IRequestProcessor, ITimeoutHandler
    {
        public Http3ControlStream OutboundControlStream { get; set; }

        private Http3ControlStream _inboundControlStream;
        private Http3ControlStream _inboundEncoderStream;
        private Http3ControlStream _inboundDecoderStream;

        internal readonly Dictionary<long, Http3Stream> _streams = new Dictionary<long, Http3Stream>();

        private long _highestOpenedStreamId; // TODO lock to access
        private volatile bool _haveSentGoAway;
        private readonly object _sync = new object();
        private readonly MultiplexedConnectionContext _multiplexedContext;
        private readonly Http3ConnectionContext _context;
        private readonly ISystemClock _systemClock;
        private readonly TimeoutControl _timeoutControl;
        private bool _aborted;
        private ConnectionAbortedException _abortedException = null;
        private readonly object _protocolSelectionLock = new object();
        private CancellationTokenSource _connectionAborted;

        private readonly Http3PeerSettings _serverSettings = new Http3PeerSettings();

        public Http3Connection(Http3ConnectionContext context)
        {
            _multiplexedContext = context.ConnectionContext;
            _context = context;
            _systemClock = context.ServiceContext.SystemClock;
            _timeoutControl = new TimeoutControl(this);
            _context.TimeoutControl ??= _timeoutControl;
            _connectionAborted = new CancellationTokenSource();

            var httpLimits = context.ServiceContext.ServerOptions.Limits;

            _serverSettings.HeaderTableSize = (uint)httpLimits.Http3.HeaderTableSize;
            _serverSettings.MaxRequestHeaderFieldSize = (uint)httpLimits.Http3.MaxRequestHeaderFieldSize;
        }

        internal long HighestStreamId
        {
            get
            {
                return _highestOpenedStreamId;
            }
            set
            {
                if (_highestOpenedStreamId < value)
                {
                    _highestOpenedStreamId = value;
                }
            }
        }

        private IKestrelTrace Log => _context.ServiceContext.Log;

        public async Task ProcessRequestsAsync<TContext>(IHttpApplication<TContext> httpApplication)
        {
            try
            {
                // Ensure TimeoutControl._lastTimestamp is initialized before anything that could set timeouts runs.
                _timeoutControl.Initialize(_systemClock.UtcNowTicks);

                var connectionHeartbeatFeature = _context.ConnectionFeatures.Get<IConnectionHeartbeatFeature>();
                var connectionLifetimeNotificationFeature = _context.ConnectionFeatures.Get<IConnectionLifetimeNotificationFeature>();

                // These features should never be null in Kestrel itself, if this middleware is ever refactored to run outside of kestrel,
                // we'll need to handle these missing.
                Debug.Assert(connectionHeartbeatFeature != null, nameof(IConnectionHeartbeatFeature) + " is missing!");
                Debug.Assert(connectionLifetimeNotificationFeature != null, nameof(IConnectionLifetimeNotificationFeature) + " is missing!");

                // Register the various callbacks once we're going to start processing requests

                // The heart beat for various timeouts
                connectionHeartbeatFeature?.OnHeartbeat(state => ((Http3Connection)state).Tick(), this);

                // Register for graceful shutdown of the server
                using var shutdownRegistration = connectionLifetimeNotificationFeature?.ConnectionClosedRequested.Register(state => ((Http3Connection)state).StopProcessingNextRequest(), this);

                // Register for connection close
                using var closedRegistration = _context.ConnectionContext.ConnectionClosed.Register(state => ((Http3Connection)state).OnConnectionClosed(), this);

                await InnerProcessRequestsAsync(httpApplication);
            }
            catch (Exception ex)
            {
                Log.LogCritical(0, ex, $"Unexpected exception in {nameof(Http3Connection)}.{nameof(ProcessRequestsAsync)}.");
            }
            finally
            {
            }
        }

        // For testing only
        internal void Initialize()
        {
        }

        public void StopProcessingNextRequest()
        {
            bool previousState;
            lock (_protocolSelectionLock)
            {
                previousState = _aborted;
            }

            if (!previousState)
            {
                _connectionAborted.Cancel();
            }
        }

        public void OnConnectionClosed()
        {
            bool previousState;
            lock (_protocolSelectionLock)
            {
                previousState = _aborted;
            }

            // TODO figure out how to gracefully close next requests
        }

        public void Abort(ConnectionAbortedException ex)
        {
            bool previousState;

            lock (_protocolSelectionLock)
            {
                previousState = _aborted;
                _aborted = true;
            }

            if (!previousState)
            {
                InnerAbort(ex);
            }
        }

        public void Tick()
        {
            if (_aborted)
            {
                // It's safe to check for timeouts on a dead connection,
                // but try not to in order to avoid extraneous logs.
                return;
            }

            // It's safe to use UtcNowUnsynchronized since Tick is called by the Heartbeat.
            var now = _systemClock.UtcNowUnsynchronized;
            _timeoutControl.Tick(now);
        }

        public void OnTimeout(TimeoutReason reason)
        {
            // In the cases that don't log directly here, we expect the setter of the timeout to also be the input
            // reader, so when the read is canceled or aborted, the reader should write the appropriate log.
            switch (reason)
            {
                case TimeoutReason.KeepAlive:
                    StopProcessingNextRequest();
                    break;
                case TimeoutReason.RequestHeaders:
                    HandleRequestHeadersTimeout();
                    break;
                case TimeoutReason.ReadDataRate:
                    HandleReadDataRateTimeout();
                    break;
                case TimeoutReason.WriteDataRate:
                    Log.ResponseMinimumDataRateNotSatisfied(_context.ConnectionId, "" /*TraceIdentifier*/); // TODO trace identifier.
                    Abort(new ConnectionAbortedException(CoreStrings.ConnectionTimedBecauseResponseMininumDataRateNotSatisfied));
                    break;
                case TimeoutReason.RequestBodyDrain:
                case TimeoutReason.TimeoutFeature:
                    Abort(new ConnectionAbortedException(CoreStrings.ConnectionTimedOutByServer));
                    break;
                default:
                    Debug.Assert(false, "Invalid TimeoutReason");
                    break;
            }
        }

        internal async Task InnerProcessRequestsAsync<TContext>(IHttpApplication<TContext> application)
        {
            // An endpoint MAY avoid creating an encoder stream if it's not going to
            // be used(for example if its encoder doesn't wish to use the dynamic
            // table, or if the maximum size of the dynamic table permitted by the
            // peer is zero).

            // An endpoint MAY avoid creating a decoder stream if its decoder sets
            // the maximum capacity of the dynamic table to zero.

            // Don't create Encoder and Decoder as they aren't used now.
            var controlTask = CreateControlStream(application);

            try
            {
                while (true)
                {
                    var streamContext = await _multiplexedContext.AcceptAsync(_connectionAborted.Token);
                    if (streamContext == null || _haveSentGoAway)
                    {
                        break;
                    }

                    var quicStreamFeature = streamContext.Features.Get<IStreamDirectionFeature>();
                    var streamIdFeature = streamContext.Features.Get<IStreamIdFeature>();

                    Debug.Assert(quicStreamFeature != null);

                    var httpConnectionContext = new Http3StreamContext
                    {
                        ConnectionId = streamContext.ConnectionId,
                        StreamContext = streamContext,
                        // TODO connection context is null here. Should we set it to anything?
                        ServiceContext = _context.ServiceContext,
                        ConnectionFeatures = streamContext.Features,
                        MemoryPool = _context.MemoryPool,
                        Transport = streamContext.Transport,
                        TimeoutControl = _context.TimeoutControl,
                        LocalEndPoint = streamContext.LocalEndPoint as IPEndPoint,
                        RemoteEndPoint = streamContext.RemoteEndPoint as IPEndPoint
                    };

                    if (!quicStreamFeature.CanWrite)
                    {
                        // Unidirectional stream
                        var stream = new Http3ControlStream<TContext>(application, this, httpConnectionContext);
                        ThreadPool.UnsafeQueueUserWorkItem(stream, preferLocal: false);
                    }
                    else
                    {
                        // Keep track of highest stream id seen for GOAWAY
                        var streamId = streamIdFeature.StreamId;
                        HighestStreamId = streamId;

                        var http3Stream = new Http3Stream<TContext>(application, this, httpConnectionContext);
                        var stream = http3Stream;
                        lock (_streams)
                        {
                            _streams[streamId] = http3Stream;
                        }
                        KestrelEventSource.Log.RequestQueuedStart(stream, AspNetCore.Http.HttpProtocol.Http3);
                        ThreadPool.UnsafeQueueUserWorkItem(stream, preferLocal: false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Stream aborted/closed.
            }
            finally
            {
                // Abort all streams as connection has shutdown.
                lock (_streams)
                {
                    if (_aborted)
                    {
                        foreach (var stream in _streams.Values)
                        {
                            stream.Abort(_abortedException);
                        }
                    }
                    else
                    {
                        foreach (var stream in _streams.Values)
                        {
                            stream.OnInputOrOutputCompleted();
                        }
                    }

                    _inboundControlStream?.OnInputOrOutputCompleted();
                    _inboundEncoderStream?.OnInputOrOutputCompleted();
                    _inboundDecoderStream?.OnInputOrOutputCompleted();
                }

                OutboundControlStream?.Abort(new ConnectionAbortedException("Connection is shutting down."));

                await controlTask;
            }
        }

        private async ValueTask CreateControlStream<TContext>(IHttpApplication<TContext> application)
        {
            var stream = await CreateNewUnidirectionalStreamAsync(application);
            OutboundControlStream = stream;
            await stream.SendStreamIdAsync(id: 0);
            await stream.SendSettingsFrameAsync();
        }

        private async ValueTask<Http3ControlStream> CreateNewUnidirectionalStreamAsync<TContext>(IHttpApplication<TContext> application)
        {
            var features = new FeatureCollection();
            features.Set<IStreamDirectionFeature>(new DefaultStreamDirectionFeature(canRead: false, canWrite: true));
            var streamContext = await _multiplexedContext.ConnectAsync(features);
            var httpConnectionContext = new Http3StreamContext
            {
                //ConnectionId = "", TODO getting stream ID from stream that isn't started throws an exception.
                StreamContext = streamContext,
                Protocols = HttpProtocols.Http3,
                ServiceContext = _context.ServiceContext,
                ConnectionFeatures = streamContext.Features,
                MemoryPool = _context.MemoryPool,
                Transport = streamContext.Transport,
                TimeoutControl = _context.TimeoutControl,
                LocalEndPoint = streamContext.LocalEndPoint as IPEndPoint,
                RemoteEndPoint = streamContext.RemoteEndPoint as IPEndPoint,
                ServerSettings = _serverSettings,
            };

            return new Http3ControlStream<TContext>(application, this, httpConnectionContext);
        }

        public void HandleRequestHeadersTimeout()
        {
        }

        public void HandleReadDataRateTimeout()
        {
        }

        public void OnInputOrOutputCompleted()
        {
            // TODO poison streams?
        }

        public void Tick(DateTimeOffset now)
        {
        }

        private void InnerAbort(ConnectionAbortedException ex)
        {
            _abortedException = ex;

            lock (_sync)
            {
                if (OutboundControlStream != null)
                {
                    // TODO need to await this somewhere or allow this to be called elsewhere?
                    OutboundControlStream.SendGoAway(_highestOpenedStreamId).GetAwaiter().GetResult();
                }

                _haveSentGoAway = true;
                _connectionAborted.Cancel();
            }
        }

        public void ApplyMaxHeaderListSize(long value)
        {
            // TODO something here to call OnHeader?
        }

        internal void ApplyBlockedStream(long value)
        {
        }

        internal void ApplyMaxTableCapacity(long value)
        {
            // TODO make sure this works
            //_maxDynamicTableSize = value;
        }

        internal void RemoveStream(long streamId)
        {
            lock(_streams)
            {
                _streams.Remove(streamId);
            }
        }

        public bool SetInboundControlStream(Http3ControlStream stream)
        {
            lock (_sync)
            {
                if (_inboundControlStream == null)
                {
                    _inboundControlStream = stream;
                    return true;
                }
                return false;
            }
        }

        public bool SetInboundEncoderStream(Http3ControlStream stream)
        {
            lock (_sync)
            {
                if (_inboundEncoderStream == null)
                {
                    _inboundEncoderStream = stream;
                    return true;
                }
                return false;
            }
        }

        public bool SetInboundDecoderStream(Http3ControlStream stream)
        {
            lock (_sync)
            {
                if (_inboundDecoderStream == null)
                {
                    _inboundDecoderStream = stream;
                    return true;
                }
                return false;
            }
        }
    }
}
