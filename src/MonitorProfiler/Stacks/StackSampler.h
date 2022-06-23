#pragma once

#include "cor.h"
#include "corprof.h"
#include "com.h"
#include "tstring.h"
#include "Stack.h"

//Just use a static Sampler?
class StackSamplerState
{
    public:
        StackSamplerState(ICorProfilerInfo12* profilerInfo) : _profilerInfo(profilerInfo) {}
        Stack& GetStack() { return _stack; }
    private:
        ComPtr<ICorProfilerInfo12> _profilerInfo;
        Stack _stack;
};

class StackSampler
{
    public:
        StackSampler(ICorProfilerInfo12* profilerInfo) : _profilerInfo(profilerInfo) {}
        HRESULT CreateCallstack();
    private:
        static HRESULT __stdcall DoStackSnapshotStackSnapShotCallbackWrapper(
            FunctionID funcId,
            UINT_PTR ip,
            COR_PRF_FRAME_INFO frameInfo,
            ULONG32 contextSize,
            BYTE context[],
            void* clientData);

        ComPtr<ICorProfilerInfo12> _profilerInfo;
};