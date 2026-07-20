/*
 * ViewLab Media Filter — OBS Studio video filter (part of ViewLab Media Capture / VLMC).
 *
 * A GPU effect-based video filter that any OBS source can carry. It performs colour
 * correction (white balance, brightness, contrast, saturation, gamma) and a single
 * spatial sharpen/smooth control, intended to replace a stack of separate colour +
 * denoise filters. True temporal smoothing and stabilization are a later VLMC phase;
 * this filter is deliberately a single-frame GPU pass with no motion model.
 *
 * Like the VLMC capture source, every libobs entry point is resolved at runtime from the
 * obs.dll already loaded in the OBS process, so the module builds with MSVC alone (no OBS
 * SDK, no import library). The effect is compiled from an embedded string at create time,
 * so the plugin ships as a single DLL with no external .effect data file.
 *
 * libobs ABI declarations are transcribed from obs-studio (GPL-2.0-or-later); this module
 * is GPL-2.0-or-later (see LICENSE). No obs-studio implementation code is copied.
 */
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#include <stdlib.h>
#include "obs_abi.h"

/* ---- runtime-resolved libobs ---------------------------------------------------------- */
static fn_obs_register_source_s          p_obs_register_source_s;
static fn_blog                           p_blog;
static fn_obs_enter_graphics             p_obs_enter_graphics;
static fn_obs_leave_graphics             p_obs_leave_graphics;
static fn_gs_effect_create               p_gs_effect_create;
static fn_gs_effect_destroy              p_gs_effect_destroy;
static fn_gs_effect_get_param_by_name    p_gs_effect_get_param_by_name;
static fn_gs_effect_set_float            p_gs_effect_set_float;
static fn_gs_effect_set_vec2             p_gs_effect_set_vec2;
static fn_gs_effect_set_vec3             p_gs_effect_set_vec3;
static fn_obs_source_process_filter_begin p_obs_source_process_filter_begin;
static fn_obs_source_process_filter_end   p_obs_source_process_filter_end;
static fn_obs_source_skip_video_filter    p_obs_source_skip_video_filter;
static fn_obs_filter_get_target          p_obs_filter_get_target;
static fn_obs_source_get_base_width      p_obs_source_get_base_width;
static fn_obs_source_get_base_height     p_obs_source_get_base_height;
static fn_obs_properties_create          p_obs_properties_create;
static fn_obs_properties_add_float_slider p_obs_properties_add_float_slider;
static fn_obs_properties_add_bool        p_obs_properties_add_bool;
static fn_obs_data_get_double            p_obs_data_get_double;
static fn_obs_data_set_default_double    p_obs_data_set_default_double;
static fn_obs_data_get_bool              p_obs_data_get_bool;
static fn_obs_data_set_default_bool      p_obs_data_set_default_bool;
static fn_bfree                          p_bfree;

static FARPROC vlmc_resolve(const char *name)
{
    HMODULE obs = GetModuleHandleW(L"obs.dll");
    return obs ? GetProcAddress(obs, name) : NULL;
}

#define VLMC_RESOLVE(var, name)                     \
    do {                                            \
        *(FARPROC *)&(var) = vlmc_resolve(name);    \
        if (!(var))                                 \
            return false;                           \
    } while (0)

static bool vlmc_filter_resolve_libobs(void)
{
    VLMC_RESOLVE(p_obs_register_source_s, "obs_register_source_s");
    VLMC_RESOLVE(p_blog, "blog");
    VLMC_RESOLVE(p_obs_enter_graphics, "obs_enter_graphics");
    VLMC_RESOLVE(p_obs_leave_graphics, "obs_leave_graphics");
    VLMC_RESOLVE(p_gs_effect_create, "gs_effect_create");
    VLMC_RESOLVE(p_gs_effect_destroy, "gs_effect_destroy");
    VLMC_RESOLVE(p_gs_effect_get_param_by_name, "gs_effect_get_param_by_name");
    VLMC_RESOLVE(p_gs_effect_set_float, "gs_effect_set_float");
    VLMC_RESOLVE(p_gs_effect_set_vec2, "gs_effect_set_vec2");
    VLMC_RESOLVE(p_gs_effect_set_vec3, "gs_effect_set_vec3");
    VLMC_RESOLVE(p_obs_source_process_filter_begin, "obs_source_process_filter_begin");
    VLMC_RESOLVE(p_obs_source_process_filter_end, "obs_source_process_filter_end");
    VLMC_RESOLVE(p_obs_source_skip_video_filter, "obs_source_skip_video_filter");
    VLMC_RESOLVE(p_obs_filter_get_target, "obs_filter_get_target");
    VLMC_RESOLVE(p_obs_source_get_base_width, "obs_source_get_base_width");
    VLMC_RESOLVE(p_obs_source_get_base_height, "obs_source_get_base_height");
    VLMC_RESOLVE(p_obs_properties_create, "obs_properties_create");
    VLMC_RESOLVE(p_obs_properties_add_float_slider, "obs_properties_add_float_slider");
    VLMC_RESOLVE(p_obs_properties_add_bool, "obs_properties_add_bool");
    VLMC_RESOLVE(p_obs_data_get_double, "obs_data_get_double");
    VLMC_RESOLVE(p_obs_data_set_default_double, "obs_data_set_default_double");
    VLMC_RESOLVE(p_obs_data_get_bool, "obs_data_get_bool");
    VLMC_RESOLVE(p_obs_data_set_default_bool, "obs_data_set_default_bool");
    VLMC_RESOLVE(p_bfree, "bfree");
    return true;
}

/* ---- embedded OBS effect -------------------------------------------------------------- */
/* One pass: spatial sharpen/smooth (unsharp mask), then white balance, brightness,
 * contrast, saturation and gamma. `image` and `ViewProj` are bound by libobs during
 * obs_source_process_filter_end; every other uniform is set per frame below. */
static const char *VLMC_FILTER_EFFECT =
"uniform float4x4 ViewProj;\n"
"uniform texture2d image;\n"
"uniform float2 texel;\n"
"uniform float3 temp_gain;\n"
"uniform float brightness;\n"
"uniform float contrast;\n"
"uniform float saturation;\n"
"uniform float gamma;\n"
"uniform float sharpen;\n"
"\n"
"sampler_state vlmcSampler {\n"
"    Filter   = Linear;\n"
"    AddressU = Clamp;\n"
"    AddressV = Clamp;\n"
"};\n"
"\n"
"struct VertData {\n"
"    float4 pos : POSITION;\n"
"    float2 uv  : TEXCOORD0;\n"
"};\n"
"\n"
"VertData VSDefault(VertData v_in) {\n"
"    VertData o;\n"
"    o.pos = mul(float4(v_in.pos.xyz, 1.0), ViewProj);\n"
"    o.uv  = v_in.uv;\n"
"    return o;\n"
"}\n"
"\n"
"float4 PSFilter(VertData v_in) : TARGET {\n"
"    float4 c = image.Sample(vlmcSampler, v_in.uv);\n"
"    float3 neighbours =\n"
"          image.Sample(vlmcSampler, v_in.uv + float2(texel.x, 0.0)).rgb\n"
"        + image.Sample(vlmcSampler, v_in.uv - float2(texel.x, 0.0)).rgb\n"
"        + image.Sample(vlmcSampler, v_in.uv + float2(0.0, texel.y)).rgb\n"
"        + image.Sample(vlmcSampler, v_in.uv - float2(0.0, texel.y)).rgb;\n"
"    float3 blurred = (c.rgb + neighbours) / 5.0;\n"
"    float3 col = c.rgb + sharpen * (c.rgb - blurred);\n"  /* >0 sharpen, <0 smooth */
"    col *= temp_gain;\n"
"    col += brightness;\n"
"    col = (col - 0.5) * contrast + 0.5;\n"
"    float luma = dot(col, float3(0.2126, 0.7152, 0.0722));\n"
"    col = lerp(float3(luma, luma, luma), col, saturation);\n"
"    col = pow(max(col, 0.0), gamma);\n"
"    return float4(saturate(col), c.a);\n"
"}\n"
"\n"
"technique Draw {\n"
"    pass {\n"
"        vertex_shader = VSDefault(v_in);\n"
"        pixel_shader  = PSFilter(v_in);\n"
"    }\n"
"}\n";

/* ---- filter state --------------------------------------------------------------------- */
struct vlmc_filter {
    obs_source_t *source;
    gs_effect_t  *effect;
    gs_eparam_t  *p_texel;
    gs_eparam_t  *p_temp_gain;
    gs_eparam_t  *p_brightness;
    gs_eparam_t  *p_contrast;
    gs_eparam_t  *p_saturation;
    gs_eparam_t  *p_gamma;
    gs_eparam_t  *p_sharpen;
    /* settings */
    bool  enabled;
    float brightness, contrast, saturation, gamma, temperature, sharpen;
};

static void vlmc_filter_load_settings(struct vlmc_filter *ctx, obs_data_t *settings)
{
    ctx->enabled     = p_obs_data_get_bool(settings, "enabled");
    ctx->brightness  = (float)p_obs_data_get_double(settings, "brightness");
    ctx->contrast    = (float)p_obs_data_get_double(settings, "contrast");
    ctx->saturation  = (float)p_obs_data_get_double(settings, "saturation");
    ctx->gamma       = (float)p_obs_data_get_double(settings, "gamma");
    ctx->temperature = (float)p_obs_data_get_double(settings, "temperature");
    ctx->sharpen     = (float)p_obs_data_get_double(settings, "sharpen");
}

/* ---- obs_source_info callbacks -------------------------------------------------------- */
static const char *vlmc_filter_get_name(void *type_data)
{
    (void)type_data;
    return "ViewLab Media Filter";
}

static void vlmc_filter_get_defaults(obs_data_t *settings)
{
    p_obs_data_set_default_bool(settings, "enabled", true);
    p_obs_data_set_default_double(settings, "brightness", 0.0);
    p_obs_data_set_default_double(settings, "contrast", 1.0);
    p_obs_data_set_default_double(settings, "saturation", 1.0);
    p_obs_data_set_default_double(settings, "gamma", 1.0);
    p_obs_data_set_default_double(settings, "temperature", 0.0);
    p_obs_data_set_default_double(settings, "sharpen", 0.0);
}

static void vlmc_filter_update(void *data, obs_data_t *settings)
{
    vlmc_filter_load_settings((struct vlmc_filter *)data, settings);
}

static void *vlmc_filter_create(obs_data_t *settings, obs_source_t *source)
{
    struct vlmc_filter *ctx = calloc(1, sizeof(*ctx));
    if (!ctx)
        return NULL;
    ctx->source = source;

    p_obs_enter_graphics();
    char *error = NULL;
    ctx->effect = p_gs_effect_create(VLMC_FILTER_EFFECT, "viewlab_media_filter.effect", &error);
    if (ctx->effect) {
        ctx->p_texel      = p_gs_effect_get_param_by_name(ctx->effect, "texel");
        ctx->p_temp_gain  = p_gs_effect_get_param_by_name(ctx->effect, "temp_gain");
        ctx->p_brightness = p_gs_effect_get_param_by_name(ctx->effect, "brightness");
        ctx->p_contrast   = p_gs_effect_get_param_by_name(ctx->effect, "contrast");
        ctx->p_saturation = p_gs_effect_get_param_by_name(ctx->effect, "saturation");
        ctx->p_gamma      = p_gs_effect_get_param_by_name(ctx->effect, "gamma");
        ctx->p_sharpen    = p_gs_effect_get_param_by_name(ctx->effect, "sharpen");
    }
    p_obs_leave_graphics();

    if (!ctx->effect) {
        p_blog(LOG_ERROR, "[viewlab-media-filter] effect failed to compile: %s",
               error ? error : "(no message)");
        if (error) p_bfree(error);
        free(ctx);
        return NULL;
    }
    if (error) p_bfree(error);

    vlmc_filter_load_settings(ctx, settings);
    p_blog(LOG_INFO, "[viewlab-media-filter] filter created");
    return ctx;
}

static void vlmc_filter_destroy(void *data)
{
    struct vlmc_filter *ctx = (struct vlmc_filter *)data;
    if (!ctx)
        return;
    if (ctx->effect) {
        p_obs_enter_graphics();
        p_gs_effect_destroy(ctx->effect);
        p_obs_leave_graphics();
    }
    free(ctx);
}

static obs_properties_t *vlmc_filter_get_properties(void *data)
{
    (void)data;
    obs_properties_t *props = p_obs_properties_create();
    p_obs_properties_add_bool(props, "enabled", "Enable ViewLab Media Filter");
    p_obs_properties_add_float_slider(props, "temperature", "White balance (cool → warm)", -1.0, 1.0, 0.01);
    p_obs_properties_add_float_slider(props, "brightness", "Brightness", -0.5, 0.5, 0.01);
    p_obs_properties_add_float_slider(props, "contrast", "Contrast", 0.0, 2.0, 0.01);
    p_obs_properties_add_float_slider(props, "saturation", "Saturation", 0.0, 2.0, 0.01);
    p_obs_properties_add_float_slider(props, "gamma", "Gamma", 0.25, 2.0, 0.01);
    p_obs_properties_add_float_slider(props, "sharpen", "Sharpen (−) smooth / (+) sharpen", -1.0, 2.0, 0.01);
    return props;
}

static void vlmc_filter_video_render(void *data, gs_effect_t *unused)
{
    (void)unused;
    struct vlmc_filter *ctx = (struct vlmc_filter *)data;

    obs_source_t *target = p_obs_filter_get_target(ctx->source);
    uint32_t cx = target ? p_obs_source_get_base_width(target) : 0;
    uint32_t cy = target ? p_obs_source_get_base_height(target) : 0;

    if (!ctx->enabled || !ctx->effect || cx == 0 || cy == 0) {
        p_obs_source_skip_video_filter(ctx->source);
        return;
    }

    if (!p_obs_source_process_filter_begin(ctx->source, GS_RGBA, OBS_NO_DIRECT_RENDERING))
        return;

    struct vec2 texel = { 1.0f / (float)cx, 1.0f / (float)cy };
    /* White balance: warm (t>0) boosts red and trims blue; green anchors so overall
     * exposure stays roughly constant. Neutral at t == 0 (gain 1,1,1). */
    float t = ctx->temperature;
    struct vec3 temp_gain = { 1.0f + 0.30f * t, 1.0f, 1.0f - 0.30f * t, 0.0f };

    if (ctx->p_texel)      p_gs_effect_set_vec2(ctx->p_texel, &texel);
    if (ctx->p_temp_gain)  p_gs_effect_set_vec3(ctx->p_temp_gain, &temp_gain);
    if (ctx->p_brightness) p_gs_effect_set_float(ctx->p_brightness, ctx->brightness);
    if (ctx->p_contrast)   p_gs_effect_set_float(ctx->p_contrast, ctx->contrast);
    if (ctx->p_saturation) p_gs_effect_set_float(ctx->p_saturation, ctx->saturation);
    if (ctx->p_gamma)      p_gs_effect_set_float(ctx->p_gamma, ctx->gamma);
    if (ctx->p_sharpen)    p_gs_effect_set_float(ctx->p_sharpen, ctx->sharpen);

    p_obs_source_process_filter_end(ctx->source, ctx->effect, cx, cy);
}

/* ---- registration (called from obs_module_load in viewlab-mirror.c) ------------------- */
bool viewlab_media_filter_register(void)
{
    if (!vlmc_filter_resolve_libobs())
        return false;
    static struct obs_source_info info;
    memset(&info, 0, sizeof(info));
    info.id            = "viewlab_media_filter";
    info.type          = OBS_SOURCE_TYPE_FILTER;
    info.output_flags  = OBS_SOURCE_VIDEO;
    info.get_name      = vlmc_filter_get_name;
    info.create        = vlmc_filter_create;
    info.destroy       = vlmc_filter_destroy;
    info.update        = vlmc_filter_update;
    info.get_defaults  = vlmc_filter_get_defaults;
    info.get_properties = vlmc_filter_get_properties;
    info.video_render  = vlmc_filter_video_render;
    p_obs_register_source_s(&info, sizeof(info));
    p_blog(LOG_INFO, "[viewlab-media-filter] registered (id=viewlab_media_filter)");
    return true;
}
