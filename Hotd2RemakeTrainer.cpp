#define UNICODE
#define _UNICODE

#include <windows.h>
#include <tlhelp32.h>
#include <shellapi.h>
#include <commctrl.h>

#include <cstdio>
#include <cstring>
#include <cwchar>

namespace {

constexpr wchar_t kGameExe[] = L"THE HOUSE OF THE DEAD 2 Remake.exe";
constexpr wchar_t kPipePath[] = L"\\\\.\\pipe\\Hotd2RemakeTrainer";
constexpr UINT_PTR kTimerId = 1;
constexpr UINT kTimerIntervalMs = 250;
constexpr int kMinRapidFireRate = 2;
constexpr int kMaxRapidFireRate = 16;
constexpr int kDefaultRapidFireRate = 8;

enum ControlId {
    GodModeCheckbox = 1001,
    AmmoCheckbox,
    ContinuesCheckbox,
    OneShotCheckbox,
    EasyBossCheckbox,
    ZeroDamageCheckbox,
    AllWeaponsCheckbox,
    FireOffRadio,
    AutoFireRadio,
    RapidFireRadio,
    RapidFireRateEdit,
    RapidFireRateSpinner,
    PersistCheckbox,
    UnlockChaptersButton,
    UnlockBestiaryButton,
    UnlockTrainingButton,
    UnlockTrainingStarsButton,
    UnlockBossButton,
    UnlockBossStarsButton,
    UnlockTrunkButton,
    UnlockAchievementsButton,
};

HWND gStatus = nullptr;
HWND gGodMode = nullptr;
HWND gAmmo = nullptr;
HWND gContinues = nullptr;
HWND gOneShot = nullptr;
HWND gEasyBoss = nullptr;
HWND gZeroDamage = nullptr;
HWND gAllWeapons = nullptr;
HWND gGameplayGroup = nullptr;
HWND gFireGroup = nullptr;
HWND gProgressionGroup = nullptr;
HWND gFireOff = nullptr;
HWND gAutoFire = nullptr;
HWND gRapidFire = nullptr;
HWND gRapidFireRateLabel = nullptr;
HWND gRapidFireRateEdit = nullptr;
HWND gRapidFireRateSpinner = nullptr;
HWND gPersist = nullptr;
HWND gActionStatus = nullptr;
HWND gActionButtons[8]{};
HANDLE gPipe = INVALID_HANDLE_VALUE;
bool gLocalStatePending = false;
bool gApplyingState = false;

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

int GetRapidFireRate() {
    BOOL error = FALSE;
    const int rate = static_cast<int>(SendMessageW(
        gRapidFireRateSpinner,
        UDM_GETPOS32,
        0,
        reinterpret_cast<LPARAM>(&error)));
    return !error && rate >= kMinRapidFireRate && rate <= kMaxRapidFireRate
        ? rate
        : kDefaultRapidFireRate;
}

void SetRapidFireRate(int rate) {
    SendMessageW(gRapidFireRateSpinner, UDM_SETPOS32, 0, rate);
    wchar_t text[4]{};
    swprintf_s(text, L"%d", rate);
    SetWindowTextW(gRapidFireRateEdit, text);
}

void UpdateRapidFireRateEnabled() {
    const BOOL enabled = IsChecked(gRapidFire);
    EnableWindow(gRapidFireRateLabel, enabled);
    EnableWindow(gRapidFireRateEdit, enabled);
    EnableWindow(gRapidFireRateSpinner, enabled);
}

bool IsUnlockAction(int cheatType) {
    switch (cheatType) {
    case 7:  // Training modes
    case 9:  // Trunk items
    case 11: // Boss modes
    case 13: // Chapters
    case 15: // Bestiary
    case 16: // Achievements
    case 17: // Boss mode stars
    case 18: // Training mode stars
        return true;
    default:
        return false;
    }
}

int FormatActionCommand(
    char* command, std::size_t size, int cheatType) {
    if (!IsUnlockAction(cheatType)) {
        return -1;
    }
    return std::snprintf(command, size, "ACTION %d\n", cheatType);
}

int FormatStateCommand(
    char* command,
    std::size_t size,
    bool godMode,
    bool ammo,
    bool continues,
    bool autoFire,
    bool persist,
    bool rapidFire,
    bool oneShot,
    bool easyBoss,
    bool zeroDamage,
    bool allWeapons,
    int rapidFireRate) {
    return std::snprintf(
        command,
        size,
        "STATE %d %d %d %d %d %d %d %d %d %d %d\n",
        godMode ? 1 : 0,
        ammo ? 1 : 0,
        continues ? 1 : 0,
        autoFire ? 1 : 0,
        persist ? 1 : 0,
        rapidFire ? 1 : 0,
        oneShot ? 1 : 0,
        easyBoss ? 1 : 0,
        zeroDamage ? 1 : 0,
        allWeapons ? 1 : 0,
        rapidFireRate);
}

bool ParseStateCommand(
    const char* command,
    bool& godMode,
    bool& ammo,
    bool& continues,
    bool& autoFire,
    bool& persist,
    bool& rapidFire,
    bool& oneShot,
    bool& easyBoss,
    bool& zeroDamage,
    bool& allWeapons,
    int& rapidFireRate) {
    int values[11]{};
    int consumed = 0;
    if (sscanf_s(
            command,
            "STATE %d %d %d %d %d %d %d %d %d %d %d%n",
            &values[0],
            &values[1],
            &values[2],
            &values[3],
            &values[4],
            &values[5],
            &values[6],
            &values[7],
            &values[8],
            &values[9],
            &values[10],
            &consumed) != 11) {
        return false;
    }

    for (int index = 0; index < 10; ++index) {
        if (values[index] != 0 && values[index] != 1) {
            return false;
        }
    }
    if (values[3] != 0 && values[5] != 0) {
        return false;
    }
    if (values[10] < kMinRapidFireRate ||
        values[10] > kMaxRapidFireRate) {
        return false;
    }
    for (const char* rest = command + consumed; *rest; ++rest) {
        if (*rest != '\r' && *rest != '\n') {
            return false;
        }
    }

    godMode = values[0] != 0;
    ammo = values[1] != 0;
    continues = values[2] != 0;
    autoFire = values[3] != 0;
    persist = values[4] != 0;
    rapidFire = values[5] != 0;
    oneShot = values[6] != 0;
    easyBoss = values[7] != 0;
    zeroDamage = values[8] != 0;
    allWeapons = values[9] != 0;
    rapidFireRate = values[10];
    return true;
}
bool SendState(
    bool godMode,
    bool ammo,
    bool continues,
    bool autoFire,
    bool persist,
    bool rapidFire,
    bool oneShot,
    bool easyBoss,
    bool zeroDamage,
    bool allWeapons,
    int rapidFireRate) {
    if (gPipe == INVALID_HANDLE_VALUE) {
        return false;
    }
    if (rapidFireRate < kMinRapidFireRate ||
        rapidFireRate > kMaxRapidFireRate) {
        return false;
    }

    char command[64]{};
    const int length = FormatStateCommand(
        command,
        sizeof(command),
        godMode,
        ammo,
        continues,
        autoFire,
        persist,
        rapidFire,
        oneShot,
        easyBoss,
        zeroDamage,
        allWeapons,
        rapidFireRate);
    if (length <= 0 || static_cast<std::size_t>(length) >= sizeof(command)) {
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

bool SendAction(int cheatType) {
    if (gPipe == INVALID_HANDLE_VALUE) {
        return false;
    }

    char command[16]{};
    const int length =
        FormatActionCommand(command, sizeof(command), cheatType);
    if (length <= 0 || static_cast<std::size_t>(length) >= sizeof(command)) {
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
        IsChecked(gContinues),
        IsChecked(gAutoFire),
        IsChecked(gPersist),
        IsChecked(gRapidFire),
        IsChecked(gOneShot),
        IsChecked(gEasyBoss),
        IsChecked(gZeroDamage),
        IsChecked(gAllWeapons),
        GetRapidFireRate());
}
bool ReceiveCurrentState(bool applyState) {
    DWORD available = 0;
    for (int attempt = 0; attempt < 100; ++attempt) {
        if (!PeekNamedPipe(gPipe, nullptr, 0, nullptr, &available, nullptr)) {
            return false;
        }
        if (available != 0) {
            break;
        }
        Sleep(10);
    }
    if (available == 0) {
        return false;
    }

    char response[64]{};
    const DWORD capacity = static_cast<DWORD>(sizeof(response) - 1);
    const DWORD toRead = available < capacity ? available : capacity;
    DWORD read = 0;
    if (!ReadFile(gPipe, response, toRead, &read, nullptr) || read == 0) {
        return false;
    }
    response[read] = '\0';

    bool godMode = false;
    bool ammo = false;
    bool continues = false;
    bool autoFire = false;
    bool persist = false;
    bool rapidFire = false;
    bool oneShot = false;
    bool easyBoss = false;
    bool zeroDamage = false;
    bool allWeapons = false;
    int rapidFireRate = kDefaultRapidFireRate;
    if (!ParseStateCommand(
            response,
            godMode,
            ammo,
            continues,
            autoFire,
            persist,
            rapidFire,
            oneShot,
            easyBoss,
            zeroDamage,
            allWeapons,
            rapidFireRate)) {
        return false;
    }

    if (!applyState) {
        return true;
    }

    SendMessageW(gGodMode, BM_SETCHECK, godMode ? BST_CHECKED : BST_UNCHECKED, 0);
    SendMessageW(gAmmo, BM_SETCHECK, ammo ? BST_CHECKED : BST_UNCHECKED, 0);
    SendMessageW(
        gContinues, BM_SETCHECK, continues ? BST_CHECKED : BST_UNCHECKED, 0);
    SendMessageW(
        gOneShot, BM_SETCHECK, oneShot ? BST_CHECKED : BST_UNCHECKED, 0);
    SendMessageW(
        gEasyBoss, BM_SETCHECK, easyBoss ? BST_CHECKED : BST_UNCHECKED, 0);
    SendMessageW(
        gZeroDamage, BM_SETCHECK, zeroDamage ? BST_CHECKED : BST_UNCHECKED, 0);
    SendMessageW(
        gAllWeapons, BM_SETCHECK, allWeapons ? BST_CHECKED : BST_UNCHECKED, 0);
    SendMessageW(
        gFireOff,
        BM_SETCHECK,
        !autoFire && !rapidFire ? BST_CHECKED : BST_UNCHECKED,
        0);
    SendMessageW(
        gAutoFire, BM_SETCHECK, autoFire ? BST_CHECKED : BST_UNCHECKED, 0);
    SendMessageW(
        gRapidFire, BM_SETCHECK, rapidFire ? BST_CHECKED : BST_UNCHECKED, 0);
    gApplyingState = true;
    SetRapidFireRate(rapidFireRate);
    gApplyingState = false;
    UpdateRapidFireRateEnabled();
    SendMessageW(
        gPersist, BM_SETCHECK, persist ? BST_CHECKED : BST_UNCHECKED, 0);
    return true;
}

bool Connect() {
    gPipe = CreateFileW(
        kPipePath,
        GENERIC_READ | GENERIC_WRITE,
        0,
        nullptr,
        OPEN_EXISTING,
        0,
        nullptr);
    if (gPipe == INVALID_HANDLE_VALUE) {
        return false;
    }
    if (!ReceiveCurrentState(!gLocalStatePending)) {
        Disconnect();
        return false;
    }
    SetWindowTextW(
        gActionStatus,
        L"Ready. Unlock actions apply to the current save.");
    return true;
}

void SetActionButtonsEnabled(bool enabled) {
    for (HWND button : gActionButtons) {
        EnableWindow(button, enabled ? TRUE : FALSE);
    }
}

void Tick() {
    if (!IsGameRunning()) {
        Disconnect();
        SetActionButtonsEnabled(false);
        SetWindowTextW(
            gActionStatus, L"Start the game to enable unlock actions.");
        SetStatus(L"Waiting for Remake...");
        return;
    }

    if (gPipe == INVALID_HANDLE_VALUE && !Connect()) {
        SetActionButtonsEnabled(false);
        SetWindowTextW(
            gActionStatus, L"BepInEx bridge unavailable.");
        SetStatus(L"Game found; BepInEx bridge offline");
        return;
    }

    if (!SendCurrentState()) {
        Disconnect();
        SetActionButtonsEnabled(false);
        SetWindowTextW(
            gActionStatus, L"Bridge reconnecting...");
        SetStatus(L"Game found; reconnecting bridge...");
        return;
    }

    gLocalStatePending = false;
    SetActionButtonsEnabled(true);
    SetStatus(L"Connected to Remake");
}

HWND AddCheckbox(
    HWND parent,
    const wchar_t* text,
    int x,
    int y,
    int width,
    int id) {
    return CreateWindowExW(
        0,
        L"BUTTON",
        text,
        WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX | WS_TABSTOP,
        x,
        y,
        width,
        28,
        parent,
        reinterpret_cast<HMENU>(static_cast<INT_PTR>(id)),
        GetModuleHandleW(nullptr),
        nullptr);
}

HWND AddRadio(
    HWND parent,
    const wchar_t* text,
    int x,
    int y,
    int width,
    int id,
    bool first) {
    return CreateWindowExW(
        0,
        L"BUTTON",
        text,
        WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_AUTORADIOBUTTON |
            (first ? WS_GROUP : 0),
        x,
        y,
        width,
        24,
        parent,
        reinterpret_cast<HMENU>(static_cast<INT_PTR>(id)),
        GetModuleHandleW(nullptr),
        nullptr);
}

HWND AddButton(
    HWND parent,
    const wchar_t* text,
    int x,
    int y,
    int width,
    int id) {
    return CreateWindowExW(
        0,
        L"BUTTON",
        text,
        WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_PUSHBUTTON,
        x,
        y,
        width,
        32,
        parent,
        reinterpret_cast<HMENU>(static_cast<INT_PTR>(id)),
        GetModuleHandleW(nullptr),
        nullptr);
}

int ActionForControl(int id) {
    switch (id) {
    case UnlockTrainingButton:
        return 7;
    case UnlockTrunkButton:
        return 9;
    case UnlockBossButton:
        return 11;
    case UnlockChaptersButton:
        return 13;
    case UnlockBestiaryButton:
        return 15;
    case UnlockAchievementsButton:
        return 16;
    case UnlockBossStarsButton:
        return 17;
    case UnlockTrainingStarsButton:
        return 18;
    default:
        return 0;
    }
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
            18,
            720,
            24,
            window,
            nullptr,
            GetModuleHandleW(nullptr),
            nullptr);

        gGameplayGroup = CreateWindowExW(
            0,
            L"BUTTON",
            L"Gameplay",
            WS_CHILD | WS_VISIBLE | BS_GROUPBOX,
            20,
            50,
            350,
            260,
            window,
            nullptr,
            GetModuleHandleW(nullptr),
            nullptr);
        gGodMode = AddCheckbox(
            window, L"Infinite Health", 40, 72, 310, GodModeCheckbox);
        gAmmo = AddCheckbox(
            window, L"Infinite Ammo", 40, 104, 310, AmmoCheckbox);
        gContinues = AddCheckbox(
            window, L"Infinite Continues", 40, 136, 310, ContinuesCheckbox);
        gOneShot = AddCheckbox(
            window, L"One Shot Mode", 40, 168, 310, OneShotCheckbox);
        gEasyBoss = AddCheckbox(
            window, L"Easy Boss Mode", 40, 200, 310, EasyBossCheckbox);
        gZeroDamage = AddCheckbox(
            window, L"Zero Damage", 40, 232, 310, ZeroDamageCheckbox);
        gAllWeapons = AddCheckbox(
            window, L"All Weapons Unlocked", 40, 264, 310, AllWeaponsCheckbox);

        gFireGroup = CreateWindowExW(
            0,
            L"BUTTON",
            L"Fire mode",
            WS_CHILD | WS_VISIBLE | BS_GROUPBOX,
            20,
            322,
            350,
            148,
            window,
            nullptr,
            GetModuleHandleW(nullptr),
            nullptr);
        gFireOff = AddRadio(
            window, L"Off", 40, 344, 310, FireOffRadio, true);
        gAutoFire = AddRadio(
            window,
            L"Auto Fire (native max)",
            40,
            370,
            310,
            AutoFireRadio,
            false);
        gRapidFire = AddRadio(
            window, L"Rapid Fire", 40, 396, 310, RapidFireRadio, false);
        gRapidFireRateLabel = CreateWindowExW(
            0,
            L"STATIC",
            L"Shots/sec:",
            WS_CHILD | WS_VISIBLE,
            64,
            427,
            58,
            20,
            window,
            nullptr,
            GetModuleHandleW(nullptr),
            nullptr);
        gRapidFireRateEdit = CreateWindowExW(
            WS_EX_CLIENTEDGE,
            L"EDIT",
            L"",
            WS_CHILD | WS_VISIBLE | WS_TABSTOP | ES_NUMBER | ES_READONLY |
                ES_CENTER,
            126,
            422,
            58,
            24,
            window,
            reinterpret_cast<HMENU>(
                static_cast<INT_PTR>(RapidFireRateEdit)),
            GetModuleHandleW(nullptr),
            nullptr);
        gRapidFireRateSpinner = CreateWindowExW(
            0,
            UPDOWN_CLASSW,
            nullptr,
            WS_CHILD | WS_VISIBLE | UDS_ALIGNRIGHT | UDS_SETBUDDYINT |
                UDS_ARROWKEYS,
            0,
            0,
            0,
            0,
            window,
            reinterpret_cast<HMENU>(
                static_cast<INT_PTR>(RapidFireRateSpinner)),
            GetModuleHandleW(nullptr),
            nullptr);
        SendMessageW(
            gRapidFireRateSpinner,
            UDM_SETBUDDY,
            reinterpret_cast<WPARAM>(gRapidFireRateEdit),
            0);
        SendMessageW(
            gRapidFireRateSpinner,
            UDM_SETRANGE32,
            kMinRapidFireRate,
            kMaxRapidFireRate);
        gApplyingState = true;
        SetRapidFireRate(kDefaultRapidFireRate);
        gApplyingState = false;

        gProgressionGroup = CreateWindowExW(
            0,
            L"BUTTON",
            L"Progression unlocks",
            WS_CHILD | WS_VISIBLE | BS_GROUPBOX,
            390,
            50,
            370,
            420,
            window,
            nullptr,
            GetModuleHandleW(nullptr),
            nullptr);
        HWND warning = CreateWindowExW(
            0,
            L"STATIC",
            L"These actions modify save progress and cannot be undone.",
            WS_CHILD | WS_VISIBLE,
            410,
            74,
            330,
            38,
            window,
            nullptr,
            GetModuleHandleW(nullptr),
            nullptr);
        gActionButtons[0] = AddButton(
            window, L"All Chapters", 410, 120, 155, UnlockChaptersButton);
        gActionButtons[1] = AddButton(
            window, L"Bestiary", 580, 120, 160, UnlockBestiaryButton);
        gActionButtons[2] = AddButton(
            window, L"Training Modes", 410, 164, 155, UnlockTrainingButton);
        gActionButtons[3] = AddButton(
            window,
            L"Training + Stars",
            580,
            164,
            160,
            UnlockTrainingStarsButton);
        gActionButtons[4] = AddButton(
            window, L"Boss Modes", 410, 208, 155, UnlockBossButton);
        gActionButtons[5] = AddButton(
            window,
            L"Boss + Stars",
            580,
            208,
            160,
            UnlockBossStarsButton);
        gActionButtons[6] = AddButton(
            window, L"All Trunk Items", 410, 252, 155, UnlockTrunkButton);
        gActionButtons[7] = AddButton(
            window,
            L"All Achievements",
            580,
            252,
            160,
            UnlockAchievementsButton);
        gActionStatus = CreateWindowExW(
            0,
            L"STATIC",
            L"Start the game to enable unlock actions.",
            WS_CHILD | WS_VISIBLE,
            410,
            306,
            330,
            48,
            window,
            nullptr,
            GetModuleHandleW(nullptr),
            nullptr);

        gPersist = AddCheckbox(
            window,
            L"Remember gameplay cheats across game restarts",
            40,
            486,
            700,
            PersistCheckbox);

        HWND controls[] = {
            gStatus,
            gGameplayGroup,
            gGodMode,
            gAmmo,
            gContinues,
            gOneShot,
            gEasyBoss,
            gZeroDamage,
            gAllWeapons,
            gFireGroup,
            gFireOff,
            gAutoFire,
            gRapidFire,
            gRapidFireRateLabel,
            gRapidFireRateEdit,
            gProgressionGroup,
            warning,
            gActionStatus,
            gPersist,
        };
        for (HWND control : controls) {
            SendMessageW(
                control, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);
        }
        for (HWND button : gActionButtons) {
            SendMessageW(
                button, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);
        }
        SendMessageW(gFireOff, BM_SETCHECK, BST_CHECKED, 0);
        UpdateRapidFireRateEnabled();
        SetActionButtonsEnabled(false);

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
            const int id = LOWORD(wParam);
            const int action = ActionForControl(id);
            if (action != 0) {
                if (action == 16 &&
                    MessageBoxW(
                        window,
                        L"This may permanently unlock platform achievements. Continue?",
                        L"Unlock all achievements",
                        MB_ICONWARNING | MB_YESNO | MB_DEFBUTTON2) != IDYES) {
                    return 0;
                }
                if (SendAction(action)) {
                    SetWindowTextW(
                        gActionStatus,
                        L"Unlock request sent to the game.");
                } else {
                    Disconnect();
                    SetActionButtonsEnabled(false);
                    SetWindowTextW(
                        gActionStatus,
                        L"Bridge disconnected. Reconnect and try again.");
                }
                return 0;
            }

            if (id == FireOffRadio ||
                id == AutoFireRadio ||
                id == RapidFireRadio) {
                UpdateRapidFireRateEnabled();
            }
            if (gPipe == INVALID_HANDLE_VALUE) {
                gLocalStatePending = true;
            }
            Tick();
        } else if (
            LOWORD(wParam) == RapidFireRateEdit &&
            HIWORD(wParam) == EN_CHANGE &&
            !gApplyingState) {
            if (gPipe == INVALID_HANDLE_VALUE) {
                gLocalStatePending = true;
            }
            Tick();
        }
        return 0;
    case WM_DESTROY:
        KillTimer(window, kTimerId);
        if (!IsChecked(gPersist)) {
            SendState(
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                GetRapidFireRate());
        }
        Disconnect();
        PostQuitMessage(0);
        return 0;
    default:
        return DefWindowProcW(window, message, wParam, lParam);
    }
}
bool SelfTest() {
    char command[64]{};
    const int length = FormatStateCommand(
        command,
        sizeof(command),
        true,
        false,
        true,
        true,
        true,
        false,
        true,
        true,
        false,
        true,
        kDefaultRapidFireRate);
    char action[16]{};
    const int actionLength =
        FormatActionCommand(action, sizeof(action), 13);
    bool godMode = false;
    bool ammo = false;
    bool continues = false;
    bool autoFire = false;
    bool persist = false;
    bool rapidFire = false;
    bool oneShot = false;
    bool easyBoss = false;
    bool zeroDamage = false;
    bool allWeapons = false;
    int rapidFireRate = 0;
    return _wcsicmp(kGameExe, L"THE HOUSE OF THE DEAD 2 Remake.exe") == 0 &&
           _wcsicmp(kPipePath, L"\\\\.\\pipe\\Hotd2RemakeTrainer") == 0 &&
           length == 28 &&
           std::strcmp(command, "STATE 1 0 1 1 1 0 1 1 0 1 8\n") == 0 &&
           actionLength == 10 &&
           std::strcmp(action, "ACTION 13\n") == 0 &&
           IsUnlockAction(7) &&
           IsUnlockAction(9) &&
           IsUnlockAction(11) &&
           IsUnlockAction(13) &&
           IsUnlockAction(15) &&
           IsUnlockAction(16) &&
           IsUnlockAction(17) &&
           IsUnlockAction(18) &&
           !IsUnlockAction(8) &&
           !IsUnlockAction(10) &&
           !IsUnlockAction(12) &&
           ParseStateCommand(
               command,
               godMode,
               ammo,
               continues,
               autoFire,
               persist,
               rapidFire,
               oneShot,
               easyBoss,
               zeroDamage,
               allWeapons,
               rapidFireRate) &&
           godMode && !ammo && continues && autoFire && persist &&
           !rapidFire && oneShot && easyBoss && !zeroDamage && allWeapons &&
           rapidFireRate == kDefaultRapidFireRate &&
           !ParseStateCommand(
               "STATE 1 0 1 1 1 1 1 0 0 0 8\n",
               godMode,
               ammo,
               continues,
               autoFire,
               persist,
               rapidFire,
               oneShot,
               easyBoss,
               zeroDamage,
               allWeapons,
               rapidFireRate) &&
           !ParseStateCommand(
               "STATE 1 0 1 0 1 1 1 0 0 0 17\n",
               godMode,
               ammo,
               continues,
               autoFire,
               persist,
               rapidFire,
               oneShot,
               easyBoss,
               zeroDamage,
               allWeapons,
               rapidFireRate);
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

    INITCOMMONCONTROLSEX commonControls{
        sizeof(commonControls),
        ICC_UPDOWN_CLASS,
    };
    if (!InitCommonControlsEx(&commonControls)) {
        return 1;
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
        790,
        570,
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
