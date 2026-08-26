#pragma once

#include <cstdint>
#include <filesystem>
#include <string>
#include <vector>

namespace viewlab::sticky_note {

constexpr uint32_t kHdSurfaceSize = 1024;
constexpr uint32_t kHdMaxLines = 5;

struct HdMipLevel {
    uint32_t width = 0;
    uint32_t height = 0;
    std::vector<uint8_t> rgba;
};

struct HdSurface {
    std::vector<HdMipLevel> mips;
};

// Hashes only content that changes the cached surface. Placement, scale and opacity are applied
// by the textured-quad path and deliberately do not force a 1024x1024 rerasterisation.
uint64_t HdContentHash(const std::wstring& text, uint32_t palette);

// Produces straight-alpha RGBA plus an alpha-aware mip chain. The caller uploads the immutable
// levels to D3D11 and applies per-note opacity while drawing the quad.
bool RenderHdSurface(const std::wstring& text, uint32_t palette,
    const std::filesystem::path& fontPath, HdSurface& surface, std::string& error);

} // namespace viewlab::sticky_note
