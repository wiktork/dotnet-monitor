#pragma once

#include "cor.h"
#include "corprof.h"
#include "com.h"
#include "tstring.h"

class TypeNameUtilities
{
    public:
        TypeNameUtilities(ICorProfilerInfo12* profilerInfo) : _profilerInfo(profilerInfo) {}

        HRESULT GetClassName(ClassID id, tstring& className);
    private:
        ComPtr<ICorProfilerInfo12> _profilerInfo;
};