// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "CommandServer.h"
#include <thread>
#include <chrono>
#include "Logging/Logger.h"

CommandServer::CommandServer(const std::shared_ptr<ILogger>& logger, ICorProfilerInfo12* profilerInfo) :
    _shutdown(false),
    _server(logger),
    _logger(logger),
    _profilerInfo(profilerInfo)
{
}

HRESULT CommandServer::Start(
    const std::string& path,
    std::function<HRESULT(const IpcMessage& message)> callback,
    std::function<HRESULT(const IpcMessage& message)> validateMessageCallback,
    std::function<HRESULT(unsigned short commandSet, bool& unmanagedOnly)> unmanagedOnlyCallback)
{
    if (_shutdown.load())
    {
        return E_UNEXPECTED;
    }

    HRESULT hr;
#if TARGET_WINDOWS
    WSADATA wsaData;
    int result = WSAStartup(MAKEWORD(2, 2), &wsaData);
    if (result != 0)
    {
        return HRESULT_FROM_WIN32(result);
    }
#endif

    _callback = callback;
    _validateMessageCallback = validateMessageCallback;
    _unmanagedOnlyCallback = unmanagedOnlyCallback;

    IfFailLogRet_(_logger, _server.Bind(path));
    _listeningThread = std::thread(&CommandServer::ListeningThread, this);
    _clientThread = std::thread(&CommandServer::ClientProcessingThread, this);
    _unmanagedOnlyThread = std::thread(&CommandServer::UnmanagedOnlyProcessingThread, this);
    return S_OK;
}

void CommandServer::Shutdown()
{
    bool shutdown = false;
    if (_shutdown.compare_exchange_strong(shutdown, true))
    {
        _clientQueue.Complete();
        _unmanagedOnlyQueue.Complete();
        _server.Shutdown();

        _listeningThread.join();
        _clientThread.join();
        _unmanagedOnlyThread.join();
    }
}

void CommandServer::ListeningThread()
{
    // TODO: Handle oom scenarios
    IpcMessage response;
    response.CommandSet = static_cast<unsigned short>(CommandSet::ServerResponse);
    response.Command = static_cast<unsigned short>(ServerResponseCommand::Status);
    response.Payload.resize(sizeof(HRESULT));

    while (true)
    {
        std::shared_ptr<IpcCommClient> client;
        HRESULT hr = _server.Accept(client);
        if (FAILED(hr))
        {
            break;
        }

        CallbackInfo info;

        //Note this can timeout if the client doesn't send anything
        hr = client->Receive(info.Message);
        if (FAILED(hr))
        {
            _logger->Log(LogLevel::Error, _LS("Unexpected error when receiving data: 0x%08x"), hr);
            // Best-effort shutdown, ignore the result.
            client->Shutdown();
            continue;
        }

        bool doEnqueueMessage = true;
        hr = _validateMessageCallback(info.Message);
        if (FAILED(hr))
        {
            _logger->Log(LogLevel::Error, _LS("Failed to validate message: 0x%08x"), hr);
            doEnqueueMessage = false;
        }

        std::shared_ptr<std::promise<HRESULT>> promise;
        bool resetCommand = false;

        if ((info.Message.CommandSet == static_cast<short>(CommandSet::StartupHook)) && (info.Message.Command == 2))
        {
            // This is a shutdown command, we need to shutdown the server.
            resetCommand = true;
        }

        if (doEnqueueMessage)
        {
            if (resetCommand)
            {
                promise = std::make_shared<std::promise<HRESULT>>();
                info.Promise = promise;
            }

            bool unmanagedOnly = false;
            if (SUCCEEDED(_unmanagedOnlyCallback(info.Message.CommandSet, unmanagedOnly)) && unmanagedOnly)
            {
                _unmanagedOnlyQueue.Enqueue(info);
            }
            else
            {
                _clientQueue.Enqueue(info);
            }
        }

        // Reset command is special. We wait for everything to be processed.

        if (resetCommand)
        {
            auto start = std::chrono::high_resolution_clock::now();
            _logger->Log(LogLevel::Information, _LS("BeginDrain operation"));
            _unmanagedOnlyQueue.Drain();
            _clientQueue.Drain();

            hr = promise->get_future().get();

            auto end = std::chrono::high_resolution_clock::now();
            std::chrono::duration<double> duration = end - start;
            _logger->Log(LogLevel::Information, _LS("Drain operation complete, duration: %f seconds"), duration.count());
        }
        *reinterpret_cast<HRESULT*>(response.Payload.data()) = hr;
        hr = client->Send(response);
        if (FAILED(hr))
        {
            _logger->Log(LogLevel::Error, _LS("Unexpected error when sending data: 0x%08x"), hr);
            doEnqueueMessage = false;
        }

        hr = client->Shutdown();
        if (FAILED(hr))
        {
            _logger->Log(LogLevel::Warning, _LS("Unexpected error during shutdown: 0x%08x"), hr);
            // Not fatal, keep processing the message
        }
    }
}

void CommandServer::ClientProcessingThread()
{
    HRESULT hr = _profilerInfo->InitializeCurrentThread();

    if (FAILED(hr))
    {
        _logger->Log(LogLevel::Error, _LS("Unable to initialize thread: 0x%08x"), hr);
        return;
    }

    while (true)
    {
        CallbackInfo info;
        hr = _clientQueue.BlockingDequeue(info);
        if (hr != S_OK)
        {
            //We are complete, discard all messages
            break;
        }

        // DispatchMessage in the callback serializes all callbacks.
        // TODO Need to wait for this callback to finish entirely
        // TODO Need to figure out if parameter capture reset and stack are synchronized on the queue.
        hr = _callback(info.Message);
        if (hr != S_OK)
        {
            _logger->Log(LogLevel::Warning, _LS("IpcMessage callback failed: 0x%08x"), hr);
        }

        if (info.Promise)
        {
            info.Promise->set_value(hr);
        }
    }
}

void CommandServer::UnmanagedOnlyProcessingThread()
{
    HRESULT hr = _profilerInfo->InitializeCurrentThread();

    if (FAILED(hr))
    {
        _logger->Log(LogLevel::Error, _LS("Unable to initialize thread: 0x%08x"), hr);
        return;
    }

    while (true)
    {
        CallbackInfo info;
        hr = _unmanagedOnlyQueue.BlockingDequeue(info);
        if (hr != S_OK)
        {
            // We are complete, discard all messages
            break;
        }

        hr = _callback(info.Message);
        if (hr != S_OK)
        {
            _logger->Log(LogLevel::Warning, _LS("IpcMessage callback failed: 0x%08x"), hr);
        }

        if (info.Promise)
        {
            info.Promise->set_value(hr);
        }
    }
}
