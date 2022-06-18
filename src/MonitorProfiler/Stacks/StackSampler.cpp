#include "StackSampler.h"
#include "corhlpr.h"
#include "Stack.h"
#include <functional>

HRESULT StackSampler::CreateCallstack()
{
    HRESULT hr;

    //RAII to resume runtime here?

    IfFailRet(_profilerInfo->SuspendRuntime());

    ComPtr<ICorProfilerThreadEnum> threadEnum = nullptr;
    IfFailRet(_profilerInfo->EnumThreads(&threadEnum));

    ThreadID threadID;
    ULONG numReturned;
    while ((hr = threadEnum->Next(1, &threadID, &numReturned)) == S_OK)
    {
        Stack stack;
        stack.SetThreadId((uint64_t)threadID);

        hr = _profilerInfo->DoStackSnapshot(threadID, nullptr, COR_PRF_SNAPSHOT_REGISTER_CONTEXT, &stack, nullptr, 0);
    }


    //_profilerInfo->ResumeRuntime();
    return S_OK;
}
