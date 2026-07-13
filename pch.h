#ifndef PCH_H
#define PCH_H

#include <windows.h>

#include <algorithm>
#include <array>
#include <atomic>
#include <cstdarg>
#include <cmath>
#include <cstdint>
#include <deque>
#include <cstdio>
#include <cwctype>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <mutex>
#include <map>
#include <string>
#include <unordered_map>
#include <vector>

#define XR_USE_PLATFORM_WIN32
#define XR_USE_GRAPHICS_API_D3D11
#include <d3d11.h>
#include <dxgi.h>
#include <pdh.h>
#include <pdhmsg.h>
#pragma comment(lib, "pdh.lib")
#include <openxr/openxr.h>
#include <openxr/openxr_platform.h>

#include "loader_interfaces.h"

#endif

