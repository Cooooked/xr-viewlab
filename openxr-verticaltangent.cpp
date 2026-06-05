#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <windowsx.h>
#include <commctrl.h>

#include <algorithm>
#include <cmath>
#include <filesystem>
#include <string>
#include <vector>

#pragma comment(lib, "comctl32.lib")

namespace {
constexpr const wchar_t* AppTitle = L"XR ViewLab";
constexpr const wchar_t* ConfigFileName = L"openxr-verticaltangent.ini";
constexpr const wchar_t* ManifestFileName = L"XR_APILAYER_cooooked_verticaltangent.json";
constexpr const wchar_t* AppRegistryRoot = L"Software\\cooooked\\openxr-verticaltangent\\Apps";

constexpr int IdEnabled = 1001;
constexpr int IdTopScale = 1002;
constexpr int IdSave = 1003;
constexpr int IdStatus = 1004;
constexpr int IdBottomScale = 1005;
constexpr int IdSplitMode = 1006;
constexpr int IdTotalScale = 1007;
constexpr int IdAppList = 1008;
constexpr int IdSaveProfile = 1009;
constexpr int IdReloadApps = 1010;
constexpr int IdUseProfile = 1011;

HWND enabledCheck = nullptr;
HWND splitModeCheck = nullptr;
HWND totalScaleEdit = nullptr;
HWND topScaleEdit = nullptr;
HWND bottomScaleEdit = nullptr;
HWND appList = nullptr;
HWND useProfileCheck = nullptr;
HWND statusText = nullptr;
bool loadingApps = false;

struct AppProfile {
    std::wstring key;
    std::wstring displayName;
    std::wstring module;
};

std::vector<AppProfile> appProfiles;

HMENU ControlId(int id) {
    return reinterpret_cast<HMENU>(static_cast<INT_PTR>(id));
}

std::filesystem::path ExeDirectory() {
    wchar_t path[MAX_PATH]{};
    GetModuleFileNameW(nullptr, path, static_cast<DWORD>(std::size(path)));
    return std::filesystem::path(path).parent_path();
}

std::filesystem::path ConfigPath() {
    return ExeDirectory() / ConfigFileName;
}

std::filesystem::path ManifestPath() {
    return ExeDirectory() / ManifestFileName;
}

std::wstring FormatScale(double value) {
    wchar_t buffer[32]{};
    swprintf_s(buffer, L"%.3f", value);
    return buffer;
}

DWORD ShareToMillis(double value) {
    return static_cast<DWORD>(std::lround(std::clamp(value, 0.0, 1.0) * 1000.0));
}

double MillisToShare(DWORD value, double fallback) {
    if (value > 1000) {
        return fallback;
    }

    return static_cast<double>(value) / 1000.0;
}

std::wstring ReadRegistryString(HKEY key, const wchar_t* name, const std::wstring& fallback) {
    DWORD type = 0;
    DWORD bytes = 0;
    if (RegQueryValueExW(key, name, nullptr, &type, nullptr, &bytes) != ERROR_SUCCESS || type != REG_SZ || bytes == 0) {
        return fallback;
    }

    std::wstring value(bytes / sizeof(wchar_t), L'\0');
    if (RegQueryValueExW(key, name, nullptr, nullptr, reinterpret_cast<BYTE*>(value.data()), &bytes) != ERROR_SUCCESS) {
        return fallback;
    }
    if (!value.empty() && value.back() == L'\0') {
        value.pop_back();
    }
    return value.empty() ? fallback : value;
}

DWORD ReadRegistryDword(HKEY key, const wchar_t* name, DWORD fallback) {
    DWORD value = fallback;
    DWORD type = 0;
    DWORD bytes = sizeof(value);
    if (RegQueryValueExW(key, name, nullptr, &type, reinterpret_cast<BYTE*>(&value), &bytes) != ERROR_SUCCESS || type != REG_DWORD) {
        return fallback;
    }
    return value;
}

std::wstring AppRegistryPath(const std::wstring& appKey) {
    return std::wstring(AppRegistryRoot) + L"\\" + appKey;
}

bool ReadBoolSetting(const wchar_t* key, bool fallback) {
    wchar_t buffer[32]{};
    GetPrivateProfileStringW(L"Settings", key, fallback ? L"1" : L"0", buffer, static_cast<DWORD>(std::size(buffer)), ConfigPath().c_str());
    return _wcsicmp(buffer, L"1") == 0 || _wcsicmp(buffer, L"true") == 0 || _wcsicmp(buffer, L"yes") == 0 || _wcsicmp(buffer, L"on") == 0;
}

double ReadScale(const wchar_t* key, double fallback) {
    wchar_t buffer[64]{};
    GetPrivateProfileStringW(L"Settings", key, L"", buffer, static_cast<DWORD>(std::size(buffer)), ConfigPath().c_str());

    wchar_t* end = nullptr;
    const double value = std::wcstod(buffer, &end);
    if (end == buffer || !std::isfinite(value)) {
        return fallback;
    }

    return std::clamp(value, 0.0, 1.0);
}

int SelectedAppIndex();

void UpdateModeControls() {
    const bool splitMode = Button_GetCheck(splitModeCheck) == BST_CHECKED;
    const bool profileControlsEnabled = useProfileCheck == nullptr || Button_GetCheck(useProfileCheck) == BST_CHECKED || SelectedAppIndex() < 0;
    EnableWindow(totalScaleEdit, profileControlsEnabled && !splitMode);
    EnableWindow(topScaleEdit, profileControlsEnabled && splitMode);
    EnableWindow(bottomScaleEdit, profileControlsEnabled && splitMode);
    EnableWindow(splitModeCheck, profileControlsEnabled);
}

int SelectedAppIndex() {
    const int selected = ListView_GetNextItem(appList, -1, LVNI_SELECTED);
    if (selected < 0) {
        return -1;
    }

    LVITEMW item{};
    item.mask = LVIF_PARAM;
    item.iItem = selected;
    if (!ListView_GetItem(appList, &item)) {
        return -1;
    }

    const int profileIndex = static_cast<int>(item.lParam);
    return profileIndex >= 0 && profileIndex < static_cast<int>(appProfiles.size()) ? profileIndex : -1;
}

void SetEditValues(double total, bool splitMode, double top, double bottom) {
    Button_SetCheck(splitModeCheck, splitMode ? BST_CHECKED : BST_UNCHECKED);
    SetWindowTextW(totalScaleEdit, FormatScale(total).c_str());
    SetWindowTextW(topScaleEdit, FormatScale(top).c_str());
    SetWindowTextW(bottomScaleEdit, FormatScale(bottom).c_str());
    UpdateModeControls();
}

bool WriteRegistryEnabled(bool enabled) {
    HKEY key = nullptr;
    const LONG opened = RegCreateKeyExW(
        HKEY_LOCAL_MACHINE,
        L"Software\\Khronos\\OpenXR\\1\\ApiLayers\\Implicit",
        0,
        nullptr,
        REG_OPTION_NON_VOLATILE,
        KEY_SET_VALUE,
        nullptr,
        &key,
        nullptr);
    if (opened != ERROR_SUCCESS) {
        return false;
    }

    const DWORD value = enabled ? 0u : 1u;
    const std::wstring manifest = ManifestPath().wstring();
    const LONG written = RegSetValueExW(key, manifest.c_str(), 0, REG_DWORD, reinterpret_cast<const BYTE*>(&value), sizeof(value));
    RegCloseKey(key);
    return written == ERROR_SUCCESS;
}

void SetStatus(const std::wstring& text) {
    SetWindowTextW(statusText, text.c_str());
}

void LoadSettings() {
    const double total = ReadScale(L"vertical_tangent", 0.40);
    const bool splitMode = ReadBoolSetting(L"split_mode", false);
    Button_SetCheck(enabledCheck, ReadBoolSetting(L"enabled", true) ? BST_CHECKED : BST_UNCHECKED);
    SetEditValues(total, splitMode, ReadScale(L"top_tangent", total * 0.5), ReadScale(L"bottom_tangent", total * 0.5));
    SetStatus(L"Config: " + ConfigPath().wstring());
}

void LoadSelectedAppProfile() {
    const int profileIndex = SelectedAppIndex();
    if (profileIndex < 0) {
        LoadSettings();
        return;
    }

    const AppProfile& profile = appProfiles[profileIndex];
    HKEY key = nullptr;
    if (RegOpenKeyExW(HKEY_CURRENT_USER, AppRegistryPath(profile.key).c_str(), 0, KEY_QUERY_VALUE, &key) != ERROR_SUCCESS) {
        LoadSettings();
        return;
    }

    const bool profileEnabled = ReadRegistryDword(key, L"profile_enabled", 0) != 0;
    const double globalTotal = ReadScale(L"vertical_tangent", 0.40);
    const bool globalSplit = ReadBoolSetting(L"split_mode", false);
    const double globalTop = ReadScale(L"top_tangent", globalTotal * 0.5);
    const double globalBottom = ReadScale(L"bottom_tangent", globalTotal * 0.5);
    const bool splitMode = ReadRegistryDword(key, L"split_mode", globalSplit ? 1u : 0u) != 0;
    const double total = MillisToShare(ReadRegistryDword(key, L"vertical_tangent", ShareToMillis(globalTotal)), globalTotal);
    const double top = MillisToShare(ReadRegistryDword(key, L"top_tangent", ShareToMillis(globalTop)), globalTop);
    const double bottom = MillisToShare(ReadRegistryDword(key, L"bottom_tangent", ShareToMillis(globalBottom)), globalBottom);
    RegCloseKey(key);

    Button_SetCheck(useProfileCheck, profileEnabled ? BST_CHECKED : BST_UNCHECKED);
    SetEditValues(total, splitMode, top, bottom);
    SetStatus(L"Selected app: " + profile.displayName);
}

void LoadAppProfiles() {
    loadingApps = true;
    appProfiles.clear();
    ListView_DeleteAllItems(appList);

    HKEY root = nullptr;
    if (RegOpenKeyExW(HKEY_CURRENT_USER, AppRegistryRoot, 0, KEY_READ, &root) != ERROR_SUCCESS) {
        loadingApps = false;
        return;
    }

    for (DWORD index = 0;; ++index) {
        wchar_t keyName[256]{};
        DWORD keyNameLength = static_cast<DWORD>(std::size(keyName));
        if (RegEnumKeyExW(root, index, keyName, &keyNameLength, nullptr, nullptr, nullptr, nullptr) != ERROR_SUCCESS) {
            break;
        }

        HKEY appKey = nullptr;
        if (RegOpenKeyExW(root, keyName, 0, KEY_READ, &appKey) != ERROR_SUCCESS) {
            continue;
        }

        AppProfile profile;
        profile.key = keyName;
        profile.displayName = ReadRegistryString(appKey, L"display_name", profile.key);
        profile.module = ReadRegistryString(appKey, L"module", L"");
        const bool appEnabled = ReadRegistryDword(appKey, L"app_enabled", 1) != 0;
        RegCloseKey(appKey);

        const int profileIndex = static_cast<int>(appProfiles.size());
        appProfiles.push_back(profile);

        const std::wstring moduleName = profile.module.empty() ? profile.key : std::filesystem::path(profile.module).filename().wstring();
        const std::wstring display = profile.displayName + L" (" + moduleName + L")";

        LVITEMW item{};
        item.mask = LVIF_TEXT | LVIF_PARAM;
        item.iItem = profileIndex;
        item.pszText = const_cast<wchar_t*>(display.c_str());
        item.lParam = profileIndex;
        ListView_InsertItem(appList, &item);
        ListView_SetCheckState(appList, profileIndex, appEnabled);
    }

    RegCloseKey(root);
    loadingApps = false;
}

bool ReadScaleFromControl(HWND control, double& scale, double minimum) {
    wchar_t text[64]{};
    GetWindowTextW(control, text, static_cast<int>(std::size(text)));

    wchar_t* end = nullptr;
    const double parsed = std::wcstod(text, &end);
    if (end == text || !std::isfinite(parsed)) {
        return false;
    }

    scale = std::clamp(parsed, minimum, 1.0);
    return true;
}

void SaveSettings() {
    double totalTangent = 0.40;
    double topTangent = 0.20;
    double bottomTangent = 0.20;
    const bool profileEnabled = Button_GetCheck(useProfileCheck) == BST_CHECKED;
    const bool splitMode = Button_GetCheck(splitModeCheck) == BST_CHECKED;
    if (!ReadScaleFromControl(totalScaleEdit, totalTangent, 0.01) ||
        !ReadScaleFromControl(topScaleEdit, topTangent, 0.0) ||
        !ReadScaleFromControl(bottomScaleEdit, bottomTangent, 0.0)) {
        MessageBoxW(nullptr, L"Enter screen share values from 0.000 to 1.000. Total must be at least 0.010.", AppTitle, MB_ICONWARNING);
        return;
    }

    if (splitMode) {
        totalTangent = std::clamp(topTangent + bottomTangent, 0.01, 1.0);
    } else {
        topTangent = totalTangent * 0.5;
        bottomTangent = totalTangent * 0.5;
    }

    const bool enabled = Button_GetCheck(enabledCheck) == BST_CHECKED;
    const auto config = ConfigPath();

    if (!WritePrivateProfileStringW(L"Settings", L"enabled", enabled ? L"1" : L"0", config.c_str()) ||
        !WritePrivateProfileStringW(L"Settings", L"split_mode", splitMode ? L"1" : L"0", config.c_str()) ||
        !WritePrivateProfileStringW(L"Settings", L"vertical_tangent", FormatScale(totalTangent).c_str(), config.c_str()) ||
        !WritePrivateProfileStringW(L"Settings", L"top_tangent", FormatScale(topTangent).c_str(), config.c_str()) ||
        !WritePrivateProfileStringW(L"Settings", L"bottom_tangent", FormatScale(bottomTangent).c_str(), config.c_str())) {
        MessageBoxW(nullptr, L"Could not write config. Run as administrator.", AppTitle, MB_ICONERROR);
        return;
    }

    if (!WriteRegistryEnabled(enabled)) {
        MessageBoxW(nullptr, L"Could not update OpenXR layer registry. Run as administrator.", AppTitle, MB_ICONERROR);
        return;
    }

    SetWindowTextW(totalScaleEdit, FormatScale(totalTangent).c_str());
    SetWindowTextW(topScaleEdit, FormatScale(topTangent).c_str());
    SetWindowTextW(bottomScaleEdit, FormatScale(bottomTangent).c_str());
    UpdateModeControls();
    SetStatus(enabled ? L"Saved. Restart the OpenXR game." : L"Layer disabled.");
}

void SaveSelectedAppProfile() {
    const int profileIndex = SelectedAppIndex();
    if (profileIndex < 0) {
        MessageBoxW(nullptr, L"Select an application first.", AppTitle, MB_ICONINFORMATION);
        return;
    }

    double totalTangent = 0.40;
    double topTangent = 0.20;
    double bottomTangent = 0.20;
    const bool profileEnabled = Button_GetCheck(useProfileCheck) == BST_CHECKED;
    const bool splitMode = Button_GetCheck(splitModeCheck) == BST_CHECKED;
    if (!ReadScaleFromControl(totalScaleEdit, totalTangent, 0.01) ||
        !ReadScaleFromControl(topScaleEdit, topTangent, 0.0) ||
        !ReadScaleFromControl(bottomScaleEdit, bottomTangent, 0.0)) {
        MessageBoxW(nullptr, L"Enter screen share values from 0.000 to 1.000. Total must be at least 0.010.", AppTitle, MB_ICONWARNING);
        return;
    }

    if (splitMode) {
        totalTangent = std::clamp(topTangent + bottomTangent, 0.01, 1.0);
    } else {
        topTangent = totalTangent * 0.5;
        bottomTangent = totalTangent * 0.5;
    }

    const AppProfile& profile = appProfiles[profileIndex];
    HKEY key = nullptr;
    if (RegCreateKeyExW(HKEY_CURRENT_USER, AppRegistryPath(profile.key).c_str(), 0, nullptr, REG_OPTION_NON_VOLATILE, KEY_SET_VALUE, nullptr, &key, nullptr) != ERROR_SUCCESS) {
        MessageBoxW(nullptr, L"Could not write app profile.", AppTitle, MB_ICONERROR);
        return;
    }

    const DWORD enabled = profileEnabled ? 1u : 0u;
    const DWORD split = splitMode ? 1u : 0u;
    const DWORD total = ShareToMillis(totalTangent);
    const DWORD top = ShareToMillis(topTangent);
    const DWORD bottom = ShareToMillis(bottomTangent);
    RegSetValueExW(key, L"profile_enabled", 0, REG_DWORD, reinterpret_cast<const BYTE*>(&enabled), sizeof(enabled));
    RegSetValueExW(key, L"split_mode", 0, REG_DWORD, reinterpret_cast<const BYTE*>(&split), sizeof(split));
    RegSetValueExW(key, L"vertical_tangent", 0, REG_DWORD, reinterpret_cast<const BYTE*>(&total), sizeof(total));
    RegSetValueExW(key, L"top_tangent", 0, REG_DWORD, reinterpret_cast<const BYTE*>(&top), sizeof(top));
    RegSetValueExW(key, L"bottom_tangent", 0, REG_DWORD, reinterpret_cast<const BYTE*>(&bottom), sizeof(bottom));
    RegCloseKey(key);

    SetEditValues(totalTangent, splitMode, topTangent, bottomTangent);
    SetStatus(profileEnabled ? L"Saved custom profile for " + profile.displayName + L". Restart the OpenXR game." : L"Custom profile disabled for " + profile.displayName + L".");
}

HFONT UiFont() {
    NONCLIENTMETRICSW metrics{sizeof(metrics)};
    SystemParametersInfoW(SPI_GETNONCLIENTMETRICS, sizeof(metrics), &metrics, 0);
    return CreateFontIndirectW(&metrics.lfMessageFont);
}

LRESULT CALLBACK WindowProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam) {
    static HFONT font = nullptr;
    static HBRUSH windowBrush = nullptr;

    switch (message) {
    case WM_CREATE: {
        font = UiFont();
        windowBrush = CreateSolidBrush(GetSysColor(COLOR_WINDOW));

        auto label = [&](const wchar_t* text, int x, int y, int w, int h) {
            HWND control = CreateWindowExW(0, L"STATIC", text, WS_CHILD | WS_VISIBLE, x, y, w, h, hwnd, nullptr, nullptr, nullptr);
            SendMessageW(control, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);
            return control;
        };

        label(L"XR ViewLab", 18, 16, 360, 24);
        label(L"Use total 0.40, or split 0.20 + 0.20 for the same 40%.", 18, 44, 440, 24);

        enabledCheck = CreateWindowExW(0, L"BUTTON", L"Enable OpenXR layer", WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX, 18, 84, 220, 26, hwnd, ControlId(IdEnabled), nullptr, nullptr);
        SendMessageW(enabledCheck, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);

        label(L"Total screen share", 18, 128, 160, 24);
        totalScaleEdit = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"", WS_CHILD | WS_VISIBLE | ES_AUTOHSCROLL, 190, 124, 82, 26, hwnd, ControlId(IdTotalScale), nullptr, nullptr);
        SendMessageW(totalScaleEdit, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);
        label(L"0.40 = 40% centered", 288, 128, 180, 24);

        splitModeCheck = CreateWindowExW(0, L"BUTTON", L"Use separate top and bottom values", WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX, 18, 166, 300, 26, hwnd, ControlId(IdSplitMode), nullptr, nullptr);
        SendMessageW(splitModeCheck, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);

        label(L"Top share", 18, 206, 160, 24);
        topScaleEdit = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"", WS_CHILD | WS_VISIBLE | ES_AUTOHSCROLL, 190, 202, 82, 26, hwnd, ControlId(IdTopScale), nullptr, nullptr);
        SendMessageW(topScaleEdit, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);
        label(L"0.20 top = 20%", 288, 206, 130, 24);

        label(L"Bottom share", 18, 244, 160, 24);
        bottomScaleEdit = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"", WS_CHILD | WS_VISIBLE | ES_AUTOHSCROLL, 190, 240, 82, 26, hwnd, ControlId(IdBottomScale), nullptr, nullptr);
        SendMessageW(bottomScaleEdit, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);
        label(L"0.20 bottom = 20%", 288, 244, 150, 24);

        HWND save = CreateWindowExW(0, L"BUTTON", L"Save global", WS_CHILD | WS_VISIBLE | BS_DEFPUSHBUTTON, 18, 292, 120, 32, hwnd, ControlId(IdSave), nullptr, nullptr);
        SendMessageW(save, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);

        label(L"Enable layer selectively for each application", 18, 344, 300, 24);
        appList = CreateWindowExW(WS_EX_CLIENTEDGE, WC_LISTVIEWW, L"", WS_CHILD | WS_VISIBLE | LVS_REPORT | LVS_SINGLESEL | LVS_NOCOLUMNHEADER, 18, 372, 430, 128, hwnd, ControlId(IdAppList), nullptr, nullptr);
        SendMessageW(appList, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);
        ListView_SetExtendedListViewStyle(appList, LVS_EX_CHECKBOXES | LVS_EX_FULLROWSELECT);
        LVCOLUMNW column{};
        column.mask = LVCF_WIDTH;
        column.cx = 405;
        ListView_InsertColumn(appList, 0, &column);

        HWND saveProfile = CreateWindowExW(0, L"BUTTON", L"Save selected app", WS_CHILD | WS_VISIBLE, 18, 514, 150, 32, hwnd, ControlId(IdSaveProfile), nullptr, nullptr);
        SendMessageW(saveProfile, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);

        HWND reloadApps = CreateWindowExW(0, L"BUTTON", L"Reload app list", WS_CHILD | WS_VISIBLE, 188, 514, 130, 32, hwnd, ControlId(IdReloadApps), nullptr, nullptr);
        SendMessageW(reloadApps, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);

        useProfileCheck = CreateWindowExW(0, L"BUTTON", L"Use custom values for selected app", WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX, 18, 560, 300, 26, hwnd, ControlId(IdUseProfile), nullptr, nullptr);
        SendMessageW(useProfileCheck, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);

        statusText = CreateWindowExW(0, L"STATIC", L"", WS_CHILD | WS_VISIBLE, 18, 604, 450, 42, hwnd, ControlId(IdStatus), nullptr, nullptr);
        SendMessageW(statusText, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);

        LoadSettings();
        LoadAppProfiles();
        return 0;
    }
    case WM_CTLCOLORSTATIC:
    case WM_CTLCOLORBTN:
        SetBkMode(reinterpret_cast<HDC>(wParam), TRANSPARENT);
        return reinterpret_cast<LRESULT>(windowBrush);
    case WM_COMMAND:
        if (LOWORD(wParam) == IdSplitMode) {
            UpdateModeControls();
            return 0;
        }
        if (LOWORD(wParam) == IdUseProfile) {
            UpdateModeControls();
            return 0;
        }
        if (LOWORD(wParam) == IdSave) {
            SaveSettings();
            return 0;
        }
        if (LOWORD(wParam) == IdSaveProfile) {
            SaveSelectedAppProfile();
            return 0;
        }
        if (LOWORD(wParam) == IdReloadApps) {
            LoadAppProfiles();
            SetStatus(L"App list reloaded.");
            return 0;
        }
        break;
    case WM_NOTIFY: {
        const LPNMHDR header = reinterpret_cast<LPNMHDR>(lParam);
        if (header && header->hwndFrom == appList) {
            if (header->code == LVN_ITEMCHANGED) {
                const auto changed = reinterpret_cast<NMLISTVIEW*>(lParam);
                if (!loadingApps && (changed->uChanged & LVIF_STATE) != 0) {
                    const UINT oldCheck = changed->uOldState & INDEXTOSTATEIMAGEMASK(0xF);
                    const UINT newCheck = changed->uNewState & INDEXTOSTATEIMAGEMASK(0xF);
                    if (oldCheck != newCheck && changed->iItem >= 0 && changed->iItem < static_cast<int>(appProfiles.size())) {
                        HKEY key = nullptr;
                        if (RegCreateKeyExW(HKEY_CURRENT_USER, AppRegistryPath(appProfiles[changed->iItem].key).c_str(), 0, nullptr, REG_OPTION_NON_VOLATILE, KEY_SET_VALUE, nullptr, &key, nullptr) == ERROR_SUCCESS) {
                            const DWORD enabled = ListView_GetCheckState(appList, changed->iItem) ? 1u : 0u;
                            RegSetValueExW(key, L"app_enabled", 0, REG_DWORD, reinterpret_cast<const BYTE*>(&enabled), sizeof(enabled));
                            RegCloseKey(key);
                            SetStatus((enabled ? L"Layer enabled for " : L"Layer disabled for ") + appProfiles[changed->iItem].displayName + L". Restart the OpenXR game.");
                        }
                    }
                }
                if ((changed->uChanged & LVIF_STATE) != 0 && (changed->uNewState & LVIS_SELECTED) != 0) {
                    LoadSelectedAppProfile();
                }
            }
        }
        break;
    }
    case WM_DESTROY:
        if (font) {
            DeleteObject(font);
        }
        if (windowBrush) {
            DeleteObject(windowBrush);
        }
        PostQuitMessage(0);
        return 0;
    default:
        break;
    }

    return DefWindowProcW(hwnd, message, wParam, lParam);
}
}

int APIENTRY wWinMain(HINSTANCE instance, HINSTANCE, LPWSTR, int show) {
    INITCOMMONCONTROLSEX controls{sizeof(controls), ICC_LISTVIEW_CLASSES};
    InitCommonControlsEx(&controls);

    WNDCLASSEXW wc{sizeof(wc)};
    wc.lpfnWndProc = WindowProc;
    wc.hInstance = instance;
    wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
    wc.hIcon = LoadIcon(nullptr, IDI_APPLICATION);
    wc.hIconSm = LoadIcon(nullptr, IDI_APPLICATION);
    wc.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);
    wc.lpszClassName = L"OpenXRVerticalTangentWindow";
    RegisterClassExW(&wc);

    HWND hwnd = CreateWindowExW(
        0,
        wc.lpszClassName,
        AppTitle,
        WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX,
        CW_USEDEFAULT,
        CW_USEDEFAULT,
        480,
        650,
        nullptr,
        nullptr,
        instance,
        nullptr);

    ShowWindow(hwnd, show);
    UpdateWindow(hwnd);

    MSG msg{};
    while (GetMessageW(&msg, nullptr, 0, 0) > 0) {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }

    return static_cast<int>(msg.wParam);
}
