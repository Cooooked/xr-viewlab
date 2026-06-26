#include "pch_vr.h"
#include "log.h"

namespace {

std::ofstream g_logStream;
std::mutex    g_logMutex;

std::filesystem::path LogPath() {
    wchar_t localAppData[MAX_PATH]{};
    if (GetEnvironmentVariableW(L"LOCALAPPDATA", localAppData, static_cast<DWORD>(std::size(localAppData))) == 0)
        return {};
    return std::filesystem::path(localAppData) / L"XR ViewLab" / L"Logs" / L"ViewLabVR.log";
}

void RotateIfNeeded(const std::filesystem::path& p) {
    std::error_code ec;
    if (!std::filesystem::exists(p, ec) || std::filesystem::file_size(p, ec) < 2 * 1024 * 1024)
        return;
    const auto old = p.parent_path() / L"ViewLabVR.old.log";
    std::filesystem::remove(old, ec);
    std::filesystem::rename(p, old, ec);
}

void OpenIfNeeded() {
    if (g_logStream.is_open()) return;
    const auto p = LogPath();
    if (p.empty()) return;
    std::error_code ec;
    std::filesystem::create_directories(p.parent_path(), ec);
    RotateIfNeeded(p);
    g_logStream.open(p, std::ios_base::app);
}

} // namespace

void VrLog(const char* fmt, ...) {
    char msg[1024]{};
    va_list args;
    va_start(args, fmt);
    _vsnprintf_s(msg, sizeof(msg), _TRUNCATE, fmt, args);
    va_end(args);

    SYSTEMTIME t{};
    GetLocalTime(&t);

    char line[1400]{};
    _snprintf_s(line, sizeof(line), _TRUNCATE,
        "%02u:%02u:%02u.%03u [%lu] | ViewLabVR | %s",
        t.wHour, t.wMinute, t.wSecond, t.wMilliseconds,
        static_cast<unsigned long>(GetCurrentProcessId()), msg);

    OutputDebugStringA(msg);
    std::lock_guard<std::mutex> lk(g_logMutex);
    OpenIfNeeded();
    if (g_logStream.is_open()) {
        g_logStream << line;
        if (line[std::strlen(line) - 1] != '\n') g_logStream << '\n';
        g_logStream.flush();
    }
}
