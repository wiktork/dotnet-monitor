#pragma once

#include <vector>
#include "cor.h"
#include "corprof.h"
#include "tstring.h"

class ModuleData
{
public:
    ModuleData(tstring&& name) :
        _moduleName(name)
    {
    }

    const tstring& GetName() const { return _moduleName; }

private:
    tstring _moduleName;
};

class ClassData
{
public:
    ClassData(ModuleID moduleId, ClassID parentClass, tstring&& name) :
        _moduleId(moduleId), _parentClass(parentClass), _className(name)
    {
    }

    const ModuleID& GetModuleId() const { return _moduleId; }
    const tstring& GetName() const { return _className; }
    const ClassID& GetParentClass() const { return _parentClass; }
    const std::vector<ClassID>& GetTypeArgs() const { return _typeArgs; }
    void AddTypeArg(ClassID id) { _typeArgs.push_back(id); }

private:
    ModuleID _moduleId;
    ClassID _parentClass = 0x0;
    tstring _className;
    std::vector<ClassID> _typeArgs;
};

class FunctionData
{
public:
    FunctionData(ModuleID moduleId, ClassID containingClass, tstring&& name) :
        _moduleId(moduleId), _class(containingClass), _functionName(name)
    {
    }

    const ModuleID& GetModuleId() const { return _moduleId; }
    const tstring& GetName() const { return _functionName; }
    const ClassID& GetClass() const { return _class; }
    const std::vector<ClassID>& GetTypeArgs() const { return _typeArgs; }
    void AddTypeArg(ClassID classID) { _typeArgs.push_back(classID); }

private:
    ModuleID _moduleId;
    ClassID _class;
    tstring _functionName;
    std::vector<ClassID> _typeArgs;
};