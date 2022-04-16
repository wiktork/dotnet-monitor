// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <string>
#include <memory>

#include "SocketWrapper.h"
#include "IpcCommClient.h"

class IpcCommServer
{
public:
    IpcCommServer();
    HRESULT Bind(const std::string& rootAddress);
    HRESULT Accept(std::shared_ptr<IpcCommClient>& client);
private:
    SocketWrapper _domainSocket = 0;
};