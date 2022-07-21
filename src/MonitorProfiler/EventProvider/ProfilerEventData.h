// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include "cor.h"
#include "corprof.h"
#include <vector>
#include "tstring.h"

template<size_t DataSize>
class ProfilerEventData
{
    public:
        ProfilerEventData();

        //Because we are using addresses, not the value directly, we bind to an l-value reference.
        template<size_t Index, typename T>
        void WriteData(T& data);

        template<size_t Index>
        void WriteData(const tstring& data);

        template<size_t Index>
        void WriteData(std::vector<BYTE>& data);

        template<size_t Index, typename T>
        void WriteData(const std::vector<T>& data);

        template<typename T>
        static std::vector<BYTE> GetEventBuffer(const std::vector<T>& data);

        template<typename T, typename U>
        static std::vector<BYTE> GetEventBuffer(const std::vector<T>& data, U(*transform)(const T&));

    private:
        template<typename T>
        static void WriteToBuffer(BYTE* pBuffer, size_t bufferLength, size_t* pOffset, const T& value);

    public:
        COR_PRF_EVENT_DATA _eventData[DataSize];
};

template<size_t DataSize>
ProfilerEventData<DataSize>::ProfilerEventData()
{
    memset(_eventData, 0, sizeof(_eventData));
}

template<size_t DataSize>
//Because we are using addresses, not the value directly, we bind to an l-value reference.
template<size_t Index, typename T>
void ProfilerEventData<DataSize>::WriteData(T& data)
{
    static_assert(Index < DataSize);
    _eventData[Index].ptr = reinterpret_cast<UINT64>(&data);
    _eventData[Index].size = sizeof(T);
}

template<size_t DataSize>
template<size_t Index>
void ProfilerEventData<DataSize>::WriteData(const tstring& data)
{
    static_assert(Index < DataSize);

    if (data.size() > 0)
    {
        _eventData[Index].ptr = reinterpret_cast<UINT64>(data.c_str());
        _eventData[Index].size = static_cast<UINT32>((data.size() + 1) * sizeof(WCHAR));
    }
    else
    {
        _eventData[Index].ptr = 0;
        _eventData[Index].size = 0;
    }
}

template<size_t DataSize>
template<size_t Index>
void ProfilerEventData<DataSize>::WriteData(std::vector<BYTE>& data)
{
    static_assert(Index < DataSize);
    if (data.size() > 0)
    {
        _eventData[Index].ptr = reinterpret_cast<UINT64>(data.data());
        _eventData[Index].size = static_cast<UINT32>(data.size());
    }
    else
    {
        _eventData[Index].ptr = 0;
        _eventData[Index].size = 0;
    }
}

template<size_t DataSize>
template<size_t Index, typename T>
void ProfilerEventData<DataSize>::WriteData(const std::vector<T>& data)
{
    static_assert("Invalid serialization");
}

template<size_t DataSize>
template<typename T>
static std::vector<BYTE> ProfilerEventData<DataSize>::GetEventBuffer(const std::vector<T>& data)
{
    return GetEventBuffer<T, T>(data, nullptr);
}

template<size_t DataSize>
template<typename T, typename U>
static std::vector<BYTE> ProfilerEventData<DataSize>::GetEventBuffer(const std::vector<T>& data, U(*transform)(const T&))
{
    size_t offset = 0;
    size_t bufferSize = 2 + (data.size() * sizeof(U));
    std::vector<BYTE> buffer = std::vector<BYTE>(bufferSize);
    WriteToBuffer<UINT16>(buffer.data(), bufferSize, &offset, (UINT16)data.size());

    for (const T& element : data)
    {
        if (transform == nullptr)
        {
            WriteToBuffer<T>(buffer.data(), bufferSize, &offset, element);
        }
        else
        {
            U transformed = transform(element);
            WriteToBuffer<U>(buffer.data(), bufferSize, &offset, transformed);
        }
    }

    return buffer;
}

template<size_t DataSize>
template<typename T>
static void ProfilerEventData<DataSize>::WriteToBuffer(BYTE* pBuffer, size_t bufferLength, size_t* pOffset, const T& value)
{
    *(T*)(pBuffer + *pOffset) = value;
    *pOffset += sizeof(T);
}
