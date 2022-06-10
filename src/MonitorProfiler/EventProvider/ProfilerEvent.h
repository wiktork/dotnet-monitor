#pragma once

#include "cor.h"
#include "corprof.h"
#include "com.h"

class ProfilerEvent
{
    friend class ProfilerEventProvider;
    public:
        template<size_t Size>
        HRESULT WriteEvent(COR_PRF_EVENT_DATA (&eventData)[Size]);
    private:
        ProfilerEvent(ICorProfilerInfo12* profilerInfo, EVENTPIPE_EVENT event) : _profilerInfo(profilerInfo), _event(event) {}
        ComPtr<ICorProfilerInfo12> _profilerInfo;
        EVENTPIPE_EVENT _event = 0;
};

template<size_t Size>
inline HRESULT ProfilerEvent::WriteEvent(COR_PRF_EVENT_DATA(&eventData)[Size])
{
    return _profilerInfo->EventPipeWriteEvent(_event, Size, eventData, nullptr, nullptr);
}
