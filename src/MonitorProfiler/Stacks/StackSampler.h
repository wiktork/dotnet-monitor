#pragma once

#include "cor.h"
#include "corprof.h"
#include "com.h"
#include "tstring.h"
#include "Stack.h"
#include "../Utilities/NameCache.h"

//Just use a static Sampler?
class StackSamplerState
{
    public:
        StackSamplerState(ICorProfilerInfo12* profilerInfo, std::shared_ptr<NameCache> nameCache) : _profilerInfo(profilerInfo), _nameCache(nameCache) {}
        Stack& GetStack() { return _stack; }
        std::shared_ptr<NameCache> GetNameCache() { return _nameCache; }
        ICorProfilerInfo12* GetProfilerInfo() { return _profilerInfo; }
    private:
        ComPtr<ICorProfilerInfo12> _profilerInfo;
        Stack _stack;
        std::shared_ptr<NameCache> _nameCache;
};

class StackSampler
{
    public:
        StackSampler(ICorProfilerInfo12* profilerInfo) : _profilerInfo(profilerInfo) {}
        HRESULT CreateCallstack(std::vector<std::unique_ptr<StackSamplerState>>& stackStates, std::shared_ptr<NameCache>& nameCache);
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