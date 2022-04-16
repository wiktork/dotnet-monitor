// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "IpcCommClient.h"
#include "IpcCommServer.h"

IpcCommServer::IpcCommServer()
{
}

HRESULT IpcCommServer::Bind(const std::string& rootAddress)
{
    if (rootAddress.length() >= UNIX_PATH_MAX) 
    {
        return E_INVALIDARG;
    }
    
    //We don't error check this on purpose
    std::remove(rootAddress.c_str());

    sockaddr_un address;
    ZeroMemory(&address, sizeof(address));
    address.sun_family = AF_UNIX;
    errno_t error = strncpy_s(address.sun_path, rootAddress.c_str(), UNIX_PATH_MAX);
    if (error != ERROR_SUCCESS)
    {
        return HRESULT_FROM_WIN32(error);
    }
    _domainSocket = socket(AF_UNIX, SOCK_STREAM, 0);
    if (!_domainSocket.Valid())
    {
        return SocketWrapper::GetSocketError();
    }
    if (bind(_domainSocket, (sockaddr*)&address, sizeof(address)) != 0)
    {
        return SocketWrapper::GetSocketError();
    }
    if (listen(_domainSocket, 20) != 0)
    {
        return SocketWrapper::GetSocketError();
    }

    return S_OK;
}

HRESULT IpcCommServer::Accept(std::shared_ptr<IpcCommClient>& client)
{
    if (!_domainSocket.Valid())
    {
        return E_UNEXPECTED;
    }

    SocketWrapper clientSocket = SocketWrapper(accept(_domainSocket, nullptr, nullptr));
    if (!clientSocket.Valid())
    {
        return SocketWrapper::GetSocketError();
    }

    client = std::make_shared<IpcCommClient>(clientSocket.Release());

    return S_OK;
}
