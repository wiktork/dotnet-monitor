// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include "cor.h"
#include "corprof.h"
#include "com.h"
#include <memory>
#include "ProfilerEvent.h"

class ProfilerEventProvider
{
    public:
        static HRESULT CreateProvider(const WCHAR* providerName, ICorProfilerInfo12* profilerInfo, std::unique_ptr<ProfilerEventProvider>& provider);

        HRESULT DefineEvent(const WCHAR* eventName, std::unique_ptr<ProfilerEvent>& profilerEventDescriptor);

        template<int DescLen>
        HRESULT DefineEvent(const WCHAR* eventName, COR_PRF_EVENTPIPE_PARAM_DESC(&descriptors)[DescLen], std::unique_ptr<ProfilerEvent>& profilerEventDescriptor);

    private:
        HRESULT DefineEvent(const WCHAR* eventName, COR_PRF_EVENTPIPE_PARAM_DESC* descriptors, UINT32 descriptorsLen, std::unique_ptr<ProfilerEvent>& profilerEventDescriptor);

    private:
        ProfilerEventProvider(ICorProfilerInfo12* profilerInfo, EVENTPIPE_PROVIDER provider);
        EVENTPIPE_PROVIDER _provider = 0;
        int _currentEventId = 1;
        ComPtr<ICorProfilerInfo12> _profilerInfo;
};

template<int DescLen>
HRESULT ProfilerEventProvider::DefineEvent(const WCHAR* eventName, COR_PRF_EVENTPIPE_PARAM_DESC(&descriptors)[DescLen], std::unique_ptr<ProfilerEvent>& profilerEventDescriptor)
{
    return DefineEvent(eventName, descriptors, DescLen, profilerEventDescriptor);
}
