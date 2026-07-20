// Minimal re-declaration of the libobs C ABI used by the ViewLab Mirror source plugin.
//
// Provenance: declarations transcribed from obs-studio's libobs headers
// (https://github.com/obsproject/obs-studio, GPL-2.0-or-later); the obs_source_info layout
// is copied field-for-field from libobs/obs-source.h. This plugin is therefore distributed
// under GPL-2.0-or-later (see LICENSE in this directory). No obs-studio implementation code
// is copied.
//
// Every libobs function is resolved at runtime from the obs.dll module already loaded in
// the OBS process (see OBS_RESOLVE in viewlab-mirror.c), so the plugin builds without the
// OBS SDK and never hard-links a specific libobs import library. obs_register_source_s
// receives sizeof(struct obs_source_info) and libobs copies min(size, its own size), which
// keeps the prefix-compatible layout safe across OBS releases.
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
typedef struct obs_module obs_module_t;
typedef struct obs_missing_files obs_missing_files_t;
typedef void (*obs_source_enum_proc_t)(obs_source_t *parent, obs_source_t *child, void *param);

/* Small graphics vectors (libobs graphics/vec*.h layouts). vec3/vec4 are 16-byte aligned
 * in libobs (union with a __m128); we only read/write the scalar members, and always pass a
 * pointer, so a plain-float layout with a trailing pad is ABI-safe for our set calls. */
struct vec2 { float x, y; };
struct vec3 { float x, y, z, w_pad; };

enum obs_source_type { OBS_SOURCE_TYPE_INPUT = 0, OBS_SOURCE_TYPE_FILTER = 1 };

/* gs_color_format prefix (libobs graphics/graphics.h). We only ever pass GS_RGBA. */
enum gs_color_format { GS_CF_UNKNOWN = 0, GS_A8 = 1, GS_R8 = 2, GS_RGBA = 3, GS_BGRX = 4, GS_BGRA = 5 };
enum obs_allow_direct_render { OBS_NO_DIRECT_RENDERING = 0, OBS_ALLOW_DIRECT_RENDERING = 1 };
enum obs_icon_type { OBS_ICON_TYPE_UNKNOWN = 0, OBS_ICON_TYPE_DESKTOP_CAPTURE = 8 };
enum obs_combo_type { OBS_COMBO_TYPE_INVALID = 0, OBS_COMBO_TYPE_EDITABLE = 1, OBS_COMBO_TYPE_LIST = 2 };
enum obs_combo_format { OBS_COMBO_FORMAT_INVALID = 0, OBS_COMBO_FORMAT_INT = 1 };
enum obs_base_effect { OBS_EFFECT_DEFAULT = 0 };
enum gs_color_space { GS_CS_SRGB = 0 };

#define OBS_SOURCE_VIDEO (1 << 0)
#define OBS_SOURCE_CUSTOM_DRAW (1 << 3)
#define OBS_SOURCE_DO_NOT_DUPLICATE (1 << 11)

#define LOG_ERROR 100
#define LOG_WARNING 200
#define LOG_INFO 300

/* Field-for-field copy of libobs/obs-source.h struct obs_source_info. Only the leading
 * members are populated; the rest stay null and libobs treats them as absent callbacks. */
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
    enum obs_icon_type icon_type;
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
typedef void (*fn_obs_enter_graphics)(void);
typedef void (*fn_obs_leave_graphics)(void);
typedef gs_effect_t *(*fn_obs_get_base_effect)(enum obs_base_effect effect);
typedef bool (*fn_gs_effect_loop)(gs_effect_t *effect, const char *name);
typedef void (*fn_obs_source_draw)(gs_texture_t *texture, int x, int y, uint32_t cx, uint32_t cy, bool flip);
typedef gs_texture_t *(*fn_gs_texture_open_shared)(uint32_t handle);
typedef void (*fn_gs_texture_destroy)(gs_texture_t *tex);
typedef uint32_t (*fn_gs_texture_get_width)(const gs_texture_t *tex);
typedef uint32_t (*fn_gs_texture_get_height)(const gs_texture_t *tex);
typedef obs_properties_t *(*fn_obs_properties_create)(void);
typedef obs_property_t *(*fn_obs_properties_add_list)(obs_properties_t *props, const char *name,
    const char *description, enum obs_combo_type type, enum obs_combo_format format);
typedef size_t (*fn_obs_property_list_add_int)(obs_property_t *p, const char *name, long long val);
typedef obs_property_t *(*fn_obs_properties_add_text_like)(obs_properties_t *props, const char *name,
    const char *description, int type);
typedef long long (*fn_obs_data_get_int)(obs_data_t *data, const char *name);
typedef void (*fn_obs_data_set_default_int)(obs_data_t *data, const char *name, long long val);

/* ---- effect-based video filter surface (libobs obs-source.h / graphics.h) ------------- */
typedef bool (*fn_obs_source_process_filter_begin)(obs_source_t *filter,
    enum gs_color_format format, enum obs_allow_direct_render allow_direct);
typedef void (*fn_obs_source_process_filter_end)(obs_source_t *filter, gs_effect_t *effect,
    uint32_t width, uint32_t height);
typedef void (*fn_obs_source_skip_video_filter)(obs_source_t *filter);
typedef obs_source_t *(*fn_obs_filter_get_target)(obs_source_t *filter);
typedef obs_source_t *(*fn_obs_filter_get_parent)(obs_source_t *filter);
typedef uint32_t (*fn_obs_source_get_base_width)(obs_source_t *source);
typedef uint32_t (*fn_obs_source_get_base_height)(obs_source_t *source);
typedef gs_effect_t *(*fn_gs_effect_create)(const char *effect_string, const char *filename,
    char **error_string);
typedef void (*fn_gs_effect_destroy)(gs_effect_t *effect);
typedef gs_eparam_t *(*fn_gs_effect_get_param_by_name)(gs_effect_t *effect, const char *name);
typedef void (*fn_gs_effect_set_float)(gs_eparam_t *param, float val);
typedef void (*fn_gs_effect_set_vec2)(gs_eparam_t *param, const struct vec2 *val);
typedef void (*fn_gs_effect_set_vec3)(gs_eparam_t *param, const struct vec3 *val);
typedef void (*fn_bfree)(void *ptr);
typedef double (*fn_obs_data_get_double)(obs_data_t *data, const char *name);
typedef void (*fn_obs_data_set_default_double)(obs_data_t *data, const char *name, double val);
typedef bool (*fn_obs_data_get_bool)(obs_data_t *data, const char *name);
typedef void (*fn_obs_data_set_default_bool)(obs_data_t *data, const char *name, bool val);
typedef obs_property_t *(*fn_obs_properties_add_float_slider)(obs_properties_t *props,
    const char *name, const char *description, double min, double max, double step);
typedef obs_property_t *(*fn_obs_properties_add_bool)(obs_properties_t *props, const char *name,
    const char *description);

#ifdef __cplusplus
}
#endif
