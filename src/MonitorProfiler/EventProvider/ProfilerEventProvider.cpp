#include "ProfilerEventProvider.h"

HRESULT ProfilerEventProvider::CreateProvider(const WCHAR* providerName, ICorProfilerInfo12* profilerInfo, std::unique_ptr<ProfilerEventProvider>& provider)
{
    return E_NOTIMPL;
}

template<int DescLen>
HRESULT ProfilerEventProvider::DefineEvent(const WCHAR* eventName, COR_PRF_EVENTPIPE_PARAM_DESC descriptors[DescLen], std::unique_ptr<ProfilerEvent>& profilerEventDescriptor)
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
        DescLen,
        descriptors,
        &event);
    if (SUCCEEDED(hr))
    {
        profilerEventDescriptor = std::make_unique<ProfilerEvent>(_profilerInfo, event);
        _currentEventId++;
    }
    return hr;
}

ProfilerEventProvider::ProfilerEventProvider(ICorProfilerInfo12* profilerInfo, EVENTPIPE_PROVIDER provider) : _profilerInfo(profilerInfo), _provider(provider)
{
}
