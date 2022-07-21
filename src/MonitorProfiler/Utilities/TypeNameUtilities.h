// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include "cor.h"
#include "corprof.h"
#include "com.h"
#include "tstring.h"
#include "NameCache.h"

class TypeNameUtilities
{
    public:
        TypeNameUtilities(ICorProfilerInfo12* profilerInfo);
        HRESULT CacheNames(FunctionID functionId, COR_PRF_FRAME_INFO frameInfo, NameCache& nameCache);
    private:
        HRESULT GetFunctionInfo(NameCache& nameCache, FunctionID id, COR_PRF_FRAME_INFO frameInfo);
        HRESULT GetClassInfo(NameCache& nameCache, ClassID classId);
        HRESULT GetModuleInfo(NameCache& nameCache, ModuleID moduleId);
    private:
        ComPtr<ICorProfilerInfo12> _profilerInfo;
};