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

        HRESULT DefineEvent(const WCHAR* eventName, std::unique_ptr<ProfilerEvent>& profilerEventDescriptor)
        {
            return DefineEvent(eventName, nullptr, 0, profilerEventDescriptor);
        }

        template<int DescLen>
        HRESULT DefineEvent(const WCHAR* eventName, COR_PRF_EVENTPIPE_PARAM_DESC(&descriptors)[DescLen], std::unique_ptr<ProfilerEvent>& profilerEventDescriptor)
        {
            return DefineEvent(eventName, descriptors, DescLen, profilerEventDescriptor);
        }

    private:
        HRESULT DefineEvent(const WCHAR* eventName, COR_PRF_EVENTPIPE_PARAM_DESC* descriptors, UINT32 descriptorsLen, std::unique_ptr<ProfilerEvent>& profilerEventDescriptor);

    private:
        ProfilerEventProvider(ICorProfilerInfo12* profilerInfo, EVENTPIPE_PROVIDER provider);
        EVENTPIPE_PROVIDER _provider = 0;
        int _currentEventId = 0;
        ComPtr<ICorProfilerInfo12> _profilerInfo;
};