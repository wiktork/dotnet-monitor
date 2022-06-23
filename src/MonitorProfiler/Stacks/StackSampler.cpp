#include "StackSampler.h"
#include "corhlpr.h"
#include "Stack.h"
#include <functional>
#include <memory>

HRESULT StackSampler::CreateCallstack()
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
        StackSamplerState stackState(_profilerInfo);
        Stack& stack = stackState.GetStack();
        stack.SetThreadId(static_cast<uint64_t>(threadID));
            //Need to block ThreadDestroyed while stack walking!!! Is this still a  requirement?
        hr = _profilerInfo->DoStackSnapshot(threadID, nullptr, COR_PRF_SNAPSHOT_REGISTER_CONTEXT, &stackState, nullptr, 0);

    }

    return S_OK;
}

HRESULT __stdcall StackSampler::DoStackSnapshotStackSnapShotCallbackWrapper(FunctionID funcId, UINT_PTR ip, COR_PRF_FRAME_INFO frameInfo, ULONG32 contextSize, BYTE context[], void* clientData)
{
    StackSamplerState* state = static_cast<StackSamplerState*>(clientData);
    Stack& stack = state->GetStack();
    stack.AddFrame(StackFrame(funcId, ip));

    

    return S_OK;
}
