// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.AspNetCore.DataProtection
{
    internal class TypeForwardingActivator : SimpleActivator
    {
        private const string OldNamespace = "Microsoft.AspNet.DataProtection";
        private const string CurrentNamespace = "Microsoft.AspNetCore.DataProtection";
        private readonly ILogger _logger;

        public TypeForwardingActivator(IServiceProvider services)
            : this(services, NullLoggerFactory.Instance)
        {
        }

        public TypeForwardingActivator(IServiceProvider services, ILoggerFactory loggerFactory)
            : base(services)
        {
            _logger = loggerFactory.CreateLogger(typeof(TypeForwardingActivator));
        }

        public override object CreateInstance(Type expectedBaseType, string originalTypeName)
            => CreateInstance(expectedBaseType, originalTypeName, out var _);

        // for testing
        internal object CreateInstance(Type expectedBaseType, string originalTypeName, out bool forwarded)
        {
            var forwardedTypeName = originalTypeName;
            var candidate = false;
            if (originalTypeName.Contains(OldNamespace))
            {
                candidate = true;
                forwardedTypeName = originalTypeName.Replace(OldNamespace, CurrentNamespace);
            }

            if (candidate || forwardedTypeName.StartsWith(CurrentNamespace + ".", StringComparison.Ordinal))
            {
                candidate = true;
                forwardedTypeName = RemoveVersionFromAssemblyName(forwardedTypeName);
            }

            if (candidate)
            {
                var type = Type.GetType(forwardedTypeName, false);
                if (type != null)
                {
                    _logger.LogDebug("Forwarded activator type request from {FromType} to {ToType}",
                        originalTypeName,
                        forwardedTypeName);
                    forwarded = true;
                    return base.CreateInstance(expectedBaseType, forwardedTypeName);
                }
            }

            forwarded = false;
            return base.CreateInstance(expectedBaseType, originalTypeName);
        }

        protected string RemoveVersionFromAssemblyName(string forwardedTypeName)
        {
            // Type, Assembly, Version={Version}, Culture={Culture}, PublicKeyToken={Token}

            var versionStartIndex = forwardedTypeName.IndexOf("Version=", StringComparison.Ordinal);
            while (versionStartIndex != -1)
            {
                var versionEndIndex = forwardedTypeName.IndexOf(',', versionStartIndex + "Version=".Length);

                if (versionEndIndex == -1)
                {
                    // No end index?
                    return forwardedTypeName;
                }

                forwardedTypeName = forwardedTypeName.Remove(versionStartIndex, versionEndIndex - versionStartIndex + 2);
                versionStartIndex = forwardedTypeName.IndexOf("Version=", StringComparison.Ordinal);
            }

            // No version left
            return forwardedTypeName;

        }
    }
}
