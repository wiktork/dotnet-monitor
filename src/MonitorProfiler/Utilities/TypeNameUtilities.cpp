#include "TypeNameUtilities.h"
#include "corhlpr.h"

HRESULT TypeNameUtilities::CacheNames(FunctionID functionId, COR_PRF_FRAME_INFO frameInfo, NameCache& nameCache)
{
    std::shared_ptr<FunctionData> functionData;
    if (nameCache.GetFunctionData(functionId, functionData))
    {
        return S_OK;
    }



    return S_OK;
}

HRESULT TypeNameUtilities::GetFunctionInfo(NameCache& nameCache, FunctionID id, COR_PRF_FRAME_INFO frameInfo)
{
    if (id == 0) 
    {
        return E_INVALIDARG;
    }

    ClassID classId = 0;
    ModuleID moduleId = 0;
    mdToken token = mdTokenNil;
    ULONG32 typeArgsCount = 0;
    ClassID typeArgs[32];
    HRESULT hr;

    IfFailRet(_profilerInfo->GetFunctionInfo2(id,
        frameInfo,
        &classId,
        &moduleId,
        &token,
        sizeof(typeArgs) / sizeof(ClassID),
        &typeArgsCount,
        typeArgs));

    ComPtr<IMetaDataImport> pIMDImport;
    IfFailRet(_profilerInfo->GetModuleMetaData(moduleId,
        ofRead,
        IID_IMetaDataImport,
        (IUnknown**)&pIMDImport));

    WCHAR funcName[256];
    IfFailRet(pIMDImport->GetMethodProps(token,
        NULL,
        funcName,
        256,
        0,
        0,
        NULL,
        NULL,
        NULL,
        NULL));

    IfFailRet(GetModuleInfo(nameCache, moduleId));

    nameCache.AddFunctionData(moduleId, id, std::move(std::wstring(funcName)), classId, typeArgs, typeArgsCount);


    // If the ClassID returned from GetFunctionInfo is 0, then the function is a shared generic function.
    if (classId != 0)
    {
        IfFailRet(GetClassInfo(nameCache, classId));
    }

    for (ULONG32 i = 0; i < typeArgsCount; i++)
    {
        IfFailRet(GetClassInfo(nameCache, typeArgs[i]));
    }

    return S_OK;
}

HRESULT TypeNameUtilities::GetClassInfo(NameCache& nameCache, ClassID classId)
{
    std::shared_ptr<ClassData> classData;
    if (nameCache.GetClassData(classId, classData))
    {
        return S_OK;
    }

    if (classId == 0)
    {
        return E_INVALIDARG;
    }

    ModuleID modId;
    mdTypeDef classToken;
    ClassID parentClassID;
    ULONG32 nTypeArgs;
    ClassID typeArgs[32];
    HRESULT hr = S_OK;

    tstring placeholderName;

    IfFailRet(_profilerInfo->GetClassIDInfo2(classId,
        &modId,
        &classToken,
        &parentClassID,
        32,
        &nTypeArgs,
        typeArgs));

    if (CORPROF_E_CLASSID_IS_ARRAY == hr)
    {
        placeholderName = _T("ArrayClass");
    }
    else if (CORPROF_E_CLASSID_IS_COMPOSITE == hr)
    {
        // We have a composite class
        placeholderName = _T("CompositeClass");
    }
    else if (CORPROF_E_DATAINCOMPLETE == hr)
    {
        // type-loading is not yet complete. Cannot do anything about it.
        placeholderName = _T("DataIncomplete");
    }
    else if (FAILED(hr))
    {
        placeholderName = _T("Unknown");
    }

    if (placeholderName.size() == 0)
    {
        ComPtr<IMetaDataImport> pMDImport;
        IfFailRet(_profilerInfo->GetModuleMetaData(modId,
            (ofRead | ofWrite),
            IID_IMetaDataImport,
            (IUnknown**)&pMDImport));

        WCHAR wName[256];
        DWORD dwTypeDefFlags = 0;
        IfFailRet(pMDImport->GetTypeDefProps(classToken,
            wName,
            256,
            NULL,
            &dwTypeDefFlags,
            NULL));

        IfFailRet(GetModuleInfo(nameCache, modId));
        for (ULONG32 i = 0; i < nTypeArgs; i++)
        {
            IfFailRet(GetClassInfo(nameCache, typeArgs[i]));
        }

        placeholderName = std::wstring(wName);
    }

    nameCache.AddClassData(modId, classId, std::move(placeholderName), parentClassID, typeArgs, nTypeArgs);

    return S_OK;
}

HRESULT TypeNameUtilities::GetModuleInfo(NameCache& nameCache, ModuleID moduleId)
{
    if (moduleId == 0)
    {
        return E_INVALIDARG;
    }

    HRESULT hr;

    std::shared_ptr<ModuleData> mod;
    if (nameCache.GetModuleData(moduleId, mod))
    {
        return S_OK;
    }

    WCHAR moduleFullName[256];
    ULONG nameLength = 0;
    AssemblyID assemID;

    IfFailRet(_profilerInfo->GetModuleInfo(moduleId,
        nullptr,
        256,
        &nameLength,
        moduleFullName,
        &assemID));

    WCHAR* ptr = NULL;
    WCHAR* index = moduleFullName;
    // Find the last occurence of the \ character
    while (*index != 0)
    {
        if (*index == '\\' || *index == '/')
        {
            ptr = index;
        }

        ++index;
    }
    tstring moduleName;
    if (ptr == nullptr)
    {
        moduleName = moduleFullName;
    }
    // Skip the last \ in the string
    ++ptr;

    while (*ptr != 0)
    {
        moduleName += *ptr;
        ++ptr;
    }

    nameCache.AddModuleData(moduleId, std::move(moduleName));

    return S_OK;
}
