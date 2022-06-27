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
        StackSamplerState(ICorProfilerInfo12* profilerInfo) : _profilerInfo(profilerInfo) {}
        Stack& CreateStack(uint64_t threadId) { _stacks[threadId].SetThreadId(threadId); return _stacks[threadId]; }
        NameCache& GetNameCache() { return _nameCache; }
        ICorProfilerInfo12* GetProfilerInfo() { return _profilerInfo; }
        std::unordered_map<uint64_t, Stack>& GetStacks() { return _stacks; }
    private:
        ComPtr<ICorProfilerInfo12> _profilerInfo;
        std::unordered_map<uint64_t, Stack> _stacks;
        NameCache _nameCache;
};

class StackSampler
{
    public:
        StackSampler(ICorProfilerInfo12* profilerInfo) : _profilerInfo(profilerInfo) {}
        HRESULT CreateCallstack(std::vector<std::unique_ptr<StackSamplerState>>& stackStates);
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