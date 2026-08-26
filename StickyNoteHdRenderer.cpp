#include "StickyNoteHdRenderer.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <cstring>
#include <fstream>
#include <limits>
#include <utility>

#define STB_TRUETYPE_IMPLEMENTATION
#define STBTT_STATIC
#include "ReShadePayloadSource/deps/stb/stb_truetype.h"

namespace viewlab::sticky_note {
namespace {

struct Colour { float r, g, b; };
struct Palette { Colour paperTop, paperBottom, adhesive, ink, crease; };

constexpr std::array<Palette, 5> kPalettes{{
    {{.985f,.865f,.355f},{.935f,.750f,.235f},{.70f,.50f,.10f},{.105f,.075f,.035f},{.45f,.30f,.055f}},
    {{.995f,.720f,.755f},{.940f,.555f,.625f},{.68f,.24f,.31f},{.145f,.045f,.065f},{.49f,.17f,.23f}},
    {{.720f,.955f,.765f},{.505f,.830f,.610f},{.23f,.50f,.32f},{.040f,.115f,.070f},{.16f,.39f,.24f}},
    {{.720f,.885f,.985f},{.480f,.725f,.920f},{.20f,.41f,.59f},{.035f,.085f,.135f},{.14f,.32f,.49f}},
    {{.970f,.945f,.855f},{.885f,.835f,.700f},{.53f,.46f,.34f},{.105f,.085f,.055f},{.39f,.32f,.22f}},
}};

float Clamp01(float value) { return std::clamp(value, 0.0f, 1.0f); }
float SmoothStep(float edge0, float edge1, float value) {
    const float t = Clamp01((value - edge0) / (edge1 - edge0));
    return t * t * (3.0f - 2.0f * t);
}
uint8_t Byte(float value) { return static_cast<uint8_t>(std::lround(Clamp01(value) * 255.0f)); }

void BlendPixel(std::vector<uint8_t>& rgba, int width, int x, int y, Colour source, float sourceAlpha) {
    if (x < 0 || y < 0 || x >= width || y >= width || sourceAlpha <= 0.0f) return;
    const size_t offset = (static_cast<size_t>(y) * width + x) * 4;
    const float da = rgba[offset + 3] / 255.0f;
    const float sa = Clamp01(sourceAlpha);
    const float oa = sa + da * (1.0f - sa);
    if (oa <= 0.0f) return;
    const float dr = rgba[offset + 0] / 255.0f;
    const float dg = rgba[offset + 1] / 255.0f;
    const float db = rgba[offset + 2] / 255.0f;
    rgba[offset + 0] = Byte((source.r * sa + dr * da * (1.0f - sa)) / oa);
    rgba[offset + 1] = Byte((source.g * sa + dg * da * (1.0f - sa)) / oa);
    rgba[offset + 2] = Byte((source.b * sa + db * da * (1.0f - sa)) / oa);
    rgba[offset + 3] = Byte(oa);
}

uint32_t NoiseHash(uint32_t x, uint32_t y) {
    uint32_t value = x * 0x1f123bb5u ^ y * 0x5f356495u ^ 0x9e3779b9u;
    value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15;
    value *= 0x846ca68bu; value ^= value >> 16; return value;
}

float LeftEdge(float y) { return 47.0f + std::sin(y * .018f) * 1.15f + std::sin(y * .0061f + .8f) * .8f; }
float RightEdge(float y) { return 977.0f + std::sin(y * .014f + 1.7f) * 1.1f + std::sin(y * .0049f) * .7f; }
float TopEdge(float x) { return 48.0f + std::sin(x * .015f + .3f) * 1.0f + std::sin(x * .0057f) * .75f; }
float BottomEdge(float x) { return 976.0f + std::sin(x * .017f + 2.0f) * 1.15f + std::sin(x * .0051f) * .8f; }

float PaperSignedDistance(float x, float y, float offsetX = 0.0f, float offsetY = 0.0f) {
    const float localX = x - offsetX, localY = y - offsetY;
    const float left = LeftEdge(localY), right = RightEdge(localY);
    const float top = TopEdge(localX), bottom = BottomEdge(localX);
    const float inside = std::min({localX - left, right - localX, localY - top, bottom - localY});
    if (inside >= 0.0f) return inside;
    const float dx = localX < left ? left - localX : localX > right ? localX - right : 0.0f;
    const float dy = localY < top ? top - localY : localY > bottom ? localY - bottom : 0.0f;
    return -std::sqrt(dx * dx + dy * dy);
}

std::vector<uint32_t> Codepoints(const std::wstring& text) {
    std::vector<uint32_t> out; out.reserve(text.size());
    bool pendingSpace = false;
    for (size_t i = 0; i < text.size() && out.size() < 120; ++i) {
        uint32_t cp = static_cast<uint16_t>(text[i]);
        if (cp >= 0xD800 && cp <= 0xDBFF && i + 1 < text.size()) {
            const uint32_t low = static_cast<uint16_t>(text[i + 1]);
            if (low >= 0xDC00 && low <= 0xDFFF) { cp = 0x10000 + ((cp - 0xD800) << 10) + low - 0xDC00; ++i; }
        }
        if (cp == '\r' || cp == '\n' || cp == '\t' || cp == ' ') { if (!out.empty()) pendingSpace = true; continue; }
        if (pendingSpace) { out.push_back(' '); pendingSpace = false; }
        out.push_back(cp);
    }
    return out;
}

float GlyphAdvance(const stbtt_fontinfo& font, uint32_t cp, uint32_t next, float scale) {
    int advance = 0, bearing = 0; stbtt_GetCodepointHMetrics(&font, static_cast<int>(cp), &advance, &bearing);
    const int kern = next ? stbtt_GetCodepointKernAdvance(&font, static_cast<int>(cp), static_cast<int>(next)) : 0;
    return (advance + kern) * scale;
}

float TextWidth(const stbtt_fontinfo& font, const std::vector<uint32_t>& text, float scale) {
    float width = 0.0f;
    for (size_t i = 0; i < text.size(); ++i) width += GlyphAdvance(font, text[i], i + 1 < text.size() ? text[i + 1] : 0, scale);
    return width;
}

std::vector<std::vector<uint32_t>> Wrap(const stbtt_fontinfo& font, const std::vector<uint32_t>& text,
    float scale, float maxWidth) {
    std::vector<std::vector<uint32_t>> lines;
    std::vector<uint32_t> line, word;
    auto pushWord = [&] {
        if (word.empty()) return;
        std::vector<uint32_t> candidate = line;
        if (!candidate.empty()) candidate.push_back(' ');
        candidate.insert(candidate.end(), word.begin(), word.end());
        if (!line.empty() && TextWidth(font, candidate, scale) > maxWidth) { lines.push_back(line); line.clear(); }
        if (TextWidth(font, word, scale) <= maxWidth) {
            if (!line.empty()) line.push_back(' ');
            line.insert(line.end(), word.begin(), word.end());
        } else {
            for (uint32_t cp : word) {
                std::vector<uint32_t> next = line; next.push_back(cp);
                if (!line.empty() && TextWidth(font, next, scale) > maxWidth) { lines.push_back(line); line.clear(); }
                line.push_back(cp);
            }
        }
        word.clear();
    };
    for (uint32_t cp : text) { if (cp == ' ') pushWord(); else word.push_back(cp); }
    pushWord(); if (!line.empty()) lines.push_back(line); return lines;
}

void DrawText(std::vector<uint8_t>& rgba, const stbtt_fontinfo& font, const Palette& palette,
    const std::vector<std::vector<uint32_t>>& lines, float pixelHeight) {
    const float scale = stbtt_ScaleForPixelHeight(&font, pixelHeight);
    int ascent = 0, descent = 0, lineGap = 0; stbtt_GetFontVMetrics(&font, &ascent, &descent, &lineGap);
    const float lineHeight = (ascent - descent + lineGap) * scale * 1.04f;
    float baseline = 178.0f + ascent * scale;
    for (const auto& line : lines) {
        float penX = 142.0f;
        for (size_t i = 0; i < line.size(); ++i) {
            const uint32_t cp = line[i];
            int x0 = 0, y0 = 0, x1 = 0, y1 = 0;
            stbtt_GetCodepointBitmapBox(&font, static_cast<int>(cp), scale, scale, &x0, &y0, &x1, &y1);
            const int gw = x1 - x0, gh = y1 - y0;
            if (gw > 0 && gh > 0) {
                std::vector<uint8_t> bitmap(static_cast<size_t>(gw) * gh);
                stbtt_MakeCodepointBitmap(&font, bitmap.data(), gw, gh, gw, scale, scale, static_cast<int>(cp));
                const int dstX = static_cast<int>(std::floor(penX + x0 + .5f));
                const int dstY = static_cast<int>(std::floor(baseline + y0 + .5f));
                for (int gy = 0; gy < gh; ++gy) for (int gx = 0; gx < gw; ++gx) {
                    const float alpha = bitmap[static_cast<size_t>(gy) * gw + gx] / 255.0f * .95f;
                    BlendPixel(rgba, static_cast<int>(kHdSurfaceSize), dstX + gx, dstY + gy, palette.ink, alpha);
                }
            }
            penX += GlyphAdvance(font, cp, i + 1 < line.size() ? line[i + 1] : 0, scale);
        }
        baseline += lineHeight;
    }
}

HdMipLevel Downsample(const HdMipLevel& source) {
    HdMipLevel out; out.width = std::max(1u, source.width / 2); out.height = std::max(1u, source.height / 2);
    out.rgba.resize(static_cast<size_t>(out.width) * out.height * 4);
    for (uint32_t y = 0; y < out.height; ++y) for (uint32_t x = 0; x < out.width; ++x) {
        float alpha = 0.0f, pr = 0.0f, pg = 0.0f, pb = 0.0f;
        for (uint32_t oy = 0; oy < 2; ++oy) for (uint32_t ox = 0; ox < 2; ++ox) {
            const uint32_t sx = std::min(source.width - 1, x * 2 + ox), sy = std::min(source.height - 1, y * 2 + oy);
            const size_t si = (static_cast<size_t>(sy) * source.width + sx) * 4;
            const float a = source.rgba[si + 3] / 255.0f; alpha += a;
            pr += source.rgba[si + 0] / 255.0f * a; pg += source.rgba[si + 1] / 255.0f * a; pb += source.rgba[si + 2] / 255.0f * a;
        }
        alpha *= .25f; pr *= .25f; pg *= .25f; pb *= .25f;
        const size_t di = (static_cast<size_t>(y) * out.width + x) * 4;
        out.rgba[di + 0] = alpha > 0.0001f ? Byte(pr / alpha) : 0;
        out.rgba[di + 1] = alpha > 0.0001f ? Byte(pg / alpha) : 0;
        out.rgba[di + 2] = alpha > 0.0001f ? Byte(pb / alpha) : 0;
        out.rgba[di + 3] = Byte(alpha);
    }
    return out;
}

} // namespace

uint64_t HdContentHash(const std::wstring& text, uint32_t palette) {
    uint64_t hash = 1469598103934665603ull;
    auto add = [&](uint8_t byte) { hash ^= byte; hash *= 1099511628211ull; };
    for (wchar_t ch : text) { const uint16_t u = static_cast<uint16_t>(ch); add(static_cast<uint8_t>(u)); add(static_cast<uint8_t>(u >> 8)); }
    add(static_cast<uint8_t>(palette)); return hash ? hash : 1;
}

bool RenderHdSurface(const std::wstring& text, uint32_t paletteIndex,
    const std::filesystem::path& fontPath, HdSurface& surface, std::string& error) {
    surface.mips.clear(); error.clear();
    std::ifstream stream(fontPath, std::ios::binary);
    if (!stream) { error = "handwriting font is missing"; return false; }
    stream.seekg(0, std::ios::end); const auto length = stream.tellg(); stream.seekg(0, std::ios::beg);
    if (length <= 0 || length > 4 * 1024 * 1024) { error = "handwriting font size is invalid"; return false; }
    std::vector<uint8_t> fontBytes(static_cast<size_t>(length));
    if (!stream.read(reinterpret_cast<char*>(fontBytes.data()), length)) { error = "handwriting font could not be read"; return false; }
    stbtt_fontinfo font{}; const int offset = stbtt_GetFontOffsetForIndex(fontBytes.data(), 0);
    if (offset < 0 || !stbtt_InitFont(&font, fontBytes.data(), offset)) { error = "handwriting font is invalid"; return false; }

    const Palette& palette = kPalettes[std::min<size_t>(paletteIndex, kPalettes.size() - 1)];
    HdMipLevel base; base.width = base.height = kHdSurfaceSize;
    base.rgba.assign(static_cast<size_t>(kHdSurfaceSize) * kHdSurfaceSize * 4, 0);

    for (int y = 0; y < static_cast<int>(kHdSurfaceSize); ++y) for (int x = 0; x < static_cast<int>(kHdSurfaceSize); ++x) {
        const float shadowDistance = PaperSignedDistance(x + .5f, y + .5f, 14.0f, 18.0f);
        const float shadowAlpha = .24f * std::exp(-std::pow(std::max(0.0f, -shadowDistance) / 22.0f, 2.0f)) * SmoothStep(-42.0f, 1.0f, shadowDistance);
        BlendPixel(base.rgba, static_cast<int>(kHdSurfaceSize), x, y, {0.0f, 0.0f, 0.0f}, shadowAlpha);

        const float distance = PaperSignedDistance(x + .5f, y + .5f);
        const float paperAlpha = SmoothStep(-1.25f, 1.25f, distance);
        if (paperAlpha <= 0.0f) continue;
        const float vertical = Clamp01((y - 48.0f) / 928.0f);
        const float grain = (static_cast<int>(NoiseHash(x, y) & 255u) - 127.5f) / 127.5f * .012f;
        Colour colour{
            palette.paperTop.r + (palette.paperBottom.r - palette.paperTop.r) * vertical + grain,
            palette.paperTop.g + (palette.paperBottom.g - palette.paperTop.g) * vertical + grain,
            palette.paperTop.b + (palette.paperBottom.b - palette.paperTop.b) * vertical + grain};
        BlendPixel(base.rgba, static_cast<int>(kHdSurfaceSize), x, y, colour, paperAlpha);

        const float top = TopEdge(static_cast<float>(x));
        if (y >= top && y < top + 58.0f) {
            const float band = (1.0f - SmoothStep(top + 10.0f, top + 58.0f, static_cast<float>(y))) * .11f * paperAlpha;
            BlendPixel(base.rgba, static_cast<int>(kHdSurfaceSize), x, y, palette.adhesive, band);
        }

        const float right = RightEdge(static_cast<float>(y)); constexpr float fold = 128.0f;
        const float u = (x - (right - fold)) / fold, v = (y - top) / fold;
        if (u >= 0.0f && u <= 1.0f && v >= 0.0f && v <= 1.0f && u >= v) {
            const float edge = SmoothStep(-.012f, .012f, u - v);
            BlendPixel(base.rgba, static_cast<int>(kHdSurfaceSize), x, y, {1.0f,.955f,.72f}, .94f * edge * paperAlpha);
            if (u - v < .028f) BlendPixel(base.rgba, static_cast<int>(kHdSurfaceSize), x, y, palette.crease, .48f * paperAlpha);
        }
    }

    const auto codepoints = Codepoints(text);
    float chosenHeight = 108.0f; std::vector<std::vector<uint32_t>> lines;
    for (; chosenHeight >= 62.0f; chosenHeight -= 2.0f) {
        const float scale = stbtt_ScaleForPixelHeight(&font, chosenHeight);
        lines = Wrap(font, codepoints, scale, 742.0f);
        int ascent = 0, descent = 0, gap = 0; stbtt_GetFontVMetrics(&font, &ascent, &descent, &gap);
        const float totalHeight = lines.size() * (ascent - descent + gap) * scale * 1.04f;
        if (lines.size() <= kHdMaxLines && totalHeight <= 560.0f) break;
    }
    if (lines.size() > kHdMaxLines) lines.resize(kHdMaxLines);
    DrawText(base.rgba, font, palette, lines, chosenHeight);

    surface.mips.push_back(std::move(base));
    while (surface.mips.back().width > 1 || surface.mips.back().height > 1)
        surface.mips.push_back(Downsample(surface.mips.back()));
    return true;
}

} // namespace viewlab::sticky_note
