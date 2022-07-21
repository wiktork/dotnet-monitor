// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "ProfilerEventProvider.h"
#include <corhlpr.h>

HRESULT ProfilerEventProvider::CreateProvider(const WCHAR* providerName, ICorProfilerInfo12* profilerInfo, std::unique_ptr<ProfilerEventProvider>& provider)
{
    EVENTPIPE_PROVIDER eventProvider = 0;
    HRESULT hr;
    IfFailRet(profilerInfo->EventPipeCreateProvider(providerName, &eventProvider));

    provider = std::unique_ptr< ProfilerEventProvider>(new ProfilerEventProvider(profilerInfo, eventProvider));

    return S_OK;

}

ProfilerEventProvider::ProfilerEventProvider(ICorProfilerInfo12* profilerInfo, EVENTPIPE_PROVIDER provider) : _provider(provider), _profilerInfo(profilerInfo)
{
}

HRESULT ProfilerEventProvider::DefineEvent(const WCHAR* eventName, std::unique_ptr<ProfilerEvent>& profilerEventDescriptor)
{
    return DefineEvent(eventName, nullptr, 0, profilerEventDescriptor);
}

HRESULT ProfilerEventProvider::DefineEvent(const WCHAR* eventName, COR_PRF_EVENTPIPE_PARAM_DESC* descriptors, UINT32 descriptorsLen, std::unique_ptr<ProfilerEvent>& profilerEventDescriptor)
{
    EVENTPIPE_EVENT event = 0;
    HRESULT hr = _profilerInfo->EventPipeDefineEvent(
        _provider,
        eventName,
        _currentEventId,
        0, //We not use keywords
        1, // eventVersion
        COR_PRF_EVENTPIPE_LOGALWAYS,
        0, //We not use opcodes
        FALSE, //No need for stacks
        descriptorsLen,
        descriptors,
        &event);
    if (SUCCEEDED(hr))
    {
        profilerEventDescriptor = std::unique_ptr<ProfilerEvent>(new ProfilerEvent(_profilerInfo, event));
        _currentEventId++;
    }
    return hr;
}