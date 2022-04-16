// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#if TARGET_WINDOWS
#include <WinSock2.h>
#include <afunix.h>
#else
typedef int SOCKET;
#endif

struct SocketWrapper
{
    SOCKET _socket;
    SocketWrapper(SOCKET socket) : _socket(socket)
    {
    }

    operator SOCKET const ()
    {
        return _socket;
    }

    SOCKET Release()
    {
        SOCKET s = _socket;
        _socket = 0;
        return s;
    }

    static HRESULT GetSocketError()
    {
#if TARGET_UNIX
        return HRESULT_FROM_WIN32(errno);
#else
        return HRESULT_FROM_WIN32(WSAGetLastError());
#endif
    }

    SocketWrapper(SocketWrapper&& other)
    {
        _socket = other._socket;
        other._socket = 0;
    }

    const SocketWrapper& operator = (SocketWrapper&& other)
    {
        if (&other == this)
        {
            return *this;
        }

        if (Valid())
        {
            closesocket(_socket);
        }
        _socket = other._socket;
        other._socket = 0;

        return *this;
    }

    const SocketWrapper& operator = (const SocketWrapper&) = delete;
    SocketWrapper(const SocketWrapper&) = delete;

    ~SocketWrapper()
    {
        if (Valid())
        {
            closesocket(_socket);
        }
    }

    bool Valid()
    {
#if TARGET_UNIX
        return _socket > 0;
#else
        return _socket != 0 && _socket != INVALID_SOCKET;
#endif
    }
};
