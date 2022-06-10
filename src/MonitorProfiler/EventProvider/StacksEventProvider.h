#pragma once

#include "ProfilerEventProvider.h"
#include <memory>

class StacksEventProvider
{
    public:
        static HRESULT CreateProvider(ICorProfilerInfo12* profilerInfo, std::unique_ptr<StacksEventProvider>& eventProvider);
    private:

        HRESULT DefineEvents();

        HRESULT WriteCallstack();

        StacksEventProvider(ICorProfilerInfo12* profilerInfo, std::unique_ptr<ProfilerEventProvider> & eventProvider) :
            _profilerInfo(profilerInfo), _provider(std::move(eventProvider))
        {
        }

        ComPtr<ICorProfilerInfo12> _profilerInfo;
        std::unique_ptr<ProfilerEventProvider> _provider;
        std::unique_ptr<ProfilerEvent> _callstackEvent;
        std::unique_ptr<ProfilerEvent> _functionIdEvent;
        std::unique_ptr<ProfilerEvent> _classIdEvent;
        std::unique_ptr<ProfilerEvent> _moduleEvent;
        std::unique_ptr<ProfilerEvent> _endEvent;
};