#include "ProfilerEventProvider.h"

HRESULT ProfilerEventProvider::CreateProvider(ICorProfilerInfo12* profilerInfo, std::unique_ptr<ProfilerEventProvider>& provider)
{
    return E_NOTIMPL;
}

ProfilerEventProvider::ProfilerEventProvider(const WCHAR* name)
{
}
