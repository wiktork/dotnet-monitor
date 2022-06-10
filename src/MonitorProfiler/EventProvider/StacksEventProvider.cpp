#include "StacksEventProvider.h"
#include <corhlpr.h>



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
    //HRESULT hr;

    COR_PRF_EVENTPIPE_PARAM_DESC CallStackEventDescriptor[] =
    {
        { COR_PRF_EVENTPIPE_UINT64,   0,                          L"ThreadId" },
        { COR_PRF_EVENTPIPE_ARRAY,    COR_PRF_EVENTPIPE_INT64,    L"FunctionIds" },
        { COR_PRF_EVENTPIPE_ARRAY,    COR_PRF_EVENTPIPE_INT64,    L"IpOffsets" }
    };

    _provider->DefineEvent<3>(L"Callstack", CallStackEventDescriptor, _callstackEvent);

    return S_OK;
}
