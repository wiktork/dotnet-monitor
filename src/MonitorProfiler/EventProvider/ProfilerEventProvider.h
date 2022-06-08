#pragma once

#include "cor.h"
#include "corprof.h"
#include "com.h"
#include <memory>
#include "ProfilerEvent.h"

class ProfilerEventProvider
{
    public:
        HRESULT CreateProvider(const WCHAR* providerName, ICorProfilerInfo12* profilerInfo, std::unique_ptr<ProfilerEventProvider>& provider);

        template<int DescLen>
        HRESULT DefineEvent(const WCHAR* eventName, COR_PRF_EVENTPIPE_PARAM_DESC descriptor[DescLen], std::unique_ptr<ProfilerEvent>& profilerEventDescriptor);
    private:
        ProfilerEventProvider(ICorProfilerInfo12* profilerInfo, EVENTPIPE_PROVIDER provider);
        EVENTPIPE_PROVIDER _provider = 0;
        int _currentEventId = 0;
        ComPtr<ICorProfilerInfo12> _profilerInfo;
};