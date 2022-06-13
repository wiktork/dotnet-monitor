#pragma once

#include <memory>
#include <functional>
#include <unordered_map>
#include "cor.h"
#include "corprof.h"
#include "tstring.h"
#include "ClrData.h"

class NameCache
{
public:
    bool GetFunctionData(FunctionID id, std::shared_ptr<FunctionData>& data)
    {
        return GetData(_functionNames, id, data);
    }
    bool GetClassData(ClassID id, std::shared_ptr<ClassData>& data)
    {
        return GetData(_classNames, id, data);
    }
    bool GetModuleData(ModuleID id, std::shared_ptr<ModuleData>& data)
    {
        return GetData(_moduleNames, id, data);
    }

    void AddModuleData(ModuleID moduleId, tstring&& name)
    {
        _moduleNames.emplace(moduleId, std::make_shared<ModuleData>(std::move(name)));
    }

    void AddFunctionData(ModuleID moduleId, FunctionID id, tstring&& name, ClassID parent, ClassID* typeArgs, int typeArgsCount)
    {
        std::shared_ptr<FunctionData> functionData = std::make_shared<FunctionData>(moduleId, parent, std::move(name));
        if (typeArgs && typeArgsCount > 0)
        {
            functionData->TypeArgs.assign(typeArgs, typeArgs + typeArgsCount);
        }
        _functionNames.emplace(id, functionData);
    }

    void AddClassData(ModuleID moduleId, ClassID id, tstring&& name, ClassID parent, ClassID* typeArgs, int typeArgsCount)
    {
        std::shared_ptr<ClassData> classData = std::make_shared<ClassData>(moduleId, parent, std::move(name));
        if (typeArgs && typeArgsCount > 0)
        {
            classData->TypeArgs.assign(typeArgs, typeArgs + typeArgsCount);
        }
        _classNames.emplace(id, classData);
    }

    void ForFunctionId(std::function<void(FunctionID, const FunctionData&)> f) {
        ExecuteOver(_functionNames, f);
    }

    void ForClassId(std::function<void(ClassID, const ClassData&)> f) {
        ExecuteOver(_classNames, f);
    }

    void ForModuleId(std::function<void(ModuleID, const ModuleData&)> f) {
        ExecuteOver(_moduleNames, f);
    }


private:
    template<typename T, typename U>
    void ExecuteOver(std::unordered_map<T, std::shared_ptr<U>> map, std::function<void(T, const U&)> f) {
        for (auto& value : map)
        {
            f(value.first, *value.second);
        }
    }

    template<typename T, typename U>
    bool GetData(std::unordered_map<T, std::shared_ptr<U>> map, T id, std::shared_ptr<U>& name) {
        auto const& it = map.find(id);

        if (it != map.end())
        {
            name = it->second;
            return true;
        }
        return false;
    }

    std::unordered_map<ClassID, std::shared_ptr<ClassData>> _classNames;
    std::unordered_map<FunctionID, std::shared_ptr<FunctionData>> _functionNames;
    std::unordered_map<ModuleID, std::shared_ptr<ModuleData>> _moduleNames;
};