// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

package com.microsoft.signalr;

import java.nio.ByteBuffer;
import java.util.List;

/**
 * A protocol abstraction for communicating with SignalR hubs.
 */
public interface HubProtocol {
    String getName();
    int getVersion();

    /**
     * Creates a new list of {@link HubMessage}s.
     * @param message A ByteBuffer representation of one or more {@link HubMessage}s.
     * @param binder The {@link InvocationBinder} to use for this Protocol instance.
     * @return A list of {@link HubMessage}s.
     */
    List<HubMessage> parseMessages(ByteBuffer message, InvocationBinder binder);

    /**
     * Writes the specified {@link HubMessage} to a String.
     * @param message The message to write.
     * @return A ByteBuffer representation of the message.
     */
    ByteBuffer writeMessage(HubMessage message);
}
