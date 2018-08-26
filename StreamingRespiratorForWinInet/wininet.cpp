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

const wchar_t* const TwitterStreamingHost = L"userstream.twitter.com";

static int32_t g_localPort = 0;
static wchar_t g_proxyHost[32] = L"";

typedef HINTERNET(WINAPI *D_InternetConnectW)(
    HINTERNET hInternet,
    LPCWSTR lpszServerName,
    INTERNET_PORT nServerPort,
    LPCWSTR lpszUserName,
    LPCWSTR lpszPassword,
    DWORD dwService,
    DWORD dwFlags,
    DWORD_PTR dwContext
    );

D_InternetConnectW m_internetConnect_native;

HINTERNET WINAPI _InternetConnectW(
    HINTERNET hInternet,
    LPCWSTR lpszServerName,
    INTERNET_PORT nServerPort,
    LPCWSTR lpszUserName,
    LPCWSTR lpszPassword,
    DWORD dwService,
    DWORD dwFlags,
    DWORD_PTR dwContext
)
{
    DebugLog(L"===========================");
    DebugLog(L"InternetConnectW : %s", lpszServerName);

    if (g_localPort != 0)
    {
        size_t len = std::wcslen(lpszServerName);
        len = min(len, sizeof(TwitterStreamingHost));

        if (std::wcsncmp(lpszServerName, TwitterStreamingHost, len) == 0)
        {
            /*
            INTERNET_PROXY_INFO proxy = { 0, };
            proxy.dwAccessType = INTERNET_OPEN_TYPE_PROXY;
            proxy.lpszProxy = g_proxyHost;
            proxy.lpszProxyBypass = g_proxyHost;

            BOOL proxySetResult = InternetSetOptionW(hInternet, INTERNET_OPTION_PROXY, &proxy, sizeof(proxy));
            DebugLog(L"InternetConnectW patched : %d", proxySetResult);
            */

            HINTERNET res = m_internetConnect_native(hInternet, L"localhost", g_localPort, lpszUserName, lpszPassword, dwService, dwFlags, dwContext);
            DebugLog(L"result : %ld", res);
            return res;
        }
    }

    HINTERNET res = m_internetConnect_native(hInternet, lpszServerName, nServerPort, lpszUserName, lpszPassword, dwService, dwFlags, dwContext);
    DebugLog(L"InternetConnectW Result : %ld", (size_t)res);

    return res;
}

__declspec(dllexport) DWORD SetProxyPort(int32_t port)
{
    std::wstring proxyHost(L"127.0.0.1:");
    proxyHost.append(std::to_wstring(port));
    DebugLog(proxyHost.c_str());

    proxyHost.copy((wchar_t*)g_proxyHost, proxyHost.size());

    g_localPort = port;

    DebugLog(L"SetProxyPort : port : %d", g_localPort);
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
                MH_CreateHook(&InternetConnectW, &_InternetConnectW, (LPVOID*)&m_internetConnect_native);
                MH_EnableHook(&InternetConnectW);

                m_hooked = true;
            }
        }

        break;
    }

    return TRUE;
}
