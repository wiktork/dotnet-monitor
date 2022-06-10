#include "StacksEventProvider.h"
#include <corhlpr.h>

static COR_PRF_EVENTPIPE_PARAM_DESC CallStackEventDescriptor[] =
{
    { COR_PRF_EVENTPIPE_UINT64,   0,                          L"ThreadId" },
    { COR_PRF_EVENTPIPE_ARRAY,    COR_PRF_EVENTPIPE_INT64,    L"FunctionIds" },
    { COR_PRF_EVENTPIPE_ARRAY,    COR_PRF_EVENTPIPE_INT64,    L"IpOffsets" }
};

static COR_PRF_EVENTPIPE_PARAM_DESC FunctionIdDescriptor[] =
{
    { COR_PRF_EVENTPIPE_UINT64, 0, L"FunctionId"},
    { COR_PRF_EVENTPIPE_UINT64, 0, L"ClassId"},
    { COR_PRF_EVENTPIPE_UINT64, 0, L"ModuleId"},
    { COR_PRF_EVENTPIPE_STRING, 0, L"Name"},
    { COR_PRF_EVENTPIPE_ARRAY, COR_PRF_EVENTPIPE_INT64, L"TypeArgs" }
};

static COR_PRF_EVENTPIPE_PARAM_DESC ClassIdDescriptor[] =
{
    { COR_PRF_EVENTPIPE_UINT64, 0, L"ClassId"},
    { COR_PRF_EVENTPIPE_UINT64, 0, L"ModuleId"},
    { COR_PRF_EVENTPIPE_UINT64, 0, L"ParentClassId"},
    { COR_PRF_EVENTPIPE_STRING, 0, L"Name"},
    { COR_PRF_EVENTPIPE_ARRAY, COR_PRF_EVENTPIPE_INT64, L"TypeArgs"}
};

static COR_PRF_EVENTPIPE_PARAM_DESC ModuleDescriptor[] =
{
    { COR_PRF_EVENTPIPE_UINT64, 0, L"ModuleId"},
    { COR_PRF_EVENTPIPE_STRING, 0, L"Name"}
};

HRESULT StacksEventProvider::CreateProvider(ICorProfilerInfo12* profilerInfo, std::unique_ptr<StacksEventProvider>& eventProvider)
{
    std::unique_ptr<ProfilerEventProvider> provider;
    HRESULT hr;

    IfFailRet(ProfilerEventProvider::CreateProvider(L"DotnetMonitorStacksEventProvider", profilerInfo, provider));

    eventProvider = std::unique_ptr<StacksEventProvider>(new StacksEventProvider(profilerInfo, provider));

    return S_OK;
}

HRESULT StacksEventProvider::DefineEvents()
{
    HRESULT hr;

    IfFailRet(_provider->DefineEvent(L"Callstack", CallStackEventDescriptor, _callstackEvent));
    IfFailRet(_provider->DefineEvent(L"FunctionDesc", FunctionIdDescriptor, _functionIdEvent));
    IfFailRet(_provider->DefineEvent(L"ClassDesc", ClassIdDescriptor, _classIdEvent));
    IfFailRet(_provider->DefineEvent(L"ModuleDesc", ModuleDescriptor, _moduleEvent));
    IfFailRet(_provider->DefineEvent(L"End", _endEvent));

    return S_OK;
}
