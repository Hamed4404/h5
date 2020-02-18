// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Quic.Internal;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Server.Kestrel.Transport.Quic
{
    public class QuicTransportFactory : IMultiplexedConnectionListenerFactory
    {
        private QuicTrace _log;
        private QuicTransportOptions _options;

        public QuicTransportFactory(ILoggerFactory loggerFactory, IOptions<QuicTransportOptions> options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            var logger = loggerFactory.CreateLogger("Microsoft.AspNetCore.Server.Kestrel.Transport.MsQuic");
            _log = new QuicTrace(logger);
            _options = options.Value;
        }

        public  ValueTask<IConnectionListener> BindAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
        {
            var transport = new QuicConnectionListener(_options, _log, endpoint);
            return new ValueTask<IConnectionListener>(transport);
        }
    }
}
