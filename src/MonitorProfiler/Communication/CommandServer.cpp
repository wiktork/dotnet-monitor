// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "CommandServer.h"
#include <thread>
#include "../Logging/Logger.h"

CommandServer::CommandServer(std::shared_ptr<ILogger> logger) : _logger(logger)
{
}

HRESULT CommandServer::Start(const std::string& path, std::function<HRESULT(const IpcMessage& message)> callback)
{
    HRESULT hr;

    _callback = callback;

    IfFailLogRet_(_logger, _server.Bind(path));
    _listeningThread = std::thread(&CommandServer::ListeningThread, this);
    _clientThread = std::thread(&CommandServer::ClientProcessingThread, this);
    return S_OK;
}

void CommandServer::ListeningThread()
{
    while (true)
    {
        std::shared_ptr<IpcCommClient> client;
        HRESULT hr = _server.Accept(client);
        if (FAILED(hr))
        {
            break;
        }

        _clientQueue.Push(client);
    }
}

void CommandServer::ClientProcessingThread()
{
    while (true)
    {
        std::shared_ptr<IpcCommClient> client = _clientQueue.BlockingDequeue();

        IpcMessage message;
        HRESULT hr = client->Receive(message);
        if (FAILED(hr))
        {
            _logger->Log(LogLevel::Error, _T("Unexpected error when receiving data: 0x%08x"), hr);
            continue;
        }

        hr = _callback(message);
        IpcMessage response;
        response.MessageType = SUCCEEDED(hr) ? MessageType::OK : MessageType::Error;
        response.Parameters = hr;

        hr = client->Send(response);
    }
}