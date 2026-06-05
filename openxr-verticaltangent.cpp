#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <windowsx.h>

#include <algorithm>
#include <cmath>
#include <filesystem>
#include <string>

namespace {
constexpr const wchar_t* AppTitle = L"OpenXR Vertical Tangent";
constexpr const wchar_t* ConfigFileName = L"openxr-verticaltangent.ini";
constexpr const wchar_t* ManifestFileName = L"XR_APILAYER_cooooked_verticaltangent.json";

constexpr int IdEnabled = 1001;
constexpr int IdTopScale = 1002;
constexpr int IdSave = 1003;
constexpr int IdStatus = 1004;
constexpr int IdBottomScale = 1005;
constexpr int IdSplitMode = 1006;
constexpr int IdTotalScale = 1007;

HWND enabledCheck = nullptr;
HWND splitModeCheck = nullptr;
HWND totalScaleEdit = nullptr;
HWND topScaleEdit = nullptr;
HWND bottomScaleEdit = nullptr;
HWND statusText = nullptr;

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

void UpdateModeControls() {
    const bool splitMode = Button_GetCheck(splitModeCheck) == BST_CHECKED;
    EnableWindow(totalScaleEdit, !splitMode);
    EnableWindow(topScaleEdit, splitMode);
    EnableWindow(bottomScaleEdit, splitMode);
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
    Button_SetCheck(splitModeCheck, splitMode ? BST_CHECKED : BST_UNCHECKED);
    SetWindowTextW(totalScaleEdit, FormatScale(total).c_str());
    SetWindowTextW(topScaleEdit, FormatScale(ReadScale(L"top_tangent", total * 0.5)).c_str());
    SetWindowTextW(bottomScaleEdit, FormatScale(ReadScale(L"bottom_tangent", total * 0.5)).c_str());
    UpdateModeControls();
    SetStatus(L"Config: " + ConfigPath().wstring());
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

        label(L"Vertical FOV tangent override", 18, 16, 360, 24);
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

        HWND save = CreateWindowExW(0, L"BUTTON", L"Save", WS_CHILD | WS_VISIBLE | BS_DEFPUSHBUTTON, 18, 292, 90, 32, hwnd, ControlId(IdSave), nullptr, nullptr);
        SendMessageW(save, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);

        statusText = CreateWindowExW(0, L"STATIC", L"", WS_CHILD | WS_VISIBLE, 18, 340, 450, 42, hwnd, ControlId(IdStatus), nullptr, nullptr);
        SendMessageW(statusText, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);

        LoadSettings();
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
        if (LOWORD(wParam) == IdSave) {
            SaveSettings();
            return 0;
        }
        break;
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
        420,
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
