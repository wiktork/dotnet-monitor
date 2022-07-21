// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "StacksEventProvider.h"
#include <corhlpr.h>
#include "cor.h"
#include "../EventProvider/ProfilerEventData.h"

const WCHAR* StacksEventProvider::ProviderName = _T("DotnetMonitorStacksEventProvider");

static COR_PRF_EVENTPIPE_PARAM_DESC CallStackEventDescriptor[] =
{
    { COR_PRF_EVENTPIPE_UINT64,   0,                          L"ThreadId" },
    { COR_PRF_EVENTPIPE_ARRAY,    COR_PRF_EVENTPIPE_UINT64,    L"FunctionIds" },
    { COR_PRF_EVENTPIPE_ARRAY,    COR_PRF_EVENTPIPE_UINT64,    L"IpOffsets" }
};

static COR_PRF_EVENTPIPE_PARAM_DESC FunctionIdDescriptor[] =
{
    { COR_PRF_EVENTPIPE_UINT64, 0, L"FunctionId"},
    { COR_PRF_EVENTPIPE_UINT64, 0, L"ClassId"},
    { COR_PRF_EVENTPIPE_UINT32, 0, L"ClassToken"},
    { COR_PRF_EVENTPIPE_UINT64, 0, L"ModuleId"},
    { COR_PRF_EVENTPIPE_STRING, 0, L"Name"},
    { COR_PRF_EVENTPIPE_ARRAY, COR_PRF_EVENTPIPE_UINT64, L"TypeArgs" }
};

static COR_PRF_EVENTPIPE_PARAM_DESC ClassIdDescriptor[] =
{
    { COR_PRF_EVENTPIPE_UINT64, 0, L"ClassId"},
    { COR_PRF_EVENTPIPE_UINT64, 0, L"ModuleId"},
    { COR_PRF_EVENTPIPE_UINT32, 0, L"Token"},
    { COR_PRF_EVENTPIPE_UINT32, 0, L"Flags"},
    { COR_PRF_EVENTPIPE_ARRAY, COR_PRF_EVENTPIPE_INT64, L"TypeArgs"}
};

static COR_PRF_EVENTPIPE_PARAM_DESC TokenIdDescripor[] =
{
    { COR_PRF_EVENTPIPE_UINT64, 0, L"ModuleId"},
    { COR_PRF_EVENTPIPE_UINT32, 0, L"Token"},
    { COR_PRF_EVENTPIPE_UINT32, 0, L"OuterToken"},
    { COR_PRF_EVENTPIPE_STRING, 0, L"Name"}
};

static COR_PRF_EVENTPIPE_PARAM_DESC ModuleDescriptor[] =
{
    { COR_PRF_EVENTPIPE_UINT64, 0, L"ModuleId"},
    { COR_PRF_EVENTPIPE_STRING, 0, L"Name"}
};

static UINT_PTR GetOffsetFromFrame(const StackFrame& frame)
{
    return frame.GetOffset();
}

static FunctionID GetFunctionIdFromFrame(const StackFrame& frame)
{
    return frame.GetFunctionId();
}

HRESULT StacksEventProvider::CreateProvider(ICorProfilerInfo12* profilerInfo, std::unique_ptr<StacksEventProvider>& eventProvider)
{
    std::unique_ptr<ProfilerEventProvider> provider;
    HRESULT hr;

    IfFailRet(ProfilerEventProvider::CreateProvider(ProviderName, profilerInfo, provider));

    eventProvider = std::unique_ptr<StacksEventProvider>(new StacksEventProvider(profilerInfo, provider));

    IfFailRet(eventProvider->DefineEvents());

    return S_OK;
}

HRESULT StacksEventProvider::DefineEvents()
{
    HRESULT hr;

    IfFailRet(_provider->DefineEvent(L"Callstack", CallStackEventDescriptor, _callstackEvent));
    IfFailRet(_provider->DefineEvent(L"FunctionDesc", FunctionIdDescriptor, _functionIdEvent));
    IfFailRet(_provider->DefineEvent(L"ClassDesc", ClassIdDescriptor, _classIdEvent));
    IfFailRet(_provider->DefineEvent(L"ModuleDesc", ModuleDescriptor, _moduleEvent));
    IfFailRet(_provider->DefineEvent(L"TokenDesc", TokenIdDescripor, _tokenEvent));
    IfFailRet(_provider->DefineEvent(L"End", _endEvent));

    return S_OK;
}

HRESULT StacksEventProvider::WriteCallstack(const Stack& stack)
{
    ProfilerEventData<3> profilerEventData;
    HRESULT hr;

    profilerEventData.WriteData<0>(stack.GetThreadId());
    const std::vector<StackFrame>& frames = stack.GetFrames();

    auto ids = profilerEventData.GetEventBuffer(frames, &GetFunctionIdFromFrame);
    auto offsets = profilerEventData.GetEventBuffer(frames, &GetOffsetFromFrame);

    profilerEventData.WriteData<1>(ids);
    profilerEventData.WriteData<2>(offsets);

    IfFailRet(_callstackEvent->WriteEvent(profilerEventData._eventData));

    return S_OK;
}

HRESULT StacksEventProvider::WriteClassData(ClassID classId, const ClassData& classData)
{
    ProfilerEventData<5> profilerEventData;
    HRESULT hr;

    profilerEventData.WriteData<0>(classId);
    profilerEventData.WriteData<1>(classData.GetModuleId());
    profilerEventData.WriteData<2>(classData.GetToken());
    profilerEventData.WriteData<3>(classData.GetFlags());

    std::vector<BYTE> buffer;

    if (classData.GetTypeArgs().size() > 0)
    {
        buffer = profilerEventData.GetEventBuffer(classData.GetTypeArgs());
        profilerEventData.WriteData<4>(buffer);
    }

    IfFailRet(_classIdEvent->WriteEvent(profilerEventData._eventData));

    return S_OK;
}

HRESULT StacksEventProvider::WriteFunctionData(FunctionID functionId, const FunctionData& functionData)
{
    ProfilerEventData<6> profilerEventData;
    HRESULT hr;

    profilerEventData.WriteData<0>(functionId);
    profilerEventData.WriteData<1>(functionData.GetClass());
    profilerEventData.WriteData<2>(functionData.GetClassToken());
    profilerEventData.WriteData<3>(functionData.GetModuleId());
    profilerEventData.WriteData<4>(functionData.GetName());

    std::vector<BYTE> buffer;
    if (functionData.GetTypeArgs().size() > 0)
    {
        buffer = profilerEventData.GetEventBuffer(functionData.GetTypeArgs());
        profilerEventData.WriteData<5>(buffer);
    }

    IfFailRet(_functionIdEvent->WriteEvent(profilerEventData._eventData));

    return S_OK;
}

HRESULT StacksEventProvider::WriteModuleData(ModuleID moduleId, const ModuleData& moduleData)
{
    ProfilerEventData<2> profilerEventData;
    HRESULT hr;

    profilerEventData.WriteData<0>(moduleId);
    profilerEventData.WriteData<1>(moduleData.GetName());

    IfFailRet(_moduleEvent->WriteEvent(profilerEventData._eventData));

    return S_OK;
}

HRESULT StacksEventProvider::WriteTokenData(ModuleID moduleId, mdTypeDef typeDef, const TokenData& tokenData)
{
    ProfilerEventData<4> profilerEventData;
    HRESULT hr;

    profilerEventData.WriteData<0>(moduleId);
    profilerEventData.WriteData<1>(typeDef);
    profilerEventData.WriteData<2>(tokenData.GetOuterToken());
    profilerEventData.WriteData<3>(tokenData.GetName());
    IfFailRet(_tokenEvent->WriteEvent(profilerEventData._eventData));

    return S_OK;
}

HRESULT StacksEventProvider::WriteEndEvent()
{
    return _endEvent->WriteEvent();
}
