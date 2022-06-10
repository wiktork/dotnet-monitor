#pragma once

#include "cor.h"
#include "corprof.h"
#include "com.h"
#include <memory>
#include "ProfilerEvent.h"

class ProfilerEventProvider
{
    public:
        static HRESULT CreateProvider(const WCHAR* providerName, ICorProfilerInfo12* profilerInfo, std::unique_ptr<ProfilerEventProvider>& provider);

        template<int DescLen>
        HRESULT DefineEvent(const WCHAR* eventName, COR_PRF_EVENTPIPE_PARAM_DESC (&descriptors)[DescLen], std::unique_ptr<ProfilerEvent>& profilerEventDescriptor)
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
    private:
        ProfilerEventProvider(ICorProfilerInfo12* profilerInfo, EVENTPIPE_PROVIDER provider);
        EVENTPIPE_PROVIDER _provider = 0;
        int _currentEventId = 0;
        ComPtr<ICorProfilerInfo12> _profilerInfo;
};