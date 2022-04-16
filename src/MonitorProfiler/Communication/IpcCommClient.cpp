// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "IpcCommClient.h"
#include <memory>

HRESULT IpcCommClient::Receive(IpcMessage& message)
{
    if (!_socket.Valid())
    {
        return E_UNEXPECTED;
    }

    //CONSIDER It is generally more performant to read and buffer larger chunks, in this case we are not expecting very frequent communication.
    char buffer[6];
    int read = 0;
    int offset = 0;

    do
    {
        int read = recv(_socket, &buffer[offset], sizeof(buffer) - offset, 0);
        if (read == 0)
        {
            return E_ABORT;
        }
        if (read < 0)
        {
            return SocketWrapper::GetSocketError();
        }
        offset += read;

    } while (offset < sizeof(buffer));

    //TODO do we need net to host ordering here?

    message.MessageType = *reinterpret_cast<MessageType*>(buffer);
    message.Parameters = *reinterpret_cast<int*>(&buffer[sizeof(MessageType)]);

    return S_OK;
}

HRESULT IpcCommClient::Send(const IpcMessage& message)
{

    //TODO htons?

    char buffer[6];
    *reinterpret_cast<MessageType*>(buffer) = message.MessageType;
    *reinterpret_cast<int*>(&buffer[sizeof(MessageType)]) = message.Parameters;
    int sent = send(_socket, buffer, sizeof(buffer), 0);
    if (sent <= 0)
    {
        return SocketWrapper::GetSocketError();
    }

    return S_OK;
}

IpcCommClient::IpcCommClient(SOCKET socket) : _socket(socket)
{
}
