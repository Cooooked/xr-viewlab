/*
 * ViewLab Enhancer — OBS Studio video-filter plugin.
 *
 * Image enhancement only: sharpness, saturation, vibrance, contrast, brightness and gamma in
 * one GPU pass. Stabilization deliberately lives in the separate ViewLab Stabilizer plugin.
 * All libobs entry points are resolved at runtime from the host's obs.dll.
 *
 * GPL-2.0-or-later. See LICENSE.
 */
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>
#include "obs_filter_abi.h"

static fn_obs_register_source_s p_obs_register_source_s;
static fn_obs_get_version p_obs_get_version;
static fn_blog p_blog;
static fn_obs_source_process_filter_begin p_process_filter_begin;
static fn_obs_source_process_filter_end p_process_filter_end;
static fn_obs_source_skip_video_filter p_skip_video_filter;
static fn_obs_filter_get_target p_filter_get_target;
static fn_obs_source_get_base_width p_get_base_width;
static fn_obs_source_get_base_height p_get_base_height;
static fn_gs_effect_create p_gs_effect_create;
static fn_gs_effect_destroy p_gs_effect_destroy;
static fn_gs_effect_get_param_by_name p_gs_effect_get_param_by_name;
static fn_gs_effect_set_vec2 p_gs_effect_set_vec2;
static fn_gs_effect_set_float p_gs_effect_set_float;
static fn_obs_properties_create p_obs_properties_create;
static fn_obs_properties_add_float_slider p_obs_properties_add_float_slider;
static fn_obs_property_float_set_suffix p_obs_property_float_set_suffix;
static fn_obs_data_get_double p_obs_data_get_double;
static fn_obs_data_set_default_double p_obs_data_set_default_double;

static FARPROC obs_resolve(const char *name)
{
    HMODULE obs = GetModuleHandleW(L"obs.dll");
    return obs ? GetProcAddress(obs, name) : NULL;
}

#define OBS_RESOLVE(var, name)                          \
    do {                                                \
        *(FARPROC *)&(var) = obs_resolve(name);         \
        if (!(var))                                     \
            return false;                               \
    } while (0)

static bool resolve_libobs(void)
{
    OBS_RESOLVE(p_obs_register_source_s, "obs_register_source_s");
    OBS_RESOLVE(p_obs_get_version, "obs_get_version");
    OBS_RESOLVE(p_blog, "blog");
    OBS_RESOLVE(p_process_filter_begin, "obs_source_process_filter_begin");
    OBS_RESOLVE(p_process_filter_end, "obs_source_process_filter_end");
    OBS_RESOLVE(p_skip_video_filter, "obs_source_skip_video_filter");
    OBS_RESOLVE(p_filter_get_target, "obs_filter_get_target");
    OBS_RESOLVE(p_get_base_width, "obs_source_get_base_width");
    OBS_RESOLVE(p_get_base_height, "obs_source_get_base_height");
    OBS_RESOLVE(p_gs_effect_create, "gs_effect_create");
    OBS_RESOLVE(p_gs_effect_destroy, "gs_effect_destroy");
    OBS_RESOLVE(p_gs_effect_get_param_by_name, "gs_effect_get_param_by_name");
    OBS_RESOLVE(p_gs_effect_set_vec2, "gs_effect_set_vec2");
    OBS_RESOLVE(p_gs_effect_set_float, "gs_effect_set_float");
    OBS_RESOLVE(p_obs_properties_create, "obs_properties_create");
    OBS_RESOLVE(p_obs_properties_add_float_slider, "obs_properties_add_float_slider");
    OBS_RESOLVE(p_obs_property_float_set_suffix, "obs_property_float_set_suffix");
    OBS_RESOLVE(p_obs_data_get_double, "obs_data_get_double");
    OBS_RESOLVE(p_obs_data_set_default_double, "obs_data_set_default_double");
    return true;
}

static const char *ENHANCE_EFFECT =
    "uniform float4x4 ViewProj;\n"
    "uniform texture2d image;\n"
    "uniform float2 texel;\n"
    "uniform float sharpen;\n"
    "uniform float saturation;\n"
    "uniform float vibrance;\n"
    "uniform float contrast;\n"
    "uniform float brightness;\n"
    "uniform float gamma_inv;\n"
    "sampler_state imageSampler { Filter = Linear; AddressU = Clamp; AddressV = Clamp; };\n"
    "struct VertInOut { float4 pos : POSITION; float2 uv : TEXCOORD0; };\n"
    "VertInOut VSDefault(VertInOut v) {\n"
    "    VertInOut o;\n"
    "    o.pos = mul(float4(v.pos.xyz, 1.0), ViewProj);\n"
    "    o.uv = v.uv;\n"
    "    return o;\n"
    "}\n"
    "float4 PSEnhance(VertInOut v) : TARGET {\n"
    "    float2 uv = v.uv;\n"
    "    float3 c = image.Sample(imageSampler, uv).rgb;\n"
    "    if (sharpen > 0.0001) {\n"
    "        float3 blur = image.Sample(imageSampler, uv + float2(texel.x, 0.0)).rgb\n"
    "                    + image.Sample(imageSampler, uv - float2(texel.x, 0.0)).rgb\n"
    "                    + image.Sample(imageSampler, uv + float2(0.0, texel.y)).rgb\n"
    "                    + image.Sample(imageSampler, uv - float2(0.0, texel.y)).rgb;\n"
    "        c = c + sharpen * (c - 0.25 * blur);\n"
    "    }\n"
    "    c *= brightness;\n"
    "    c = (c - 0.5) * contrast + 0.5;\n"
    "    c = pow(saturate(c), gamma_inv);\n"
    "    float luma = dot(c, float3(0.2126, 0.7152, 0.0722));\n"
    "    c = lerp(luma.xxx, c, saturation);\n"
    "    if (abs(vibrance) > 0.0001) {\n"
    "        float mx = max(c.r, max(c.g, c.b));\n"
    "        float mn = min(c.r, min(c.g, c.b));\n"
    "        c = lerp(luma.xxx, c, 1.0 + vibrance * (1.0 - (mx - mn)));\n"
    "    }\n"
    "    return float4(saturate(c), 1.0);\n"
    "}\n"
    "technique Draw { pass { vertex_shader = VSDefault(v); pixel_shader = PSEnhance(v); } }\n";

struct viewlab_enhancer {
    obs_source_t *source;
    double sharpness, saturation, vibrance, contrast, brightness, gamma;
    gs_effect_t *effect;
    gs_eparam_t *p_texel, *p_sharpen, *p_saturation, *p_vibrance;
    gs_eparam_t *p_contrast, *p_brightness, *p_gamma_inv;
};

static double clampd(double v, double lo, double hi)
{
    return v < lo ? lo : (v > hi ? hi : v);
}

static void enhancer_read_settings(struct viewlab_enhancer *ctx, obs_data_t *settings)
{
    ctx->sharpness = clampd(p_obs_data_get_double(settings, "sharpness"), 0.0, 100.0);
    ctx->saturation = clampd(p_obs_data_get_double(settings, "saturation"), 0.0, 200.0);
    ctx->vibrance = clampd(p_obs_data_get_double(settings, "vibrance"), -100.0, 100.0);
    ctx->contrast = clampd(p_obs_data_get_double(settings, "contrast"), 0.0, 200.0);
    ctx->brightness = clampd(p_obs_data_get_double(settings, "brightness"), 0.0, 200.0);
    ctx->gamma = clampd(p_obs_data_get_double(settings, "gamma"), 10.0, 300.0);
}

static bool enhancer_active(const struct viewlab_enhancer *ctx)
{
    return ctx->sharpness > 0.05 || fabs(ctx->saturation - 100.0) > 0.05 ||
           fabs(ctx->vibrance) > 0.05 || fabs(ctx->contrast - 100.0) > 0.05 ||
           fabs(ctx->brightness - 100.0) > 0.05 || fabs(ctx->gamma - 100.0) > 0.05;
}

static const char *enhancer_get_name(void *type_data)
{
    (void)type_data;
    return "ViewLab Enhancer";
}

static void *enhancer_create(obs_data_t *settings, obs_source_t *source)
{
    struct viewlab_enhancer *ctx = calloc(1, sizeof(*ctx));
    if (!ctx)
        return NULL;
    ctx->source = source;
    enhancer_read_settings(ctx, settings);
    p_blog(LOG_INFO, "[viewlab-enhancer] filter created (image enhancement only)");
    return ctx;
}

static void enhancer_destroy(void *data)
{
    struct viewlab_enhancer *ctx = data;
    if (!ctx)
        return;
    if (ctx->effect)
        p_gs_effect_destroy(ctx->effect);
    free(ctx);
}

static void enhancer_update(void *data, obs_data_t *settings)
{
    enhancer_read_settings((struct viewlab_enhancer *)data, settings);
}

static void enhancer_get_defaults(obs_data_t *settings)
{
    p_obs_data_set_default_double(settings, "sharpness", 0.0);
    p_obs_data_set_default_double(settings, "saturation", 100.0);
    p_obs_data_set_default_double(settings, "vibrance", 0.0);
    p_obs_data_set_default_double(settings, "contrast", 100.0);
    p_obs_data_set_default_double(settings, "brightness", 100.0);
    p_obs_data_set_default_double(settings, "gamma", 100.0);
}

static obs_properties_t *enhancer_get_properties(void *data)
{
    (void)data;
    obs_properties_t *props = p_obs_properties_create();
    obs_property_t *p;
    p = p_obs_properties_add_float_slider(props, "sharpness", "Sharpness", 0.0, 100.0, 1.0);
    p_obs_property_float_set_suffix(p, " %");
    p = p_obs_properties_add_float_slider(props, "saturation", "Saturation", 0.0, 200.0, 1.0);
    p_obs_property_float_set_suffix(p, " %");
    p = p_obs_properties_add_float_slider(props, "vibrance",
        "Vibrance (boosts muted colours, protects vivid ones)", -100.0, 100.0, 1.0);
    p_obs_property_float_set_suffix(p, " %");
    p = p_obs_properties_add_float_slider(props, "contrast", "Contrast", 0.0, 200.0, 1.0);
    p_obs_property_float_set_suffix(p, " %");
    p = p_obs_properties_add_float_slider(props, "brightness", "Brightness", 0.0, 200.0, 1.0);
    p_obs_property_float_set_suffix(p, " %");
    p = p_obs_properties_add_float_slider(props, "gamma", "Gamma", 10.0, 300.0, 1.0);
    p_obs_property_float_set_suffix(p, " %");
    return props;
}

static void enhancer_video_render(void *data, gs_effect_t *unused)
{
    (void)unused;
    struct viewlab_enhancer *ctx = data;
    obs_source_t *target = p_filter_get_target(ctx->source);
    if (!target || !enhancer_active(ctx)) {
        p_skip_video_filter(ctx->source);
        return;
    }

    uint32_t width = p_get_base_width(target);
    uint32_t height = p_get_base_height(target);
    if (!ctx->effect) {
        ctx->effect = p_gs_effect_create(ENHANCE_EFFECT, NULL, NULL);
        if (ctx->effect) {
            ctx->p_texel = p_gs_effect_get_param_by_name(ctx->effect, "texel");
            ctx->p_sharpen = p_gs_effect_get_param_by_name(ctx->effect, "sharpen");
            ctx->p_saturation = p_gs_effect_get_param_by_name(ctx->effect, "saturation");
            ctx->p_vibrance = p_gs_effect_get_param_by_name(ctx->effect, "vibrance");
            ctx->p_contrast = p_gs_effect_get_param_by_name(ctx->effect, "contrast");
            ctx->p_brightness = p_gs_effect_get_param_by_name(ctx->effect, "brightness");
            ctx->p_gamma_inv = p_gs_effect_get_param_by_name(ctx->effect, "gamma_inv");
        }
    }
    if (!ctx->effect || !width || !height ||
        !p_process_filter_begin(ctx->source, GS_RGBA, OBS_ALLOW_DIRECT_RENDERING)) {
        p_skip_video_filter(ctx->source);
        return;
    }

    struct vec2 texel = {1.0f / (float)width, 1.0f / (float)height};
    if (ctx->p_texel) p_gs_effect_set_vec2(ctx->p_texel, &texel);
    if (ctx->p_sharpen) p_gs_effect_set_float(ctx->p_sharpen, (float)(ctx->sharpness / 100.0));
    if (ctx->p_saturation) p_gs_effect_set_float(ctx->p_saturation, (float)(ctx->saturation / 100.0));
    if (ctx->p_vibrance) p_gs_effect_set_float(ctx->p_vibrance, (float)(ctx->vibrance / 100.0));
    if (ctx->p_contrast) p_gs_effect_set_float(ctx->p_contrast, (float)(ctx->contrast / 100.0));
    if (ctx->p_brightness) p_gs_effect_set_float(ctx->p_brightness, (float)(ctx->brightness / 100.0));
    if (ctx->p_gamma_inv) p_gs_effect_set_float(ctx->p_gamma_inv, (float)(100.0 / ctx->gamma));
    p_process_filter_end(ctx->source, ctx->effect, width, height);
}

static obs_module_t *g_module;
__declspec(dllexport) void obs_module_set_pointer(obs_module_t *module) { g_module = module; }
__declspec(dllexport) obs_module_t *obs_current_module(void) { return g_module; }

__declspec(dllexport) uint32_t obs_module_ver(void)
{
    fn_obs_get_version get_version = NULL;
    *(FARPROC *)&get_version = obs_resolve("obs_get_version");
    return get_version ? get_version() : 0;
}

__declspec(dllexport) bool obs_module_load(void)
{
    if (!resolve_libobs())
        return false;
    static struct obs_source_info info;
    memset(&info, 0, sizeof(info));
    info.id = "viewlab_enhancer";
    info.type = OBS_SOURCE_TYPE_FILTER;
    info.output_flags = OBS_SOURCE_VIDEO;
    info.get_name = enhancer_get_name;
    info.create = enhancer_create;
    info.destroy = enhancer_destroy;
    info.update = enhancer_update;
    info.get_defaults = enhancer_get_defaults;
    info.get_properties = enhancer_get_properties;
    info.video_render = enhancer_video_render;
    p_obs_register_source_s(&info, sizeof(info));
    p_blog(LOG_INFO,
        "[viewlab-enhancer] module loaded (image enhancement only, id=viewlab_enhancer)");
    return true;
}

__declspec(dllexport) void obs_module_unload(void) {}
__declspec(dllexport) const char *obs_module_name(void) { return "ViewLab Enhancer"; }
__declspec(dllexport) const char *obs_module_description(void)
{
    return "Enhances a mirrored VR view or any OBS source: sharpness, saturation, vibrance, contrast, brightness and gamma. Stabilization is a separate ViewLab Stabilizer filter.";
}
