/*
 * ViewLab Media Capture (VLMC) — OBS Studio source plugin.
 *
 * Purpose: give ViewLab its own OBS capture route, fully separate from the unrelated
 * third-party "OpenXR Mirror Capture" source. This source has a unique OBS source id
 * ("viewlab_media_capture") and display name ("ViewLab Media Capture") so both plugins
 * load and appear independently in the Add Source menu. The producer side of the
 * frame-transfer route is the ViewLab OpenXR layer; see viewlab_mirror_contract.h for the
 * versioned shared-surface contract and the per-frame copy pipeline.
 *
 * Portions derived from OpenXR-Layer-OBSMirror (Jabbah), MIT licence — see LICENSE: the
 * percentage crop maths and the crop-then-report-cropped-size behaviour are ported from
 * its OBS plugin (OBSPlugin/win-openxr/win-openxr.cpp). libobs declarations are transcribed
 * from obs-studio (GPL-2.0-or-later) — this plugin is GPL-2.0-or-later.
 *
 * License: GPL-2.0-or-later (see LICENSE).
 */
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#include <dxgiformat.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "obs_abi.h"
#include "viewlab_mirror_contract.h"

/* ---- runtime-resolved libobs ---------------------------------------------------------- */
static fn_obs_register_source_s p_obs_register_source_s;
static fn_obs_get_version p_obs_get_version;
static fn_blog p_blog;
static fn_obs_enter_graphics p_obs_enter_graphics;
static fn_obs_leave_graphics p_obs_leave_graphics;
static fn_gs_effect_loop p_gs_effect_loop;
static fn_obs_get_base_effect p_obs_get_base_effect;
static fn_obs_source_draw p_obs_source_draw;
static fn_gs_texture_open_shared p_gs_texture_open_shared;
static fn_gs_texture_destroy p_gs_texture_destroy;
static fn_obs_properties_create p_obs_properties_create;
static fn_obs_properties_add_list p_obs_properties_add_list;
static fn_obs_property_list_add_int p_obs_property_list_add_int;
static fn_obs_data_get_int p_obs_data_get_int;
static fn_obs_data_set_default_int p_obs_data_set_default_int;
static fn_obs_properties_add_bool p_obs_properties_add_bool;
static fn_obs_data_get_bool p_obs_data_get_bool;
static fn_obs_data_set_default_bool p_obs_data_set_default_bool;
static fn_obs_properties_add_float_slider p_obs_properties_add_float_slider;
static fn_obs_data_get_double p_obs_data_get_double;
static fn_obs_data_set_default_double p_obs_data_set_default_double;
static fn_obs_properties_add_button p_obs_properties_add_button;
static fn_gs_effect_create p_gs_effect_create;
static fn_gs_effect_destroy p_gs_effect_destroy;
static fn_gs_effect_get_param_by_name p_gs_effect_get_param_by_name;
static fn_gs_effect_set_texture p_gs_effect_set_texture;
static fn_gs_draw_sprite_subregion p_gs_draw_sprite_subregion;

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
    OBS_RESOLVE(p_obs_enter_graphics, "obs_enter_graphics");
    OBS_RESOLVE(p_obs_leave_graphics, "obs_leave_graphics");
    OBS_RESOLVE(p_gs_effect_loop, "gs_effect_loop");
    OBS_RESOLVE(p_obs_get_base_effect, "obs_get_base_effect");
    OBS_RESOLVE(p_obs_source_draw, "obs_source_draw");
    OBS_RESOLVE(p_gs_texture_open_shared, "gs_texture_open_shared");
    OBS_RESOLVE(p_gs_texture_destroy, "gs_texture_destroy");
    OBS_RESOLVE(p_obs_properties_create, "obs_properties_create");
    OBS_RESOLVE(p_obs_properties_add_list, "obs_properties_add_list");
    OBS_RESOLVE(p_obs_property_list_add_int, "obs_property_list_add_int");
    OBS_RESOLVE(p_obs_data_get_int, "obs_data_get_int");
    OBS_RESOLVE(p_obs_data_set_default_int, "obs_data_set_default_int");
    OBS_RESOLVE(p_obs_properties_add_bool, "obs_properties_add_bool");
    OBS_RESOLVE(p_obs_data_get_bool, "obs_data_get_bool");
    OBS_RESOLVE(p_obs_data_set_default_bool, "obs_data_set_default_bool");
    OBS_RESOLVE(p_obs_properties_add_float_slider, "obs_properties_add_float_slider");
    OBS_RESOLVE(p_obs_data_get_double, "obs_data_get_double");
    OBS_RESOLVE(p_obs_data_set_default_double, "obs_data_set_default_double");
    OBS_RESOLVE(p_obs_properties_add_button, "obs_properties_add_button");
    OBS_RESOLVE(p_gs_effect_create, "gs_effect_create");
    OBS_RESOLVE(p_gs_effect_destroy, "gs_effect_destroy");
    OBS_RESOLVE(p_gs_effect_get_param_by_name, "gs_effect_get_param_by_name");
    OBS_RESOLVE(p_gs_effect_set_texture, "gs_effect_set_texture");
    OBS_RESOLVE(p_gs_draw_sprite_subregion, "gs_draw_sprite_subregion");
    return true;
}

/* A shared _SRGB ring texture is sampled through an sRGB SRV, so D3D decodes it to linear
 * before the shader sees it, and OBS then writes those linear values out as if they were
 * already display-encoded — the capture comes out far too dark. VLMC's ring is a raw copy of
 * an already display-encoded eye texture, so re-encode once here. This is the same net result
 * OXRMC gets by handing OBS a linear-typed view of its own crop texture (win-openxr.cpp,
 * "Using linear here will cause correct sRGB gamma to be applied"); VLMC opens the producer's
 * shared texture directly and cannot retype it, so it corrects in the shader instead.
 *
 * Alpha is forced to 1.0 deliberately: ReShade fullscreen effects may leave alpha undefined or
 * zero in the eye texture, which would otherwise show up as a transparent source in OBS.
 */
static const char *VIEWLAB_SRGB_REENCODE_EFFECT =
    "uniform float4x4 ViewProj;\n"
    "uniform texture2d image;\n"
    "sampler_state def_sampler { Filter = Linear; AddressU = Clamp; AddressV = Clamp; };\n"
    "struct VertInOut { float4 pos : POSITION; float2 uv : TEXCOORD0; };\n"
    "VertInOut VSMain(VertInOut v) { VertInOut o; o.pos = mul(float4(v.pos.xyz, 1.0), ViewProj); o.uv = v.uv; return o; }\n"
    "float encode_srgb(float v) { v = max(v, 0.0); return v <= 0.0031308 ? v * 12.92 : 1.055 * pow(v, 0.416666667) - 0.055; }\n"
    "float4 PSMain(VertInOut v) : TARGET { float4 c = image.Sample(def_sampler, v.uv); c.rgb = float3(encode_srgb(c.r), encode_srgb(c.g), encode_srgb(c.b)); c.a = 1.0; return c; }\n"
    "technique Draw { pass { vertex_shader = VSMain(v); pixel_shader = PSMain(v); } }\n";

static bool viewlab_mirror_format_is_srgb(uint32_t format)
{
    return format == DXGI_FORMAT_R8G8B8A8_UNORM_SRGB ||
           format == DXGI_FORMAT_B8G8R8A8_UNORM_SRGB;
}

/* ---- source state --------------------------------------------------------------------- */
struct viewlab_mirror_source {
    obs_source_t *source;
    long long requested_eye_mode; /* user setting; written into the shared block for the producer */
    HANDLE surface_map;
    ViewLabMirrorSurface *surface; /* mapped read/write so we can publish requestedEyeMode */
    gs_texture_t *textures[3];
    uint64_t opened_handles[3];
    uint32_t width, height;
    float reconnect_countdown;
    bool logged_connected;
    bool show_overlays;                                  /* publish requestedShowOverlays */
    double crop_top, crop_bottom, crop_left, crop_right; /* percentages, as OXRMC uses */
    uint32_t crop_x, crop_y, crop_w, crop_h;             /* pixel rect derived from them */
    gs_effect_t *srgb_effect;
    bool srgb_effect_failed;
    bool logged_srgb_correction;
    ULONGLONG last_reinit_tick;
};

/* Crop maths ported from OXRMC (OBSPlugin/win-openxr/win-openxr.cpp, win_openxrmirror_init):
 * left/top are taken off first, then right/bottom are a percentage of what remains, and each
 * is clamped so at least one pixel always survives. Applied OBS-side only — the headset image
 * is never touched. */
static void viewlab_mirror_recompute_crop(struct viewlab_mirror_source *ctx)
{
    const uint32_t w = ctx->width, h = ctx->height;
    if (w == 0 || h == 0) {
        ctx->crop_x = ctx->crop_y = ctx->crop_w = ctx->crop_h = 0;
        return;
    }
    uint32_t x = (uint32_t)(ctx->crop_left / 100.0 * w);
    if (x > w - 1)
        x = w - 1;
    uint32_t y = (uint32_t)(ctx->crop_top / 100.0 * h);
    if (y > h - 1)
        y = h - 1;
    const uint32_t remaining_w = w - x;
    const uint32_t remaining_h = h - y;
    uint32_t crop_r = (uint32_t)(ctx->crop_right / 100.0 * remaining_w);
    if (crop_r > remaining_w - 1)
        crop_r = remaining_w - 1;
    uint32_t crop_b = (uint32_t)(ctx->crop_bottom / 100.0 * remaining_h);
    if (crop_b > remaining_h - 1)
        crop_b = remaining_h - 1;
    ctx->crop_x = x;
    ctx->crop_y = y;
    ctx->crop_w = remaining_w - crop_r;
    ctx->crop_h = remaining_h - crop_b;
}

static void viewlab_mirror_disconnect(struct viewlab_mirror_source *ctx)
{
    p_obs_enter_graphics();
    for (int i = 0; i < 3; ++i) {
        if (ctx->textures[i])
            p_gs_texture_destroy(ctx->textures[i]);
        ctx->textures[i] = NULL;
        ctx->opened_handles[i] = 0;
    }
    if (ctx->srgb_effect) {
        p_gs_effect_destroy(ctx->srgb_effect);
        ctx->srgb_effect = NULL;
    }
    p_obs_leave_graphics();
    if (ctx->surface)
        UnmapViewOfFile(ctx->surface);
    if (ctx->surface_map)
        CloseHandle(ctx->surface_map);
    ctx->surface = NULL;
    ctx->surface_map = NULL;
    ctx->srgb_effect_failed = false;
    ctx->logged_srgb_correction = false;
    ctx->width = ctx->height = 0;
    ctx->crop_x = ctx->crop_y = ctx->crop_w = ctx->crop_h = 0;
    ctx->logged_connected = false;
}

static bool viewlab_mirror_try_connect(struct viewlab_mirror_source *ctx)
{
    if (!ctx->surface) {
        /* Read/write: the producer owns the block, but the consumer publishes its selected
         * eye mode into it (requestedEyeMode), so we need write access. */
        ctx->surface_map = OpenFileMappingW(FILE_MAP_ALL_ACCESS, FALSE, VIEWLAB_MIRROR_SURFACE_NAME);
        if (!ctx->surface_map)
            return false;
        ctx->surface = (ViewLabMirrorSurface *)MapViewOfFile(
            ctx->surface_map, FILE_MAP_ALL_ACCESS, 0, 0, sizeof(ViewLabMirrorSurface));
        if (!ctx->surface) {
            CloseHandle(ctx->surface_map);
            ctx->surface_map = NULL;
            return false;
        }
    }
    ViewLabMirrorSurface snapshot = *ctx->surface;
    if (snapshot.magic != VIEWLAB_MIRROR_MAGIC || snapshot.version != VIEWLAB_MIRROR_VERSION ||
        snapshot.width == 0 || snapshot.height == 0)
        return false;
    /* Publish the user's selected eye mode for the producer to honour next frame. */
    ctx->surface->requestedEyeMode = (uint32_t)ctx->requested_eye_mode;
    ctx->surface->requestedShowOverlays = ctx->show_overlays ? 1u : 0u;
    ctx->width = snapshot.width;
    ctx->height = snapshot.height;
    viewlab_mirror_recompute_crop(ctx);
    for (int i = 0; i < 3; ++i) {
        if (!snapshot.sharedHandle[i])
            continue;
        if (ctx->textures[i] && ctx->opened_handles[i] == snapshot.sharedHandle[i])
            continue;
        p_obs_enter_graphics();
        if (ctx->textures[i])
            p_gs_texture_destroy(ctx->textures[i]);
        ctx->textures[i] = p_gs_texture_open_shared((uint32_t)snapshot.sharedHandle[i]);
        p_obs_leave_graphics();
        ctx->opened_handles[i] = ctx->textures[i] ? snapshot.sharedHandle[i] : 0;
    }
    if (!ctx->logged_connected && ctx->textures[snapshot.displayIndex % 3]) {
        ctx->logged_connected = true;
        p_blog(LOG_INFO, "[viewlab-mirror] connected to ViewLab surface (%ux%u eyeMode=%u)",
               snapshot.width, snapshot.height, snapshot.eyeMode);
    }
    return true;
}

/* ---- obs_source_info callbacks -------------------------------------------------------- */
static const char *viewlab_mirror_get_name(void *type_data)
{
    (void)type_data;
    return "ViewLab Media Capture";
}

static void *viewlab_mirror_create(obs_data_t *settings, obs_source_t *source)
{
    struct viewlab_mirror_source *ctx = calloc(1, sizeof(*ctx));
    if (!ctx)
        return NULL;
    ctx->source = source;
    ctx->requested_eye_mode = p_obs_data_get_int(settings, "eye_mode");
    ctx->show_overlays = p_obs_data_get_bool(settings, "show_overlays");
    ctx->crop_top = p_obs_data_get_double(settings, "crop_top");
    ctx->crop_bottom = p_obs_data_get_double(settings, "crop_bottom");
    ctx->crop_left = p_obs_data_get_double(settings, "crop_left");
    ctx->crop_right = p_obs_data_get_double(settings, "crop_right");
    p_blog(LOG_INFO, "[viewlab-mirror] source created (waiting for the ViewLab layer to publish frames)");
    return ctx;
}

static void viewlab_mirror_destroy(void *data)
{
    struct viewlab_mirror_source *ctx = data;
    if (!ctx)
        return;
    viewlab_mirror_disconnect(ctx);
    free(ctx);
}

static void viewlab_mirror_update(void *data, obs_data_t *settings)
{
    struct viewlab_mirror_source *ctx = data;
    ctx->requested_eye_mode = p_obs_data_get_int(settings, "eye_mode");
    ctx->show_overlays = p_obs_data_get_bool(settings, "show_overlays");
    ctx->crop_top = p_obs_data_get_double(settings, "crop_top");
    ctx->crop_bottom = p_obs_data_get_double(settings, "crop_bottom");
    ctx->crop_left = p_obs_data_get_double(settings, "crop_left");
    ctx->crop_right = p_obs_data_get_double(settings, "crop_right");
    viewlab_mirror_recompute_crop(ctx);
    /* Propagate immediately when connected so the producer switches eyes and overlay mode
     * without waiting for the next reconnect tick. */
    if (ctx->surface) {
        ctx->surface->requestedEyeMode = (uint32_t)ctx->requested_eye_mode;
        ctx->surface->requestedShowOverlays = ctx->show_overlays ? 1u : 0u;
    }
}

static void viewlab_mirror_get_defaults(obs_data_t *settings)
{
    p_obs_data_set_default_int(settings, "eye_mode", 0);
    p_obs_data_set_default_bool(settings, "show_overlays", true);
    p_obs_data_set_default_double(settings, "crop_top", 0.0);
    p_obs_data_set_default_double(settings, "crop_bottom", 0.0);
    p_obs_data_set_default_double(settings, "crop_left", 0.0);
    p_obs_data_set_default_double(settings, "crop_right", 0.0);
}

/* Drops the shared surface and textures. video_tick reconnects within a second, which
 * re-opens the ring on OBS's current graphics device — the recovery OXRMC gets from its
 * own reinit path. Rate-limited so a double-click cannot thrash the connection. */
static bool viewlab_mirror_reinit_clicked(obs_properties_t *props, obs_property_t *property,
                                          void *data)
{
    (void)props;
    (void)property;
    struct viewlab_mirror_source *ctx = data;
    if (!ctx)
        return false;
    if (GetTickCount64() - ctx->last_reinit_tick < 2000)
        return false;
    ctx->last_reinit_tick = GetTickCount64();
    p_blog(LOG_INFO, "[viewlab-mirror] reinitialize requested from source properties");
    viewlab_mirror_disconnect(ctx);
    ctx->reconnect_countdown = 0.f;
    return false;
}

static obs_properties_t *viewlab_mirror_get_properties(void *data)
{
    obs_properties_t *props = p_obs_properties_create();
    obs_property_t *eye = p_obs_properties_add_list(props, "eye_mode",
        "Eye (applies when the ViewLab layer publishes the selected mode)",
        OBS_COMBO_TYPE_LIST, OBS_COMBO_FORMAT_INT);
    p_obs_property_list_add_int(eye, "Left eye", 0);
    p_obs_property_list_add_int(eye, "Right eye", 1);
    p_obs_properties_add_bool(props, "show_overlays",
        "Display overlay layers (ReShade menu, OpenKneeboard, RaceLab)");
    p_obs_properties_add_float_slider(props, "crop_top",    "Crop Top Percentage",    0.0, 100.0, 0.5);
    p_obs_properties_add_float_slider(props, "crop_bottom", "Crop Bottom Percentage", 0.0, 100.0, 0.5);
    p_obs_properties_add_float_slider(props, "crop_left",   "Crop Left Percentage",   0.0, 100.0, 0.5);
    p_obs_properties_add_float_slider(props, "crop_right",  "Crop Right Percentage",  0.0, 100.0, 0.5);
    p_obs_properties_add_button(props, "reinitialize", "Reinitialize ViewLab Capture",
        viewlab_mirror_reinit_clicked);
    (void)data;
    return props;
}

static void viewlab_mirror_video_tick(void *data, float seconds)
{
    struct viewlab_mirror_source *ctx = data;
    ctx->reconnect_countdown -= seconds;
    if (ctx->reconnect_countdown > 0.f)
        return;
    ctx->reconnect_countdown = 1.f;
    if (!viewlab_mirror_try_connect(ctx) && ctx->surface) {
        /* Producer disappeared or republished an incompatible block. */
        viewlab_mirror_disconnect(ctx);
    }
}

static void viewlab_mirror_video_render(void *data, gs_effect_t *effect)
{
    (void)effect;
    struct viewlab_mirror_source *ctx = data;
    if (!ctx->surface || ctx->width == 0)
        return;
    /* Tell the producer we are actively rendering, so it only captures while a source is live. */
    ctx->surface->consumerHeartbeatTick = (uint32_t)GetTickCount64();
    ViewLabMirrorSurface snapshot = *ctx->surface;
    /* A stale heartbeat means no VR session is submitting; render nothing (no fake frame). */
    if (snapshot.heartbeatTick == 0 ||
        GetTickCount64() > snapshot.heartbeatTick + 2000)
        return;
    gs_texture_t *tex = ctx->textures[snapshot.displayIndex % 3];
    if (!tex)
        return;
    if (ctx->crop_w == 0 || ctx->crop_h == 0)
        viewlab_mirror_recompute_crop(ctx);
    if (ctx->crop_w == 0 || ctx->crop_h == 0)
        return;

    /* The ring is a raw copy of an already display-encoded eye texture. When it is shared in
     * an _SRGB format the sampler decodes it, so re-encode once; otherwise OBS's own path is
     * already correct and the base effect is used unchanged. */
    gs_effect_t *draw_effect = p_obs_get_base_effect(OBS_EFFECT_DEFAULT);
    if (viewlab_mirror_format_is_srgb(snapshot.format)) {
        if (!ctx->srgb_effect && !ctx->srgb_effect_failed) {
            ctx->srgb_effect = p_gs_effect_create(VIEWLAB_SRGB_REENCODE_EFFECT,
                                                  "viewlab-srgb-reencode.effect", NULL);
            if (!ctx->srgb_effect) {
                ctx->srgb_effect_failed = true;
                p_blog(LOG_ERROR, "[viewlab-mirror] failed to build the sRGB capture correction effect;"
                                  " capture will look dark");
            }
        }
        if (ctx->srgb_effect) {
            draw_effect = ctx->srgb_effect;
            if (!ctx->logged_srgb_correction) {
                ctx->logged_srgb_correction = true;
                p_blog(LOG_INFO, "[viewlab-mirror] correcting double sRGB decode (format=%u)",
                       snapshot.format);
            }
        }
    }

    /* Crop is drawn, not copied: obs_source_draw places a whole texture, so the cropped
     * subregion is drawn directly and get_width/get_height report the cropped size. Same
     * visible result as OXRMC's CopySubresourceRegion crop, without a second device. */
    gs_eparam_t *image = p_gs_effect_get_param_by_name(draw_effect, "image");
    if (!image)
        return;
    p_gs_effect_set_texture(image, tex);
    while (p_gs_effect_loop(draw_effect, "Draw"))
        p_gs_draw_sprite_subregion(tex, 0, ctx->crop_x, ctx->crop_y, ctx->crop_w, ctx->crop_h);
}

static uint32_t viewlab_mirror_get_width(void *data)
{
    struct viewlab_mirror_source *ctx = data;
    return ctx->crop_w ? ctx->crop_w : ctx->width;
}

static uint32_t viewlab_mirror_get_height(void *data)
{
    struct viewlab_mirror_source *ctx = data;
    return ctx->crop_h ? ctx->crop_h : ctx->height;
}

/* ---- module entry points (stable OBS module ABI) -------------------------------------- */
static obs_module_t *g_module;

__declspec(dllexport) void obs_module_set_pointer(obs_module_t *module)
{
    g_module = module;
}

__declspec(dllexport) obs_module_t *obs_current_module(void)
{
    return g_module;
}

__declspec(dllexport) uint32_t obs_module_ver(void)
{
    /* Report the host's own libobs version so the loader always accepts the module; the
     * plugin only touches the stable prefix of the source ABI. */
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
    info.id = "viewlab_media_capture";
    info.type = OBS_SOURCE_TYPE_INPUT;
    info.output_flags = OBS_SOURCE_VIDEO | OBS_SOURCE_CUSTOM_DRAW | OBS_SOURCE_DO_NOT_DUPLICATE;
    info.get_name = viewlab_mirror_get_name;
    info.create = viewlab_mirror_create;
    info.destroy = viewlab_mirror_destroy;
    info.update = viewlab_mirror_update;
    info.get_defaults = viewlab_mirror_get_defaults;
    info.get_properties = viewlab_mirror_get_properties;
    info.video_tick = viewlab_mirror_video_tick;
    info.video_render = viewlab_mirror_video_render;
    info.get_width = viewlab_mirror_get_width;
    info.get_height = viewlab_mirror_get_height;
    info.icon_type = OBS_ICON_TYPE_DESKTOP_CAPTURE;
    p_obs_register_source_s(&info, sizeof(info));
    p_blog(LOG_INFO, "[viewlab-mirror] module loaded (ViewLab Media Capture source registered, id=viewlab_media_capture)");
    return true;
}

__declspec(dllexport) void obs_module_unload(void)
{
}

__declspec(dllexport) const char *obs_module_name(void)
{
    return "ViewLab Media Capture";
}

__declspec(dllexport) const char *obs_module_description(void)
{
    return "ViewLab Media Capture: brings the ViewLab-composited VR view (game frame plus selected ViewLab overlays) into OBS, with a companion colour/smoothing filter.";
}
