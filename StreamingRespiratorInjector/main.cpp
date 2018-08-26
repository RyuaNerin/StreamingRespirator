#include <string>

#include <windows.h>
#include <tlhelp32.h>

#ifdef _DEBUG
const wchar_t* const DebugLogHeader = L"[Streaming Respirator: Injector] ";

void DebugLog(const TCHAR *fmt, ...)
{
#ifdef _UNICODE
#define lvsnprintf  vswprintf
#else
#define lvsnprintf  vsnprintf
#endif

    va_list	va;
    TCHAR *str;
    int len;

    va_start(va, fmt);

    len = lstrlenW(DebugLogHeader) + vswprintf(NULL, 0, fmt, va) + 2;
    str = (TCHAR*)calloc(len, sizeof(TCHAR));
    if (str == NULL)
    {
        va_end(va);
        return;
    }

    len = wsprintf(str, DebugLogHeader);
    wvsprintf(str + len, fmt, va);

    va_end(va);

    OutputDebugString(str);

    free(str);
}
#else
#define DebugLog
#endif

std::wstring getExeDirectory();

HMODULE isInjected(DWORD pid, LPCTSTR moduleName);
HMODULE injectDll(HANDLE hProcess, LPCTSTR path);
bool setProxyPort(HANDLE hProcess, HMODULE hModule, LPCWSTR dllPath, int32_t port);

int wmain(int argc, wchar_t *argv[], wchar_t *envp[])
{
    if (argc != 4)
        return 0;

    const DWORD    pid  = (DWORD)std::wcstol(argv[1], NULL, 10);
    const DWORD    port = (DWORD)std::wcstol(argv[2], NULL, 10);

    std::wstring dllPath = getExeDirectory();
    dllPath.append(argv[3]);

    const wchar_t* dllPathPtr = dllPath.c_str();

    DebugLog(dllPathPtr);

    DebugLog(L"OpenProcess");
    HANDLE hProcess = OpenProcess(PROCESS_ALL_ACCESS, FALSE, pid);
    if (hProcess == NULL)
        return 0;

    DebugLog(L"isInjected");
    HMODULE hInjected = isInjected(pid, dllPathPtr);

    if (hInjected == NULL)
    {
        DebugLog(L"inject");
        hInjected = injectDll(hProcess, dllPathPtr);

        if (hInjected == NULL)
            return 0;
    }

    DebugLog(L"setProxyPort");
    setProxyPort(hProcess, hInjected, dllPathPtr, port);

    CloseHandle(hProcess);

    return 1;
}

std::wstring getExeDirectory()
{
    wchar_t buff[MAX_PATH];
    GetModuleFileNameW(NULL, buff, MAX_PATH);
    size_t len = wcsrchr(buff, L'\\') + 1 - buff;
    return std::wstring(buff, len);
}


HMODULE isInjected(DWORD pid, LPCTSTR moduleName)
{
    HMODULE res = NULL;

    MODULEENTRY32W snapEntry = { 0 };
    HANDLE hSnapshot;

    snapEntry.dwSize = sizeof(MODULEENTRY32);
    hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE, pid);
    if (hSnapshot == NULL)
        return FALSE;

    if (Module32FirstW(hSnapshot, &snapEntry))
    {
        do
        {
            if (!lstrcmpW(snapEntry.szModule, moduleName))
            {
                res = snapEntry.hModule;
                break;
            }
        } while (Module32NextW(hSnapshot, & snapEntry));
    }
    CloseHandle(hSnapshot);

    return res;
}

HMODULE injectDll(HANDLE hProcess, LPCTSTR path)
{
    HMODULE res = NULL;

    DebugLog(L"hKernel32");
    HMODULE hKernel32 = GetModuleHandleW(L"kernel32.dll");
    if (hKernel32 == NULL)
        return FALSE;

    DebugLog(L"lpLoadLibrary");
    LPTHREAD_START_ROUTINE lpLoadLibrary = (LPTHREAD_START_ROUTINE)GetProcAddress(hKernel32, "LoadLibraryW");
    if (lpLoadLibrary == NULL)
        return FALSE;

    DebugLog(L"VirtualAllocEx");
    SIZE_T pBuffSize = (std::wcslen(path) + 1) * sizeof(wchar_t);
    LPVOID pBuff = VirtualAllocEx(hProcess, NULL, pBuffSize, MEM_COMMIT, PAGE_READWRITE);
    if (pBuff != NULL)
    {
        DebugLog(L"WriteProcessMemory");
        if (WriteProcessMemory(hProcess, pBuff, (LPVOID)path, pBuffSize, NULL))
        {
            DebugLog(L"CreateRemoteThread");
            HANDLE hThread = CreateRemoteThread(hProcess, NULL, 0, lpLoadLibrary, pBuff, 0, NULL);
            if (hThread != NULL)
            {
                DebugLog(L"hThread");
                WaitForSingleObject(hThread, INFINITE);

                DebugLog(L"GetExitCodeThread");

                HMODULE hInjected = NULL;
                if (GetExitCodeThread(hThread, (LPDWORD)&hInjected) && hInjected)
                    res = hInjected;

                CloseHandle(hThread);
            }
        }

        DebugLog(L"VirtualFreeEx");
        VirtualFreeEx(hProcess, pBuff, 0, MEM_RELEASE);
    }

    return res;
}

void* GetFunctionAddr(LPCWSTR dllPath, HMODULE hInjected, LPCSTR functionName)
{
    DebugLog(L"LoadLibraryW");
    HMODULE hLoaded = LoadLibraryW(dllPath);

    if (hLoaded == NULL) {
        return NULL;
    }
    else {
        DebugLog(L"GetProcAddress");
        void* lpFunc = GetProcAddress(hLoaded, functionName);
        size_t dwOffset = (char*)lpFunc - (char*)hLoaded;

        DebugLog(L"FreeLibrary");
        FreeLibrary(hLoaded);
        return (void*)((size_t)hInjected + dwOffset);
    }
}

bool setProxyPort(HANDLE hProcess, HMODULE hModule, LPCWSTR dllPath, int32_t port)
{
    bool res = false;

    DebugLog(L"GetPayloadExportAddr");
    LPTHREAD_START_ROUTINE setProxyPort = (LPTHREAD_START_ROUTINE)GetFunctionAddr(dllPath, hModule, "SetProxyPort");

    if (setProxyPort != NULL)
    {
        DebugLog(L"CreateRemoteThread");
        HANDLE hThread = CreateRemoteThread(hProcess, NULL, 0, setProxyPort, (LPVOID)port, 0, NULL);
        if (hThread != NULL)
        {
            DebugLog(L"WaitForSingleObject");
            WaitForSingleObject(hThread, INFINITE);

            DebugLog(L"GetExitCodeThread");
            DWORD exitCode;
            res = GetExitCodeThread(hThread, &exitCode) && exitCode;

            DebugLog(L"CloseHandle");
            CloseHandle(hThread);
        }
    }

    return res;
}
