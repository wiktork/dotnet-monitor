#pragma once

#include "ProfilerEventProvider.h"
#include <memory>

class StacksEventProvider
{
    public:
        static HRESULT CreateProvider(ICorProfilerInfo12* profilerInfo, std::unique_ptr<StacksEventProvider>& eventProvider);
    private:
        //static COR_PRF_EVENTPIPE_PARAM_DESC CallStackEventDescriptor[];
        //static COR_PRF_EVENTPIPE_PARAM_DESC FunctionIdDescriptor[];
        //static COR_PRF_EVENTPIPE_PARAM_DESC ClassIdDescriptor[];
        //static COR_PRF_EVENTPIPE_PARAM_DESC ModuleDescriptor[];

        HRESULT DefineEvents();

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