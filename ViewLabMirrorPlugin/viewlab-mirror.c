/*
 * ViewLab Mirror Capture — OBS Studio source plugin.
 *
 * Purpose: give ViewLab its own OBS capture route, fully separate from the unrelated
 * third-party "OpenXR Mirror Capture" source. This source has a unique OBS source id
 * ("viewlab_mirror_capture") and display name ("ViewLab Mirror Capture") so both plugins
 * load and appear independently in the Add Source menu. The producer side of the
 * frame-transfer route is the ViewLab OpenXR layer; see viewlab_mirror_contract.h for the
 * versioned shared-surface contract and the per-frame copy pipeline.
 *
 * Reference implementation studied: Jabbah/OpenXR-Layer-OBSMirror (MIT). No source was
 * copied from it; its shared-surface design informed this contract. libobs declarations
 * are transcribed from obs-studio (GPL-2.0-or-later) — this plugin is GPL-2.0-or-later.
 *
 * License: GPL-2.0-or-later (see LICENSE).
 */
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
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
    return true;
}

/* ---- source state --------------------------------------------------------------------- */
struct viewlab_mirror_source {
    obs_source_t *source;
    long long requested_eye_mode; /* user setting; forwarded once the producer supports it */
    HANDLE surface_map;
    const ViewLabMirrorSurface *surface;
    gs_texture_t *textures[3];
    uint64_t opened_handles[3];
    uint32_t width, height;
    float reconnect_countdown;
    bool logged_connected;
};

static void viewlab_mirror_disconnect(struct viewlab_mirror_source *ctx)
{
    p_obs_enter_graphics();
    for (int i = 0; i < 3; ++i) {
        if (ctx->textures[i])
            p_gs_texture_destroy(ctx->textures[i]);
        ctx->textures[i] = NULL;
        ctx->opened_handles[i] = 0;
    }
    p_obs_leave_graphics();
    if (ctx->surface)
        UnmapViewOfFile(ctx->surface);
    if (ctx->surface_map)
        CloseHandle(ctx->surface_map);
    ctx->surface = NULL;
    ctx->surface_map = NULL;
    ctx->width = ctx->height = 0;
    ctx->logged_connected = false;
}

static bool viewlab_mirror_try_connect(struct viewlab_mirror_source *ctx)
{
    if (!ctx->surface) {
        ctx->surface_map = OpenFileMappingW(FILE_MAP_READ, FALSE, VIEWLAB_MIRROR_SURFACE_NAME);
        if (!ctx->surface_map)
            return false;
        ctx->surface = (const ViewLabMirrorSurface *)MapViewOfFile(
            ctx->surface_map, FILE_MAP_READ, 0, 0, sizeof(ViewLabMirrorSurface));
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
    ctx->width = snapshot.width;
    ctx->height = snapshot.height;
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
    return "ViewLab Mirror Capture";
}

static void *viewlab_mirror_create(obs_data_t *settings, obs_source_t *source)
{
    struct viewlab_mirror_source *ctx = calloc(1, sizeof(*ctx));
    if (!ctx)
        return NULL;
    ctx->source = source;
    ctx->requested_eye_mode = p_obs_data_get_int(settings, "eye_mode");
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
}

static void viewlab_mirror_get_defaults(obs_data_t *settings)
{
    p_obs_data_set_default_int(settings, "eye_mode", 0);
}

static obs_properties_t *viewlab_mirror_get_properties(void *data)
{
    (void)data;
    obs_properties_t *props = p_obs_properties_create();
    obs_property_t *eye = p_obs_properties_add_list(props, "eye_mode",
        "Eye (applies when the ViewLab layer publishes the selected mode)",
        OBS_COMBO_TYPE_LIST, OBS_COMBO_FORMAT_INT);
    p_obs_property_list_add_int(eye, "Left eye", 0);
    p_obs_property_list_add_int(eye, "Right eye", 1);
    p_obs_property_list_add_int(eye, "Both eyes (side-by-side)", 2);
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
    ViewLabMirrorSurface snapshot = *ctx->surface;
    /* A stale heartbeat means no VR session is submitting; render nothing (no fake frame). */
    if (snapshot.heartbeatTick == 0 ||
        GetTickCount64() > snapshot.heartbeatTick + 2000)
        return;
    gs_texture_t *tex = ctx->textures[snapshot.displayIndex % 3];
    if (!tex)
        return;
    gs_effect_t *draw_effect = p_obs_get_base_effect(OBS_EFFECT_DEFAULT);
    while (p_gs_effect_loop(draw_effect, "Draw"))
        p_obs_source_draw(tex, 0, 0, ctx->width, ctx->height, false);
}

static uint32_t viewlab_mirror_get_width(void *data)
{
    struct viewlab_mirror_source *ctx = data;
    return ctx->width;
}

static uint32_t viewlab_mirror_get_height(void *data)
{
    struct viewlab_mirror_source *ctx = data;
    return ctx->height;
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
    info.id = "viewlab_mirror_capture";
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
    p_blog(LOG_INFO, "[viewlab-mirror] module loaded (ViewLab Mirror Capture source registered, id=viewlab_mirror_capture)");
    return true;
}

__declspec(dllexport) void obs_module_unload(void)
{
}

__declspec(dllexport) const char *obs_module_name(void)
{
    return "ViewLab Mirror";
}

__declspec(dllexport) const char *obs_module_description(void)
{
    return "Mirrors the ViewLab-composited VR view (game frame plus selected ViewLab overlays) into OBS.";
}
