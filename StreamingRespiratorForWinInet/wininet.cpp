#include <string>

#include <Windows.h>
#include <WinInet.h>
#pragma comment(lib, "wininet.lib")

#include "minhook-1.3.3\include\MinHook.h"

#ifdef _DEBUG
const wchar_t* const DebugLogHeader = L"[Streaming Respirator: WinInet] ";

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

static wchar_t g_proxyHost[32] = L"";

typedef HINTERNET(WINAPI *D_InternetOpenW)(
    LPCWSTR lpszAgent,
    DWORD dwAccessType,
    LPCWSTR lpszProxy,
    LPCWSTR lpszProxyBypass,
    DWORD dwFlags
    );

D_InternetOpenW m_InternetOpenW_native;

HINTERNET WINAPI _InternetOpenW(
    LPCWSTR lpszAgent,
    DWORD dwAccessType,
    LPCWSTR lpszProxy,
    LPCWSTR lpszProxyBypass,
    DWORD dwFlags
)
{
    DebugLog(L"===========================");
    DebugLog(L"D_InternetOpenW");

    HINTERNET res = m_InternetOpenW_native(lpszAgent, INTERNET_OPEN_TYPE_PROXY, g_proxyHost, NULL, dwFlags);
    DebugLog(L"res : %ld", res);

    return res;
}

__declspec(dllexport) DWORD SetProxyPort(int32_t port)
{
    std::wstring proxyHost(L"127.0.0.1:");
    proxyHost.append(std::to_wstring(port));
    DebugLog(proxyHost.c_str());

    proxyHost.copy((wchar_t*)g_proxyHost, proxyHost.size());

    DebugLog(L"SetProxyPort : url  : %s", g_proxyHost);

    return 1;
}

static bool m_hooked = false;
__declspec(dllexport) BOOL APIENTRY DllMain(HINSTANCE hinstDLL, DWORD fdwReason, LPVOID lpvReserved)
{
    switch (fdwReason)
    {
    case DLL_PROCESS_ATTACH:
        if (!m_hooked)
        {
            DebugLog(L"DLL_PROCESS_ATTACH");

            if (MH_Initialize() == MH_OK)
            {
                MH_CreateHook(&InternetOpenW, &_InternetOpenW, (LPVOID*)&m_InternetOpenW_native);
                MH_EnableHook(&InternetOpenW);

                m_hooked = true;
            }
        }

        break;
    }

    return TRUE;
}
