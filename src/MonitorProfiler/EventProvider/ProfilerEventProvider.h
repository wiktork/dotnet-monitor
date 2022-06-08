#pragma once

#include "cor.h"
#include "corprof.h"
#include "com.h"
#include <memory>


class ProfilerEventProvider
{
    public:
        HRESULT CreateProvider(ICorProfilerInfo12* profilerInfo, std::unique_ptr<ProfilerEventProvider>& provider);

        HRESULT DefineEvent(COR_PRF_EVENTPIPE_PARAM_DESC descriptor[]);
    private:
        ProfilerEventProvider(const WCHAR* name);
        EVENTPIPE_PROVIDER _provider = 0;
        int _currentEventId = 0;
        ComPtr<ICorProfilerInfo12> _profilerInfo;
};