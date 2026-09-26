// ViewLab colour grade (OpenXR Toolkit post-processing port) — compiled at build time by
// Shaders\Build-Shaders.ps1 into Shaders\ColourGrade*.h, so no shader compiler runs inside the game
// (the old runtime D3DCompile cost a one-off 20-100 ms hitch on the first graded frame).
//
// Colour maths: line-for-line port of OpenXR Toolkit postprocess.hlsl (mainPostProcess / mainPassThrough
// with PASS_THROUGH_USE_GAINS).
//   OpenXR Toolkit — MIT License
//   Copyright (c) 2021-2022 Matthieu Bucchianeri
//   Copyright (c) 2021-2022 Jean-Luc Dupiot - Reality XP
//   Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated
//   documentation files (the "Software"), to deal in the Software without restriction, including without
//   limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the
//   Software, and to permit persons to whom the Software is furnished to do so, subject to the following
//   conditions: The above copyright notice and this permission notice shall be included in all copies or
//   substantial portions of the Software. THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND.
//
// Entry points:
//   VSMain  full-screen triangle
//   PSMath  per-pixel maths (the original path)
//   PSLut   one 3D lookup per pixel into a LUT baked from the same maths (constant cost for any settings)
//   PSBake  writes one slice of that LUT (run only when a setting changes)

cbuffer Grade : register(b0) {
    float4 Params1; float4 Params2; float4 Params3;
    int4 RectOffset;   // xy = eye rect offset, z = 1 for an sRGB swapchain view
    float4 Levels;     // x black, y white, z gamma (display encoding)
    float4 Bake;       // x = LUT slice being baked, y = LUT size
};
Texture2D Source : register(t0);
Texture3D<float4> Lut : register(t1);
SamplerState LutSampler : register(s0);

static const float FLT_EPS = 1.192092896e-07;

float3 SrgbToLinear(float3 c) { float3 lo = c / 12.92; float3 hi = pow((max(c, 0.0) + 0.055) / 1.055, 2.4); return lerp(hi, lo, step(c, 0.04045)); }
float3 LinearToSrgb(float3 c) { float3 lo = c * 12.92; float3 hi = 1.055 * pow(max(c, 0.0), 1.0 / 2.4) - 0.055; return lerp(hi, lo, step(c, 0.0031308)); }
float3 SafePow(float3 v, float3 p) { return pow(max(abs(v), FLT_EPS), p); }

float3 AdjustContrast(float3 color, float scale) {
    float luminance = dot(saturate(color), float3(0.2125, 0.7154, 0.0721));
    float contrast = luminance * luminance * (3.0 - 2.0 * luminance);
    contrast = lerp(luminance, contrast, scale);
    return max(color + contrast - luminance, 0.0);
}
float3 AdjustBrightness(float3 color, float scale) { float e = 1.0 - scale; return SafePow(color, float3(e, e, e)); }
float3 AdjustExposure(float3 color, float scale) { return color * pow(2.0, scale); }
float3 AdjustVibrance(float3 color, float scale) {
    float average = (color.r + color.g + color.b) / 3.0;
    float highest = max(color.r, max(color.g, color.b));
    float amount = (average - highest) * scale;
    return lerp(color, highest.xxx, amount);
}
float3 AdjustSaturation(float3 color, float amount) {
    float luminance = dot(saturate(color), float3(0.2125, 0.7154, 0.0721));
    return luminance + (color - luminance) * (amount + 1.0);
}
float3 AdjustGains(float3 color, float3 gains) { return saturate(color * (gains + 1)); }
float3 AdjustHighlightsShadows(float3 color, float2 amount) {
    float2 inv_hs = 1.0 / (amount + 1.0);
    float luma = dot(saturate(color), float3(0.3, 0.3, 0.3));
    float h = 1.0 - pow(max(abs(1.0 - luma), FLT_EPS), inv_hs.x);
    float s = pow(max(abs(luma), FLT_EPS), inv_hs.y);
    return (color / luma) * (h + s - luma);
}

// The whole grade, in the swapchain view's space (linear for sRGB views, stored values otherwise).
float3 Grade(float3 color) {
    if (any(Params2.rgb)) color = AdjustGains(color, Params2.rgb);
    if (Params2.w > 0.5) {
        if (any(Params1)) {
            color = AdjustContrast(color, Params1.x);
            color = AdjustBrightness(color, Params1.y);
            color = AdjustExposure(color, Params1.z);
            color = AdjustSaturation(color, Params1.w);
        }
        if (any(Params3.z)) color = AdjustVibrance(color, Params3.z);
        if (any(Params3.xy)) color = AdjustHighlightsShadows(color, Params3.xy);
    }
    if (Levels.x > 0.0001 || Levels.y < 0.9999 || abs(Levels.z - 1.0) > 0.0001) {
        float3 e = saturate(color); if (RectOffset.z != 0) e = LinearToSrgb(e);
        e = Levels.x + (Levels.y - Levels.x) * pow(max(e, 0.0), Levels.z);
        color = RectOffset.z != 0 ? SrgbToLinear(saturate(e)) : e;
    }
    return saturate(color);
}

float4 VSMain(uint id : SV_VertexID) : SV_POSITION {
    float2 t = float2((id << 1) & 2, id & 2);
    return float4(t * float2(2.0f, -2.0f) + float2(-1.0f, 1.0f), 0.0f, 1.0f);
}

float4 PSMath(float4 pos : SV_POSITION) : SV_TARGET {
    int2 xy = int2(pos.xy) - RectOffset.xy;
    return float4(Grade(Source.Load(int3(xy, 0)).rgb), 1.0);
}

// The LUT is indexed and stored in display encoding (sRGB-encoded values for sRGB views), which spreads its
// grid evenly over what the eye sees; a linear-indexed LUT would starve the shadows.
float4 PSLut(float4 pos : SV_POSITION) : SV_TARGET {
    int2 xy = int2(pos.xy) - RectOffset.xy;
    float3 c = saturate(Source.Load(int3(xy, 0)).rgb);
    float3 e = RectOffset.z != 0 ? LinearToSrgb(c) : c;
    const float n = Bake.y;
    float3 g = Lut.SampleLevel(LutSampler, e * ((n - 1.0) / n) + 0.5 / n, 0).rgb;
    return float4(RectOffset.z != 0 ? SrgbToLinear(g) : g, 1.0);
}

float4 PSBake(float4 pos : SV_POSITION) : SV_TARGET {
    const float n1 = Bake.y - 1.0;
    float3 e = float3(floor(pos.x), floor(pos.y), Bake.x) / n1;
    float3 c = RectOffset.z != 0 ? SrgbToLinear(e) : e;
    float3 g = Grade(c);
    return float4(RectOffset.z != 0 ? LinearToSrgb(g) : g, 1.0);
}
