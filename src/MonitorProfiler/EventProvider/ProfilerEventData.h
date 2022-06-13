#pragma once

#include "cor.h"
#include "corprof.h"
#include <vector>
#include "tstring.h"

template<size_t DataSize>
class ProfilerEventData
{
    public:
        template<size_t Index, typename T>
        void WriteData(const T& data)
        {
            static_assert(Index < DataSize);
            eventData[Index].ptr = reinterpret_cast<UINT64>(&data);
            eventData[Index].size = sizeof(UINT64);
        }

        template<size_t Index>
        void WriteData(const tstring& data)
        {
            static_assert(Index < DataSize);

            eventData[Index].ptr = reinterpret_cast<UINT64>(data.c_str());
            eventData[Index].size = buffer.size() * sizeof(data.size() * sizeof(WCHAR) + 2);
        }

        template<size_t Index, typename T>
        void WriteData(const std::vector<T>& data)
        {
            static_assert(Index < DataSize);
            std::vector<BYTE> buffer = std::move(GetEventBuffer(data));

            eventData[Index].ptr = reinterpret_cast<UINT64>(&buffer.data());
            eventData[Index].size = buffer.size() * sizeof(UINT64);
        }
    private:
        template<typename T>
        static std::vector<BYTE> GetEventBuffer(const std::vector<T>& data)
        {
            if (data.size() == 0) {
                return std::vector<BYTE>(0);
            }
            size_t offset = 0;
            size_t bufferSize = data.size() * sizeof(T) + 2;
            std::vector<BYTE> buffer = std::vector<BYTE>(bufferSize);
            WriteToBuffer<UINT16>(buffer.data(), bufferSize, &offset, (UINT16)buffer.size());
            for (T element : data)
            {
                WriteToBuffer<T>(buffer.data(), bufferSize, &offset, element);
            }

            return buffer;
        }

        template<typename T>
        static void WriteToBuffer(BYTE* pBuffer, size_t bufferLength, size_t* pOffset, T value)
        {
            *(T*)(pBuffer + *pOffset) = value;
            *pOffset += sizeof(T);
        }

    private:
        COR_PRF_EVENT_DATA eventData[DataSize];

};