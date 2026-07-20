// Minimal re-declaration of the libobs C ABI used by the ViewLab Stabilizer filter plugin.
//
// Provenance: declarations transcribed from obs-studio's libobs headers
// (https://github.com/obsproject/obs-studio, GPL-2.0-or-later); the obs_source_info layout
// is copied field-for-field from libobs/obs-source.h and the graphics-subsystem prototypes
// from libobs/graphics/graphics.h and libobs/obs.h. This plugin is therefore distributed
// under GPL-2.0-or-later (see LICENSE in this directory). No obs-studio implementation code
// is copied.
//
// Every libobs function is resolved at runtime from the obs.dll module already loaded in the
// OBS process (see OBS_RESOLVE in viewlab-stabilizer.c), so the plugin builds with MSVC alone
// — no OBS SDK checkout, no import library, no version-pinned link dependency.
// obs_register_source_s receives sizeof(struct obs_source_info) and libobs copies
// min(size, its own size), which keeps the prefix-compatible layout safe across OBS releases.
#pragma once
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef struct obs_data obs_data_t;
typedef struct obs_source obs_source_t;
typedef struct obs_properties obs_properties_t;
typedef struct obs_property obs_property_t;
typedef struct gs_effect gs_effect_t;
typedef struct gs_effect_param gs_eparam_t;
typedef struct gs_texture gs_texture_t;
typedef struct gs_texrender gs_texrender_t;
typedef struct gs_stage_surface gs_stagesurf_t;
typedef struct obs_module obs_module_t;

/* obs_source_type: INPUT=0, FILTER=1, TRANSITION=2, SCENE=3 (obs-source.h). */
enum obs_source_type { OBS_SOURCE_TYPE_INPUT = 0, OBS_SOURCE_TYPE_FILTER = 1 };
enum obs_allow_direct_render { OBS_NO_DIRECT_RENDERING = 0, OBS_ALLOW_DIRECT_RENDERING = 1 };

/* gs_color_format (graphics.h): the leading enumerators must match libobs exactly because we
 * pass them by value. GS_RGBA is the 4th enumerator (index 3). */
enum gs_color_format {
    GS_UNKNOWN = 0,
    GS_A8 = 1,
    GS_R8 = 2,
    GS_RGBA = 3,
    GS_BGRX = 4,
    GS_BGRA = 5,
};
enum gs_zstencil_format { GS_ZS_NONE = 0 };

#define GS_CLEAR_COLOR (1 << 0)

#define OBS_SOURCE_VIDEO (1 << 0)
#define OBS_SOURCE_CUSTOM_DRAW (1 << 3)

#define OBS_ICON_TYPE_UNKNOWN 0

#define LOG_ERROR 100
#define LOG_WARNING 200
#define LOG_INFO 300

struct vec2 { float x, y; };
struct vec4 { float x, y, z, w; };

/* Field-for-field copy of libobs/obs-source.h struct obs_source_info. Only the members this
 * plugin populates are used; the rest stay null and libobs treats them as absent callbacks. */
struct obs_source_info {
    const char *id;
    enum obs_source_type type;
    uint32_t output_flags;
    const char *(*get_name)(void *type_data);
    void *(*create)(obs_data_t *settings, obs_source_t *source);
    void (*destroy)(void *data);
    uint32_t (*get_width)(void *data);
    uint32_t (*get_height)(void *data);
    void (*get_defaults)(obs_data_t *settings);
    obs_properties_t *(*get_properties)(void *data);
    void (*update)(void *data, obs_data_t *settings);
    void (*activate)(void *data);
    void (*deactivate)(void *data);
    void (*show)(void *data);
    void (*hide)(void *data);
    void (*video_tick)(void *data, float seconds);
    void (*video_render)(void *data, gs_effect_t *effect);
    void *filter_video;
    void *filter_audio;
    void *enum_active_sources;
    void *save;
    void *load;
    void *mouse_click;
    void *mouse_move;
    void *mouse_wheel;
    void *focus;
    void *key_click;
    void *filter_remove;
    void *type_data;
    void (*free_type_data)(void *type_data);
    void *audio_render;
    void *enum_all_sources;
    void *transition_start;
    void *transition_stop;
    void *get_defaults2;
    void *get_properties2;
    void *audio_mix;
    int icon_type;
    void *media_play_pause;
    void *media_restart;
    void *media_stop;
    void *media_next;
    void *media_previous;
    void *media_get_duration;
    void *media_get_time;
    void *media_set_time;
    void *media_get_state;
    void *missing_files;
    void *video_get_color_space;
    void *filter_add;
    void *get_dark_icon;
    void *get_light_icon;
};

/* Runtime-resolved libobs entry points (names match obs.dll exports). */
typedef void (*fn_obs_register_source_s)(const struct obs_source_info *info, size_t size);
typedef uint32_t (*fn_obs_get_version)(void);
typedef void (*fn_blog)(int log_level, const char *format, ...);

/* graphics context */
typedef void (*fn_obs_enter_graphics)(void);
typedef void (*fn_obs_leave_graphics)(void);

/* filter plumbing */
typedef bool (*fn_obs_source_process_filter_begin)(obs_source_t *filter, enum gs_color_format format,
    enum obs_allow_direct_render allow_direct);
typedef void (*fn_obs_source_process_filter_end)(obs_source_t *filter, gs_effect_t *effect,
    uint32_t width, uint32_t height);
typedef void (*fn_obs_source_skip_video_filter)(obs_source_t *filter);
typedef obs_source_t *(*fn_obs_filter_get_target)(const obs_source_t *filter);
typedef uint32_t (*fn_obs_source_get_base_width)(obs_source_t *source);
typedef uint32_t (*fn_obs_source_get_base_height)(obs_source_t *source);
typedef void (*fn_obs_source_video_render)(obs_source_t *source);

/* effects / params */
typedef gs_effect_t *(*fn_gs_effect_create)(const char *effect_string, const char *filename,
    char **error_string);
typedef void (*fn_gs_effect_destroy)(gs_effect_t *effect);
typedef gs_eparam_t *(*fn_gs_effect_get_param_by_name)(const gs_effect_t *effect, const char *name);
typedef void (*fn_gs_effect_set_vec2)(gs_eparam_t *param, const struct vec2 *val);
typedef void (*fn_gs_effect_set_float)(gs_eparam_t *param, float val);

/* texrender (render-to-texture) */
typedef gs_texrender_t *(*fn_gs_texrender_create)(enum gs_color_format format,
    enum gs_zstencil_format zsformat);
typedef void (*fn_gs_texrender_destroy)(gs_texrender_t *texrender);
typedef void (*fn_gs_texrender_reset)(gs_texrender_t *texrender);
typedef bool (*fn_gs_texrender_begin)(gs_texrender_t *texrender, uint32_t cx, uint32_t cy);
typedef void (*fn_gs_texrender_end)(gs_texrender_t *texrender);
typedef gs_texture_t *(*fn_gs_texrender_get_texture)(const gs_texrender_t *texrender);

/* stage surface (GPU -> CPU readback) */
typedef gs_stagesurf_t *(*fn_gs_stagesurface_create)(uint32_t width, uint32_t height,
    enum gs_color_format format);
typedef void (*fn_gs_stagesurface_destroy)(gs_stagesurf_t *stagesurf);
typedef void (*fn_gs_stage_texture)(gs_stagesurf_t *dst, gs_texture_t *src);
typedef bool (*fn_gs_stagesurface_map)(gs_stagesurf_t *stagesurf, uint8_t **data, uint32_t *linesize);
typedef void (*fn_gs_stagesurface_unmap)(gs_stagesurf_t *stagesurf);

/* raster helpers used while a texrender is bound */
typedef void (*fn_gs_clear)(uint32_t clear_flags, const struct vec4 *color, float depth, uint8_t stencil);
typedef void (*fn_gs_ortho)(float left, float right, float top, float bottom, float znear, float zfar);

/* properties / data */
typedef obs_properties_t *(*fn_obs_properties_create)(void);
typedef obs_property_t *(*fn_obs_properties_add_float_slider)(obs_properties_t *props, const char *name,
    const char *description, double min, double max, double step);
typedef obs_property_t *(*fn_obs_properties_add_bool)(obs_properties_t *props, const char *name,
    const char *description);
typedef void (*fn_obs_property_float_set_suffix)(obs_property_t *p, const char *suffix);
typedef double (*fn_obs_data_get_double)(obs_data_t *data, const char *name);
typedef bool (*fn_obs_data_get_bool)(obs_data_t *data, const char *name);
typedef void (*fn_obs_data_set_default_double)(obs_data_t *data, const char *name, double val);
typedef void (*fn_obs_data_set_default_bool)(obs_data_t *data, const char *name, bool val);

#ifdef __cplusplus
}
#endif
