#pragma once

#include "cor.h"
#include "corprof.h"
#include "com.h"
#include "tstring.h"
#include "NameCache.h"

class TypeNameUtilities
{
    public:
        TypeNameUtilities(ICorProfilerInfo12* profilerInfo) : _profilerInfo(profilerInfo) {}
        HRESULT CacheNames(FunctionID functionId, COR_PRF_FRAME_INFO frameInfo, NameCache& nameCache);
    private:
        HRESULT GetFunctionInfo(NameCache& nameCache, FunctionID id, COR_PRF_FRAME_INFO frameInfo);
        HRESULT GetClassInfo(NameCache& nameCache, ClassID classId);
        HRESULT GetModuleInfo(NameCache& nameCache, ModuleID moduleId);
    private:
        ComPtr<ICorProfilerInfo12> _profilerInfo;
};