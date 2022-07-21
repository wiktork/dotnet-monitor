// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <vector>
#include "cor.h"
#include "corprof.h"

class StackFrame
{
public:
    StackFrame(FunctionID functionId, UINT_PTR offset) : _functionId(functionId), _offset(offset)
    {
    }

    const FunctionID& GetFunctionId() const { return _functionId; }
    const UINT_PTR& GetOffset() const { return _offset; }
private:
    FunctionID _functionId = 0x0;
    UINT_PTR _offset = 0x0;
};

class Stack
{
public:
    const uint64_t& GetThreadId() const { return _tid; }
    void SetThreadId(uint64_t threadid) { _tid = threadid; }
    const std::vector<StackFrame>& GetFrames() const { return _frames; }

    void AddFrame(const StackFrame& frame)
    {
        _frames.push_back(frame);
    }
private:
    uint64_t _tid = 0;
    std::vector<StackFrame> _frames;
};



