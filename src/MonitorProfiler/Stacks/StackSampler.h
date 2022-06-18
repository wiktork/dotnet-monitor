#pragma once

#include "cor.h"
#include "corprof.h"
#include "com.h"
#include "tstring.h"

//Just use a static Sampler?
class StackSamplerState
{
    public:
        ComPtr<ICorProfilerInfo12> ProfilerInfo;
};

class StackSampler
{
    public:
        StackSampler(ICorProfilerInfo12* profilerInfo) : _profilerInfo(profilerInfo) {}
        HRESULT CreateCallstack();
    private:
        ComPtr<ICorProfilerInfo12> _profilerInfo;
};