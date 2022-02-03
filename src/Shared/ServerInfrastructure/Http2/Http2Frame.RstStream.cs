// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http2;

/* https://tools.ietf.org/html/rfc7540#section-6.4
    +---------------------------------------------------------------+
    |                        Error Code (32)                        |
    +---------------------------------------------------------------+
*/
internal partial class Http2Frame
{
    public Http2ErrorCode RstStreamErrorCode { get; set; }

    public void PrepareRstStream(int streamId, Http2ErrorCode errorCode)
    {
        PayloadLength = 4;
        Type = Http2FrameType.RST_STREAM;
        Flags = 0;
        StreamId = streamId;
        RstStreamErrorCode = errorCode;
    }
}
