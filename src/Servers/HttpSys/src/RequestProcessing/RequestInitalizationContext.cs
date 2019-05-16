// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.HttpSys.Internal;

namespace Microsoft.AspNetCore.Server.HttpSys
{
    internal struct RequestInitalizationContext
    {
        public HttpSysListener Server;
        public NativeRequestContext MemoryBlob;

        // For testing
        public RequestContext CreateContext()
        {
            var context = new RequestContext();
            context.InitializeCore(Server, MemoryBlob);
            return context;
        }
    }
}
