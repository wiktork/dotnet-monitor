// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "MainProfiler.h"
#include "../Environment/EnvironmentHelper.h"
#include "../Environment/ProfilerEnvironment.h"
#include "../Logging/AggregateLogger.h"
#include "../Logging/DebugLogger.h"
#include "corhlpr.h"
#include "macros.h"
#include <experimental/filesystem>
#include <thread>
#include <memory>

using namespace std;

#define IfFailLogRet(EXPR) IfFailLogRet_(m_pLogger, EXPR)

#define LogInformationV(format, ...) LogInformationV_(m_pLogger, format, __VA_ARGS__)

GUID MainProfiler::GetClsid()
{
    // {6A494330-5848-4A23-9D87-0E57BBF6DE79}
    return { 0x6A494330, 0x5848, 0x4A23,{ 0x9D, 0x87, 0x0E, 0x57, 0xBB, 0xF6, 0xDE, 0x79 } };
}

STDMETHODIMP MainProfiler::Initialize(IUnknown *pICorProfilerInfoUnk)
{
    ExpectedPtr(pICorProfilerInfoUnk);

    HRESULT hr = S_OK;


    while (!IsDebuggerPresent())
    {
        Sleep(1000);
        continue;
    }

    //These should always be initialized first
    IfFailRet(ProfilerBase::Initialize(pICorProfilerInfoUnk));
    IfFailRet(InitializeEnvironment());
    IfFailRet(InitializeLogging());

    IfFailLogRet(InitializeCommandServer());

    // Logging is initialized and can now be used

    // Set product version environment variable to allow discovery of if the profiler
    // as been applied to a target process. Diagnostic tools must use the diagnostic
    // communication channel's GetProcessEnvironment command to get this value.
    IfFailLogRet(EnvironmentHelper::SetProductVersion(m_pEnvironment, m_pLogger));

#ifdef TARGET_WINDOWS
    DWORD processId = GetCurrentProcessId();
    LogInformationV(_T("Process Id: %d"), processId);
#endif

    return S_OK;
}

STDMETHODIMP MainProfiler::Shutdown()
{
    m_pLogger.reset();
    m_pEnvironment.reset();

    return ProfilerBase::Shutdown();
}

STDMETHODIMP MainProfiler::LoadAsNotficationOnly(BOOL *pbNotificationOnly)
{
    ExpectedPtr(pbNotificationOnly);

    *pbNotificationOnly = TRUE;

    return S_OK;
}

HRESULT MainProfiler::InitializeEnvironment()
{
    m_pEnvironment = make_shared<ProfilerEnvironment>(m_pCorProfilerInfo);
    IfNullRet(m_pEnvironment);

    return S_OK;
}

HRESULT MainProfiler::InitializeLogging()
{
    // Create an aggregate logger to allow for multiple logging implementations
    unique_ptr<AggregateLogger> pAggregateLogger(new (nothrow) AggregateLogger());
    IfNullRet(pAggregateLogger);

#ifdef _DEBUG
#ifdef TARGET_WINDOWS
    // Add the debug output logger for when debugging on Windows
    shared_ptr<DebugLogger> pDebugLogger = make_shared<DebugLogger>();
    IfNullRet(pDebugLogger);
    pAggregateLogger->Add(pDebugLogger);
#endif
#endif

    m_pLogger.reset(pAggregateLogger.release());

    return S_OK;
}

HRESULT MainProfiler::InitializeCommandServer()
{
    HRESULT hr = S_OK;

    tstring instanceId;
    tstring tmpDir = _T("/tmp");

    IfFailRet(m_pEnvironment->GetEnvironmentVariable(_T("DOTNETMONITOR_INSTANCEID"), instanceId));

#if TARGET_WINDOWS
    IfFailRet(m_pEnvironment->GetEnvironmentVariable(_T("TEMP"), tmpDir));
#endif

#if TARGET_WINDOWS

    WSADATA d;
    WSAStartup(MAKEWORD(2, 2), & d);
#endif


    _commandServer = std::unique_ptr<CommandServer>(new CommandServer(m_pLogger));
    std::string socketPath = std::experimental::filesystem::path(tmpDir).append(instanceId).replace_extension(".sock").string();
    std::remove(socketPath.c_str());

    IfFailRet(_commandServer->Start(socketPath, [this](const IpcMessage& message)-> HRESULT { return this->MessageCallback(message); }));

    return S_OK;
}

HRESULT MainProfiler::MessageCallback(const IpcMessage& message)
{
    m_pLogger->Log(LogLevel::Information, _T("Message received from client: %d %d"), message.MessageType, message.Parameters);
    return S_OK;
}
