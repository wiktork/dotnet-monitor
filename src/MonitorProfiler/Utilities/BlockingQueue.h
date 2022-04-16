#// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <queue>
#include <mutex>

template<typename T>
class BlockingQueue final
{
public:
    void Push(const T& item)
    {
        {
            std::lock_guard<std::mutex> lock(_mutex);
            _queue.push(item);
        }
        _condition.notify_all();
    }

    T BlockingDequeue()
    {
        std::unique_lock<std::mutex> lock(_mutex);
        _condition.wait(lock, [this]() {return !_queue.empty(); });

        T item = _queue.front();
        _queue.pop();

        return item;
    }

private:
    std::queue<T> _queue;
    std::mutex _mutex;
    std::condition_variable _condition;
};