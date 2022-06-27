#include "StackSampler.h"
#include "corhlpr.h"
#include "Stack.h"
#include <functional>
#include <memory>
#include "../Utilities/TypeNameUtilities.h"

HRESULT StackSampler::CreateCallstack(std::vector<std::unique_ptr<StackSamplerState>>& stackStates)
{
    HRESULT hr;

    IfFailRet(_profilerInfo->SuspendRuntime());
    auto resumeRuntime = [](ICorProfilerInfo12* profilerInfo) { profilerInfo->ResumeRuntime(); };
    std::unique_ptr<ICorProfilerInfo12, decltype(resumeRuntime)> resumeRuntimeHandle(static_cast<ICorProfilerInfo12*>(_profilerInfo), resumeRuntime);

    ComPtr<ICorProfilerThreadEnum> threadEnum = nullptr;
    IfFailRet(_profilerInfo->EnumThreads(&threadEnum));

    ThreadID threadID;
    ULONG numReturned;

    while ((hr = threadEnum->Next(1, &threadID, &numReturned)) == S_OK)
    {
        std::unique_ptr<StackSamplerState> stackState = std::unique_ptr<StackSamplerState>(new StackSamplerState(_profilerInfo));
        Stack& stack = stackState->CreateStack(threadID);
            //Need to block ThreadDestroyed while stack walking!!! Is this still a  requirement?
        hr = _profilerInfo->DoStackSnapshot(threadID, DoStackSnapshotStackSnapShotCallbackWrapper, COR_PRF_SNAPSHOT_REGISTER_CONTEXT, &stackState, nullptr, 0);

        stackStates.push_back(stackState);

    }

    return S_OK;
}

HRESULT __stdcall StackSampler::DoStackSnapshotStackSnapShotCallbackWrapper(FunctionID funcId, UINT_PTR ip, COR_PRF_FRAME_INFO frameInfo, ULONG32 contextSize, BYTE context[], void* clientData)
{
    HRESULT hr;

    StackSamplerState* state = static_cast<StackSamplerState*>(clientData);
    Stack& stack = state->GetStack();
    stack.AddFrame(StackFrame(funcId, ip));

    NameCache& nameCache = state->GetNameCache();
    TypeNameUtilities nameUtilities(state->GetProfilerInfo());
    IfFailRet(nameUtilities.CacheNames(funcId, frameInfo, nameCache));

    tstring fullyQualifiedName = nameCache.GetFullyQualifiedName(funcId);
    
    return S_OK;
}
