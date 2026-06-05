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

HWND enabledCheck = nullptr;
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
    const double legacyTotal = ReadScale(L"vertical_tangent", 0.18);
    Button_SetCheck(enabledCheck, ReadBoolSetting(L"enabled", true) ? BST_CHECKED : BST_UNCHECKED);
    SetWindowTextW(topScaleEdit, FormatScale(ReadScale(L"top_tangent", legacyTotal * 0.5)).c_str());
    SetWindowTextW(bottomScaleEdit, FormatScale(ReadScale(L"bottom_tangent", legacyTotal * 0.5)).c_str());
    SetStatus(L"Config: " + ConfigPath().wstring());
}

bool ReadScaleFromControl(HWND control, double& scale) {
    wchar_t text[64]{};
    GetWindowTextW(control, text, static_cast<int>(std::size(text)));

    wchar_t* end = nullptr;
    const double parsed = std::wcstod(text, &end);
    if (end == text || !std::isfinite(parsed)) {
        return false;
    }

    scale = std::clamp(parsed, 0.01, 1.0);
    return true;
}

void SaveSettings() {
    double topTangent = 0.09;
    double bottomTangent = 0.09;
    if (!ReadScaleFromControl(topScaleEdit, topTangent) || !ReadScaleFromControl(bottomScaleEdit, bottomTangent)) {
        MessageBoxW(nullptr, L"Enter screen share values from 0.000 to 1.000.", AppTitle, MB_ICONWARNING);
        return;
    }

    const bool enabled = Button_GetCheck(enabledCheck) == BST_CHECKED;
    const auto config = ConfigPath();

    if (!WritePrivateProfileStringW(L"Settings", L"enabled", enabled ? L"1" : L"0", config.c_str()) ||
        !WritePrivateProfileStringW(L"Settings", L"top_tangent", FormatScale(topTangent).c_str(), config.c_str()) ||
        !WritePrivateProfileStringW(L"Settings", L"bottom_tangent", FormatScale(bottomTangent).c_str(), config.c_str()) ||
        !WritePrivateProfileStringW(L"Settings", L"vertical_tangent", nullptr, config.c_str())) {
        MessageBoxW(nullptr, L"Could not write config. Run as administrator.", AppTitle, MB_ICONERROR);
        return;
    }

    if (!WriteRegistryEnabled(enabled)) {
        MessageBoxW(nullptr, L"Could not update OpenXR layer registry. Run as administrator.", AppTitle, MB_ICONERROR);
        return;
    }

    SetWindowTextW(topScaleEdit, FormatScale(topTangent).c_str());
    SetWindowTextW(bottomScaleEdit, FormatScale(bottomTangent).c_str());
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
        label(L"Set top and bottom screen share. 0.09 + 0.09 = 18%.", 18, 44, 420, 24);

        enabledCheck = CreateWindowExW(0, L"BUTTON", L"Enable OpenXR layer", WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX, 18, 84, 220, 26, hwnd, ControlId(IdEnabled), nullptr, nullptr);
        SendMessageW(enabledCheck, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);

        label(L"Top screen share", 18, 130, 160, 24);
        topScaleEdit = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"", WS_CHILD | WS_VISIBLE | ES_AUTOHSCROLL, 190, 126, 82, 26, hwnd, ControlId(IdTopScale), nullptr, nullptr);
        SendMessageW(topScaleEdit, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);
        label(L"0.000 to 1.000", 288, 130, 130, 24);

        label(L"Bottom screen share", 18, 168, 160, 24);
        bottomScaleEdit = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"", WS_CHILD | WS_VISIBLE | ES_AUTOHSCROLL, 190, 164, 82, 26, hwnd, ControlId(IdBottomScale), nullptr, nullptr);
        SendMessageW(bottomScaleEdit, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);
        label(L"0.000 to 1.000", 288, 168, 130, 24);

        HWND save = CreateWindowExW(0, L"BUTTON", L"Save", WS_CHILD | WS_VISIBLE | BS_DEFPUSHBUTTON, 18, 222, 90, 32, hwnd, ControlId(IdSave), nullptr, nullptr);
        SendMessageW(save, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);

        statusText = CreateWindowExW(0, L"STATIC", L"", WS_CHILD | WS_VISIBLE, 18, 282, 430, 42, hwnd, ControlId(IdStatus), nullptr, nullptr);
        SendMessageW(statusText, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);

        LoadSettings();
        return 0;
    }
    case WM_CTLCOLORSTATIC:
    case WM_CTLCOLORBTN:
        SetBkMode(reinterpret_cast<HDC>(wParam), TRANSPARENT);
        return reinterpret_cast<LRESULT>(windowBrush);
    case WM_COMMAND:
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
        370,
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