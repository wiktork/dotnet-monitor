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

    const tstring& GetModuleName() const { return _moduleName; }

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

    ModuleID GetModuleId() const { return _moduleId; }
    const tstring& GetName() const { return _className; }
    ClassID GetParentClass() const { return _parentClass; }

private:
    tstring _className;
    ClassID _parentClass = 0x0;
    std::vector<ClassID> _typeArgs;
    ModuleID _moduleId;
};

class FunctionData
{
public:
    FunctionData(ModuleID moduleId, ClassID containingClass, tstring&& name) :
        _moduleId(moduleId), _class(containingClass), _functionName(name)
    {
    }

    ModuleID GetModuleId() const { return _moduleId; }
    const tstring& GetName() const { return _functionName; }
    ClassID GetClass() const { return _class; }

private:
    tstring _functionName;
    ClassID _class;
    ModuleID _moduleId;
    std::vector<ClassID> _typeArgs;
};