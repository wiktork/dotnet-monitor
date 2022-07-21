// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include "../EventProvider/ProfilerEventProvider.h"
#include <memory>
#include "Stack.h"
#include "../Utilities/ClrData.h"

class StacksEventProvider
{
    public:
        static HRESULT CreateProvider(ICorProfilerInfo12* profilerInfo, std::unique_ptr<StacksEventProvider>& eventProvider);

        HRESULT WriteCallstack(const Stack& stack);
        HRESULT WriteClassData(ClassID classId, const ClassData& classData);
        HRESULT WriteFunctionData(FunctionID functionId, const FunctionData& classData);
        HRESULT WriteModuleData(ModuleID moduleId, const ModuleData& classData);
        HRESULT WriteTokenData(ModuleID moduleId, mdTypeDef typeDef, const TokenData& tokenData);
        HRESULT WriteEndEvent();

    private:
        StacksEventProvider(ICorProfilerInfo12* profilerInfo, std::unique_ptr<ProfilerEventProvider> & eventProvider) :
            _profilerInfo(profilerInfo), _provider(std::move(eventProvider))
        {
        }

        static const WCHAR* ProviderName;

        HRESULT DefineEvents();

        ComPtr<ICorProfilerInfo12> _profilerInfo;
        std::unique_ptr<ProfilerEventProvider> _provider;
        std::unique_ptr<ProfilerEvent> _callstackEvent;
        std::unique_ptr<ProfilerEvent> _functionIdEvent;
        std::unique_ptr<ProfilerEvent> _classIdEvent;
        std::unique_ptr<ProfilerEvent> _moduleEvent;
        std::unique_ptr<ProfilerEvent> _tokenEvent;
        std::unique_ptr<ProfilerEvent> _endEvent;
};