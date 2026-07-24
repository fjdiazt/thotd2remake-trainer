#define UNICODE
#define _UNICODE

#include <windows.h>
#include <tlhelp32.h>
#include <shellapi.h>

#include <cstdio>
#include <cwchar>

namespace {

constexpr wchar_t kGameExe[] = L"THE HOUSE OF THE DEAD 2 Remake.exe";
constexpr wchar_t kPipePath[] = L"\\\\.\\pipe\\Hotd2RemakeTrainer";
constexpr UINT_PTR kTimerId = 1;
constexpr UINT kTimerIntervalMs = 250;

enum ControlId {
    GodModeCheckbox = 1001,
    AmmoCheckbox,
    ContinuesCheckbox,
};

HWND gStatus = nullptr;
HWND gGodMode = nullptr;
HWND gAmmo = nullptr;
HWND gContinues = nullptr;
HANDLE gPipe = INVALID_HANDLE_VALUE;

void SetStatus(const wchar_t* text) {
    SetWindowTextW(gStatus, text);
}

void Disconnect() {
    if (gPipe != INVALID_HANDLE_VALUE) {
        CloseHandle(gPipe);
        gPipe = INVALID_HANDLE_VALUE;
    }
}

bool IsGameRunning() {
    const HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snapshot == INVALID_HANDLE_VALUE) {
        return false;
    }

    PROCESSENTRY32W entry{sizeof(entry)};
    bool found = false;
    if (Process32FirstW(snapshot, &entry)) {
        do {
            if (_wcsicmp(entry.szExeFile, kGameExe) == 0) {
                found = true;
                break;
            }
        } while (Process32NextW(snapshot, &entry));
    }

    CloseHandle(snapshot);
    return found;
}

bool IsChecked(HWND checkbox) {
    return SendMessageW(checkbox, BM_GETCHECK, 0, 0) == BST_CHECKED;
}

bool SendState(bool godMode, bool ammo, bool continues) {
    if (gPipe == INVALID_HANDLE_VALUE) {
        return false;
    }

    char command[32]{};
    const int length = std::snprintf(
        command,
        sizeof(command),
        "STATE %d %d %d\n",
        godMode ? 1 : 0,
        ammo ? 1 : 0,
        continues ? 1 : 0);
    if (length <= 0) {
        return false;
    }

    DWORD written = 0;
    return WriteFile(
               gPipe,
               command,
               static_cast<DWORD>(length),
               &written,
               nullptr) &&
           written == static_cast<DWORD>(length);
}

bool SendCurrentState() {
    return SendState(
        IsChecked(gGodMode),
        IsChecked(gAmmo),
        IsChecked(gContinues));
}

bool Connect() {
    gPipe = CreateFileW(
        kPipePath,
        GENERIC_WRITE,
        0,
        nullptr,
        OPEN_EXISTING,
        0,
        nullptr);
    return gPipe != INVALID_HANDLE_VALUE;
}

void Tick() {
    if (!IsGameRunning()) {
        Disconnect();
        SetStatus(L"Waiting for Remake...");
        return;
    }

    if (gPipe == INVALID_HANDLE_VALUE && !Connect()) {
        SetStatus(L"Game found; BepInEx bridge offline");
        return;
    }

    if (!SendCurrentState()) {
        Disconnect();
        SetStatus(L"Game found; reconnecting bridge...");
        return;
    }

    SetStatus(L"Connected to Remake");
}

HWND AddCheckbox(HWND parent, const wchar_t* text, int y, int id) {
    return CreateWindowExW(
        0,
        L"BUTTON",
        text,
        WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX | WS_TABSTOP,
        24,
        y,
        330,
        28,
        parent,
        reinterpret_cast<HMENU>(static_cast<INT_PTR>(id)),
        GetModuleHandleW(nullptr),
        nullptr);
}

LRESULT CALLBACK WindowProc(HWND window, UINT message, WPARAM wParam, LPARAM lParam) {
    switch (message) {
    case WM_CREATE: {
        const HFONT font = static_cast<HFONT>(GetStockObject(DEFAULT_GUI_FONT));
        gStatus = CreateWindowExW(
            0,
            L"STATIC",
            L"Waiting for Remake...",
            WS_CHILD | WS_VISIBLE,
            24,
            20,
            350,
            24,
            window,
            nullptr,
            GetModuleHandleW(nullptr),
            nullptr);
        gGodMode = AddCheckbox(window, L"Infinite Health", 58, GodModeCheckbox);
        gAmmo = AddCheckbox(window, L"Infinite Ammo", 92, AmmoCheckbox);
        gContinues = AddCheckbox(window, L"Infinite Continues", 126, ContinuesCheckbox);

        SendMessageW(gStatus, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);
        SendMessageW(gGodMode, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);
        SendMessageW(gAmmo, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);
        SendMessageW(gContinues, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);

        SetTimer(window, kTimerId, kTimerIntervalMs, nullptr);
        Tick();
        return 0;
    }
    case WM_TIMER:
        if (wParam == kTimerId) {
            Tick();
        }
        return 0;
    case WM_COMMAND:
        if (HIWORD(wParam) == BN_CLICKED) {
            Tick();
        }
        return 0;
    case WM_DESTROY:
        KillTimer(window, kTimerId);
        SendState(false, false, false);
        Disconnect();
        PostQuitMessage(0);
        return 0;
    default:
        return DefWindowProcW(window, message, wParam, lParam);
    }
}

bool SelfTest() {
    return _wcsicmp(kGameExe, L"THE HOUSE OF THE DEAD 2 Remake.exe") == 0 &&
           _wcsicmp(kPipePath, L"\\\\.\\pipe\\Hotd2RemakeTrainer") == 0;
}

bool HasSelfTestArgument() {
    int argc = 0;
    wchar_t** argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    if (!argv) {
        return false;
    }

    bool found = false;
    for (int i = 1; i < argc; ++i) {
        if (_wcsicmp(argv[i], L"--self-test") == 0) {
            found = true;
            break;
        }
    }
    LocalFree(argv);
    return found;
}

} // namespace

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int showCommand) {
    if (HasSelfTestArgument()) {
        return SelfTest() ? 0 : 1;
    }

    constexpr wchar_t kClassName[] = L"Hotd2RemakeTrainerWindow";
    WNDCLASSEXW windowClass{
        sizeof(windowClass),
        CS_HREDRAW | CS_VREDRAW,
        WindowProc,
        0,
        0,
        instance,
        LoadIconW(nullptr, IDI_APPLICATION),
        LoadCursorW(nullptr, IDC_ARROW),
        reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1),
        nullptr,
        kClassName,
        LoadIconW(nullptr, IDI_APPLICATION),
    };
    if (!RegisterClassExW(&windowClass)) {
        return 1;
    }

    const HWND window = CreateWindowExW(
        0,
        kClassName,
        L"HotD2 Remake Trainer",
        WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX,
        CW_USEDEFAULT,
        CW_USEDEFAULT,
        410,
        220,
        nullptr,
        nullptr,
        instance,
        nullptr);
    if (!window) {
        return 1;
    }

    ShowWindow(window, showCommand);
    UpdateWindow(window);

    MSG message{};
    while (GetMessageW(&message, nullptr, 0, 0) > 0) {
        TranslateMessage(&message);
        DispatchMessageW(&message);
    }
    return static_cast<int>(message.wParam);
}
