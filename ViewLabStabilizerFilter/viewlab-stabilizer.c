/*
 * ViewLab Enhancer — OBS Studio video-filter plugin.
 *
 * A lightweight, drop-on-any-source enhancement filter for ViewLab's VR mirror capture. Add it
 * to the "ViewLab Mirror Capture" source (or the third-party "OpenXR Mirror Capture" source) and
 * it makes a mirrored VR view comfortable to watch on a stream or recording. Two stages, both
 * tuned for low overhead and low latency:
 *
 *   1. STABILIZATION (optional) — modelled on LiveVisionKit's (LVK) Video-Stabilization filter.
 *      A grid of texture-gated feature blocks is matched frame-to-frame; a least-squares SIMILARITY
 *      fit (translation + rotation + uniform scale, with outlier rejection) recovers the dominant
 *      camera motion — so it corrects head ROLL and dolly/zoom, not just pan. The motion path is
 *      low-passed with a CAUSAL filter (no buffered frame delay => no added latency) plus
 *      anti-windup, and each frame is re-framed within a crop margin to cancel jitter while still
 *      following intentional motion.
 *   2. IMAGE ENHANCEMENT — a single cheap GPU pass: sharpness (unsharp), saturation, vibrance,
 *      contrast, brightness and gamma. All defaults are no-ops, and when nothing is active the
 *      filter is a true zero-cost passthrough (obs_source_skip_video_filter).
 *
 * Dependency-free (no OpenCV): every libobs entry point is resolved at runtime from the host's
 * obs.dll (see OBS_RESOLVE), so this builds with MSVC alone. References studied (not copied):
 * LiveVisionKit (GPL-3.0) and obs-studio's built-in effect filters (GPL-2.0-or-later). libobs
 * declarations are transcribed from obs-studio; this plugin is GPL-2.0-or-later (see LICENSE).
 *
 * Note: the on-disk project/DLL keep the historical name "viewlab-stabilizer" for build
 * stability; the user-facing product is "ViewLab Enhancer".
 */
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>
#include "obs_filter_abi.h"

/* ---- runtime-resolved libobs ---------------------------------------------------------- */
static fn_obs_register_source_s p_obs_register_source_s;
static fn_obs_get_version p_obs_get_version;
static fn_blog p_blog;
static fn_obs_source_process_filter_begin p_process_filter_begin;
static fn_obs_source_process_filter_end p_process_filter_end;
static fn_obs_source_skip_video_filter p_skip_video_filter;
static fn_obs_filter_get_target p_filter_get_target;
static fn_obs_source_get_base_width p_get_base_width;
static fn_obs_source_get_base_height p_get_base_height;
static fn_obs_source_video_render p_source_video_render;
static fn_gs_effect_create p_gs_effect_create;
static fn_gs_effect_destroy p_gs_effect_destroy;
static fn_gs_effect_get_param_by_name p_gs_effect_get_param_by_name;
static fn_gs_effect_set_vec2 p_gs_effect_set_vec2;
static fn_gs_effect_set_float p_gs_effect_set_float;
static fn_gs_texrender_create p_gs_texrender_create;
static fn_gs_texrender_destroy p_gs_texrender_destroy;
static fn_gs_texrender_reset p_gs_texrender_reset;
static fn_gs_texrender_begin p_gs_texrender_begin;
static fn_gs_texrender_end p_gs_texrender_end;
static fn_gs_texrender_get_texture p_gs_texrender_get_texture;
static fn_gs_stagesurface_create p_gs_stagesurface_create;
static fn_gs_stagesurface_destroy p_gs_stagesurface_destroy;
static fn_gs_stage_texture p_gs_stage_texture;
static fn_gs_stagesurface_map p_gs_stagesurface_map;
static fn_gs_stagesurface_unmap p_gs_stagesurface_unmap;
static fn_gs_clear p_gs_clear;
static fn_gs_ortho p_gs_ortho;
static fn_obs_properties_create p_obs_properties_create;
static fn_obs_properties_add_float_slider p_obs_properties_add_float_slider;
static fn_obs_properties_add_bool p_obs_properties_add_bool;
static fn_obs_property_float_set_suffix p_obs_property_float_set_suffix;
static fn_obs_data_get_double p_obs_data_get_double;
static fn_obs_data_get_bool p_obs_data_get_bool;
static fn_obs_data_set_default_double p_obs_data_set_default_double;
static fn_obs_data_set_default_bool p_obs_data_set_default_bool;

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
    OBS_RESOLVE(p_source_video_render, "obs_source_video_render");
    OBS_RESOLVE(p_gs_effect_create, "gs_effect_create");
    OBS_RESOLVE(p_gs_effect_destroy, "gs_effect_destroy");
    OBS_RESOLVE(p_gs_effect_get_param_by_name, "gs_effect_get_param_by_name");
    OBS_RESOLVE(p_gs_effect_set_vec2, "gs_effect_set_vec2");
    OBS_RESOLVE(p_gs_effect_set_float, "gs_effect_set_float");
    OBS_RESOLVE(p_gs_texrender_create, "gs_texrender_create");
    OBS_RESOLVE(p_gs_texrender_destroy, "gs_texrender_destroy");
    OBS_RESOLVE(p_gs_texrender_reset, "gs_texrender_reset");
    OBS_RESOLVE(p_gs_texrender_begin, "gs_texrender_begin");
    OBS_RESOLVE(p_gs_texrender_end, "gs_texrender_end");
    OBS_RESOLVE(p_gs_texrender_get_texture, "gs_texrender_get_texture");
    OBS_RESOLVE(p_gs_stagesurface_create, "gs_stagesurface_create");
    OBS_RESOLVE(p_gs_stagesurface_destroy, "gs_stagesurface_destroy");
    OBS_RESOLVE(p_gs_stage_texture, "gs_stage_texture");
    OBS_RESOLVE(p_gs_stagesurface_map, "gs_stagesurface_map");
    OBS_RESOLVE(p_gs_stagesurface_unmap, "gs_stagesurface_unmap");
    OBS_RESOLVE(p_gs_clear, "gs_clear");
    OBS_RESOLVE(p_gs_ortho, "gs_ortho");
    OBS_RESOLVE(p_obs_properties_create, "obs_properties_create");
    OBS_RESOLVE(p_obs_properties_add_float_slider, "obs_properties_add_float_slider");
    OBS_RESOLVE(p_obs_properties_add_bool, "obs_properties_add_bool");
    OBS_RESOLVE(p_obs_property_float_set_suffix, "obs_property_float_set_suffix");
    OBS_RESOLVE(p_obs_data_get_double, "obs_data_get_double");
    OBS_RESOLVE(p_obs_data_get_bool, "obs_data_get_bool");
    OBS_RESOLVE(p_obs_data_set_default_double, "obs_data_set_default_double");
    OBS_RESOLVE(p_obs_data_set_default_bool, "obs_data_set_default_bool");
    return true;
}

/* ---- tunables ------------------------------------------------------------------------- */
#define STAB_ANALYSIS_W 192     /* downscaled analysis width (height follows aspect)          */
#define STAB_HB 10              /* feature-block half-size (block is (2*HB+1)^2)              */
#define STAB_SEARCH 14          /* per-block search radius in analysis pixels                 */
#define STAB_MARGIN (STAB_HB + STAB_SEARCH + 1)
#define STAB_GX 8               /* max feature grid columns                                   */
#define STAB_GY 5               /* max feature grid rows                                      */
#define STAB_MAXBLK (STAB_GX * STAB_GY)
#define STAB_VAR_MIN 12.0       /* min block luma variance to trust it (skips flat/black)     */

/* One combined pass: similarity re-frame (rotate/scale/translate around centre) then image
 * enhancement. process_filter_end binds "image" and "ViewProj"; we drive the rest. Sharpen
 * branches out entirely at amount 0. */
static const char *STAB_EFFECT =
    "uniform float4x4 ViewProj;\n"
    "uniform texture2d image;\n"
    "uniform float aspect;\n"      /* base_w / base_h                                          */
    "uniform float zoom;\n"        /* total centred scale (<1 zooms in to make border budget)  */
    "uniform float rot;\n"         /* residual rotation to cancel (radians)                    */
    "uniform float2 trans;\n"      /* residual translation to cancel (uv)                      */
    "uniform float2 texel;\n"
    "uniform float sharpen;\n"
    "uniform float saturation;\n"
    "uniform float vibrance;\n"
    "uniform float contrast;\n"
    "uniform float brightness;\n"
    "uniform float gamma_inv;\n"
    "sampler_state stabSampler { Filter = Linear; AddressU = Clamp; AddressV = Clamp; };\n"
    "struct VertInOut { float4 pos : POSITION; float2 uv : TEXCOORD0; };\n"
    "VertInOut VSStab(VertInOut v) {\n"
    "    VertInOut o;\n"
    "    o.pos = mul(float4(v.pos.xyz, 1.0), ViewProj);\n"
    "    o.uv  = v.uv;\n"
    "    return o;\n"
    "}\n"
    "float2 remap(float2 uv) {\n"
    "    float2 p = uv - 0.5;\n"
    "    p.x *= aspect;\n"                                   /* to square space */
    "    float ca = cos(rot), sa = sin(rot);\n"
    "    p = float2(p.x * ca - p.y * sa, p.x * sa + p.y * ca);\n"
    "    p *= zoom;\n"
    "    p.x /= aspect;\n"
    "    return 0.5 + p + trans;\n"
    "}\n"
    "float4 PSStab(VertInOut v) : TARGET {\n"
    "    float2 uv = remap(v.uv);\n"
    "    float3 c = image.Sample(stabSampler, uv).rgb;\n"
    "    if (sharpen > 0.0001) {\n"
    "        float3 blur = image.Sample(stabSampler, uv + float2(texel.x, 0.0)).rgb\n"
    "                    + image.Sample(stabSampler, uv - float2(texel.x, 0.0)).rgb\n"
    "                    + image.Sample(stabSampler, uv + float2(0.0, texel.y)).rgb\n"
    "                    + image.Sample(stabSampler, uv - float2(0.0, texel.y)).rgb;\n"
    "        c = c + sharpen * (c - 0.25 * blur);\n"
    "    }\n"
    "    c = c * brightness;\n"
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
    "technique Draw { pass { vertex_shader = VSStab(v); pixel_shader = PSStab(v); } }\n";

/* ---- source state --------------------------------------------------------------------- */
struct viewlab_stab {
    obs_source_t *source;

    /* stabilization settings */
    bool stab_enabled;
    bool stab_rotation;   /* correct head roll                                                */
    bool stab_zoom;       /* correct dolly / breathing                                        */
    double smoothing;     /* 0..100                                                           */
    double max_crop;      /* 0..50 %                                                          */

    /* image-enhancement settings (user percents) */
    double sharpness, saturation, vibrance, contrast, brightness, gamma;

    /* gpu resources (graphics thread) */
    gs_effect_t *effect;
    gs_eparam_t *p_aspect, *p_zoom, *p_rot, *p_trans, *p_texel;
    gs_eparam_t *p_sharpen, *p_saturation, *p_vibrance, *p_contrast, *p_brightness, *p_gamma_inv;
    gs_texrender_t *texrender;
    gs_stagesurf_t *stage;

    /* analysis buffers */
    uint32_t aw, ah, base_w, base_h;
    uint8_t *lum_prev, *lum_cur;
    bool has_prev;

    /* motion path (source px / radians / ln-scale) and its causal low-pass */
    double path_x, path_y, path_rot, path_ls;
    double smooth_x, smooth_y, smooth_rot, smooth_ls;
};

struct stab_motion { bool valid; double dtx, dty, dtheta, dlns; };

static double clampd(double v, double lo, double hi) { return v < lo ? lo : (v > hi ? hi : v); }

static void stab_free_buffers(struct viewlab_stab *ctx)
{
    free(ctx->lum_prev);
    free(ctx->lum_cur);
    ctx->lum_prev = ctx->lum_cur = NULL;
    ctx->has_prev = false;
    ctx->path_x = ctx->path_y = ctx->path_rot = ctx->path_ls = 0.0;
    ctx->smooth_x = ctx->smooth_y = ctx->smooth_rot = ctx->smooth_ls = 0.0;
}

static bool stab_ensure_analysis(struct viewlab_stab *ctx, uint32_t base_w, uint32_t base_h)
{
    if (base_w == 0 || base_h == 0)
        return false;
    if (base_w == ctx->base_w && base_h == ctx->base_h && ctx->lum_cur && ctx->lum_prev)
        return true;

    ctx->base_w = base_w;
    ctx->base_h = base_h;
    ctx->aw = STAB_ANALYSIS_W;
    if (ctx->aw > base_w)
        ctx->aw = base_w;
    ctx->ah = (uint32_t)((double)ctx->aw * (double)base_h / (double)base_w + 0.5);
    if (ctx->ah < 2)
        ctx->ah = 2;

    /* need room for at least a small feature grid plus the per-block search window */
    if (ctx->aw < (uint32_t)(2 * STAB_MARGIN + 2) || ctx->ah < (uint32_t)(2 * STAB_MARGIN + 2)) {
        stab_free_buffers(ctx);
        ctx->base_w = ctx->base_h = 0;   /* force re-check next frame */
        return false;
    }

    stab_free_buffers(ctx);
    size_t n = (size_t)ctx->aw * ctx->ah;
    ctx->lum_prev = (uint8_t *)malloc(n);
    ctx->lum_cur = (uint8_t *)malloc(n);
    if (!ctx->lum_prev || !ctx->lum_cur) {
        stab_free_buffers(ctx);
        return false;
    }
    if (ctx->stage) {
        p_gs_stagesurface_destroy(ctx->stage);
        ctx->stage = NULL;
    }
    return true;
}

/* Render the target into the analysis texrender and read luma back to lum_cur. */
static bool stab_capture_luma(struct viewlab_stab *ctx, obs_source_t *target)
{
    if (!ctx->texrender)
        ctx->texrender = p_gs_texrender_create(GS_RGBA, GS_ZS_NONE);
    if (!ctx->texrender)
        return false;
    if (!ctx->stage)
        ctx->stage = p_gs_stagesurface_create(ctx->aw, ctx->ah, GS_RGBA);
    if (!ctx->stage)
        return false;

    p_gs_texrender_reset(ctx->texrender);
    if (!p_gs_texrender_begin(ctx->texrender, ctx->aw, ctx->ah))
        return false;
    struct vec4 clear = {0.f, 0.f, 0.f, 1.f};
    p_gs_clear(GS_CLEAR_COLOR, &clear, 0.f, 0);
    p_gs_ortho(0.f, (float)ctx->base_w, 0.f, (float)ctx->base_h, -100.f, 100.f);
    p_source_video_render(target);
    p_gs_texrender_end(ctx->texrender);

    gs_texture_t *tex = p_gs_texrender_get_texture(ctx->texrender);
    if (!tex)
        return false;
    p_gs_stage_texture(ctx->stage, tex);

    uint8_t *data = NULL;
    uint32_t linesize = 0;
    if (!p_gs_stagesurface_map(ctx->stage, &data, &linesize) || !data)
        return false;
    for (uint32_t y = 0; y < ctx->ah; ++y) {
        const uint8_t *row = data + (size_t)y * linesize;
        uint8_t *dst = ctx->lum_cur + (size_t)y * ctx->aw;
        for (uint32_t x = 0; x < ctx->aw; ++x) {
            const uint8_t *px = row + (size_t)x * 4;   /* GS_RGBA byte order */
            dst[x] = (uint8_t)((px[0] * 54 + px[1] * 183 + px[2] * 19) >> 8);
        }
    }
    p_gs_stagesurface_unmap(ctx->stage);
    return true;
}

/* Full SAD of the block centred at (bx,by) against prev shifted by (dx,dy). No early-out, so
 * the value is usable for sub-pixel parabolic refinement of the minimum. */
static long long stab_block_sad(const struct viewlab_stab *ctx, int bx, int by, int dx, int dy)
{
    const uint32_t aw = ctx->aw;
    const uint8_t *cur = ctx->lum_cur, *prev = ctx->lum_prev;
    long long sad = 0;
    for (int y = by - STAB_HB; y <= by + STAB_HB; ++y) {
        const uint8_t *crow = cur + (size_t)y * aw;
        const uint8_t *prow = prev + (size_t)(y + dy) * aw + dx;
        for (int x = bx - STAB_HB; x <= bx + STAB_HB; ++x)
            sad += abs((int)crow[x] - (int)prow[x]);
    }
    return sad;
}

/* Best local translation for the block centred at (bx,by): fractional offset o where
 * cur[x,y] ~= prev[x+o,y+o]. Integer minimum by SAD, then parabolic sub-pixel refinement on
 * each axis (removes the 1-px quantisation that makes integer-only stabilization jitter).
 * Returns false if the block is untextured or the best offset is pinned to the search border. */
static bool stab_block_match(const struct viewlab_stab *ctx, int bx, int by, double *ox, double *oy)
{
    const uint32_t aw = ctx->aw;
    const uint8_t *cur = ctx->lum_cur;

    /* texture gate: variance of the current block */
    long sum = 0, sumsq = 0;
    int count = 0;
    for (int y = by - STAB_HB; y <= by + STAB_HB; ++y) {
        const uint8_t *crow = cur + (size_t)y * aw;
        for (int x = bx - STAB_HB; x <= bx + STAB_HB; ++x) {
            int v = crow[x];
            sum += v; sumsq += v * v; ++count;
        }
    }
    double mean = (double)sum / count;
    double var = (double)sumsq / count - mean * mean;
    if (var < STAB_VAR_MIN)
        return false;

    int best_ox = 0, best_oy = 0;
    long long best = -1;
    for (int dy = -STAB_SEARCH; dy <= STAB_SEARCH; ++dy) {
        for (int dx = -STAB_SEARCH; dx <= STAB_SEARCH; ++dx) {
            long long sad = stab_block_sad(ctx, bx, by, dx, dy);
            if (best < 0 || sad < best) { best = sad; best_ox = dx; best_oy = dy; }
        }
    }
    if (abs(best_ox) == STAB_SEARCH || abs(best_oy) == STAB_SEARCH)
        return false;   /* pinned to border => unreliable */

    /* Parabolic sub-pixel refinement: fit a parabola through the SAD minimum and its two
     * neighbours on each axis, take the vertex. Neighbours are interior (best not on border). */
    double subx = 0.0, suby = 0.0;
    long long s0 = best;
    long long sxm = stab_block_sad(ctx, bx, by, best_ox - 1, best_oy);
    long long sxp = stab_block_sad(ctx, bx, by, best_ox + 1, best_oy);
    double dxdenom = (double)(sxm + sxp - 2 * s0);
    if (dxdenom > 1e-6) subx = clampd(0.5 * (double)(sxm - sxp) / dxdenom, -1.0, 1.0);
    long long sym = stab_block_sad(ctx, bx, by, best_ox, best_oy - 1);
    long long syp = stab_block_sad(ctx, bx, by, best_ox, best_oy + 1);
    double dydenom = (double)(sym + syp - 2 * s0);
    if (dydenom > 1e-6) suby = clampd(0.5 * (double)(sym - syp) / dydenom, -1.0, 1.0);

    *ox = (double)best_ox + subx;
    *oy = (double)best_oy + suby;
    return true;
}

/* Least-squares 2D similarity mapping src points (px,py) -> dst points (qx,qy) over `n`
 * inliers, returning frame rotation, ln-scale and the resulting displacement of the frame
 * centre. Points with residual beyond a threshold are rejected once and the fit repeated. */
static bool stab_fit_similarity(const double *px, const double *py,
                                const double *qx, const double *qy, int n,
                                double cx, double cy,
                                double *out_theta, double *out_lns, double *out_dcx, double *out_dcy)
{
    for (int pass = 0; pass < 2; ++pass) {
        double Sx = 0, Sy = 0, Sxp = 0, Syp = 0, Sxx = 0, Sxxp = 0, Sxyp = 0;
        int used = 0;
        for (int i = 0; i < n; ++i) {
            if (px[i] == INFINITY) continue;   /* rejected */
            Sx += px[i]; Sy += py[i]; Sxp += qx[i]; Syp += qy[i];
            Sxx += px[i] * px[i] + py[i] * py[i];
            Sxxp += px[i] * qx[i] + py[i] * qy[i];
            Sxyp += px[i] * qy[i] - py[i] * qx[i];
            ++used;
        }
        if (used < 3)
            return false;
        double denom = Sxx - (Sx * Sx + Sy * Sy) / used;
        if (fabs(denom) < 1e-6)
            return false;
        double a = (Sxxp - (Sx * Sxp + Sy * Syp) / used) / denom;
        double b = (Sxyp - (Sx * Syp - Sy * Sxp) / used) / denom;
        double tx = (Sxp - a * Sx + b * Sy) / used;
        double ty = (Syp - b * Sx - a * Sy) / used;

        if (pass == 0) {
            /* reject worst outliers, then refit */
            double *pxm = (double *)px, *pym = (double *)py;
            for (int i = 0; i < n; ++i) {
                if (px[i] == INFINITY) continue;
                double ex = (a * px[i] - b * py[i] + tx) - qx[i];
                double ey = (b * px[i] + a * py[i] + ty) - qy[i];
                if (ex * ex + ey * ey > 9.0) { pxm[i] = INFINITY; pym[i] = INFINITY; }
            }
            continue;
        }
        double s = sqrt(a * a + b * b);
        *out_theta = atan2(b, a);
        *out_lns = (s > 1e-6) ? log(s) : 0.0;
        double mcx = a * cx - b * cy + tx;
        double mcy = b * cx + a * cy + ty;
        *out_dcx = mcx - cx;
        *out_dcy = mcy - cy;
        return true;
    }
    return false;
}

/* Estimate this frame's dominant camera motion (translation in base px, rotation, ln-scale). */
static struct stab_motion stab_estimate(struct viewlab_stab *ctx)
{
    struct stab_motion m = {0};
    const int lo_x = STAB_MARGIN, hi_x = (int)ctx->aw - 1 - STAB_MARGIN;
    const int lo_y = STAB_MARGIN, hi_y = (int)ctx->ah - 1 - STAB_MARGIN;
    if (hi_x <= lo_x || hi_y <= lo_y)
        return m;

    int nx = STAB_GX, ny = STAB_GY;
    if ((hi_x - lo_x) / (nx - 1 > 0 ? nx - 1 : 1) < STAB_HB) nx = 2;
    if ((hi_y - lo_y) / (ny - 1 > 0 ? ny - 1 : 1) < STAB_HB) ny = 2;

    double px[STAB_MAXBLK], py[STAB_MAXBLK], qx[STAB_MAXBLK], qy[STAB_MAXBLK];
    int n = 0;
    double mean_ox = 0, mean_oy = 0;
    for (int j = 0; j < ny; ++j) {
        int by = (ny == 1) ? (lo_y + hi_y) / 2 : lo_y + (hi_y - lo_y) * j / (ny - 1);
        for (int i = 0; i < nx; ++i) {
            int bx = (nx == 1) ? (lo_x + hi_x) / 2 : lo_x + (hi_x - lo_x) * i / (nx - 1);
            double ox, oy;
            if (!stab_block_match(ctx, bx, by, &ox, &oy))
                continue;
            /* content displacement = -offset; target point = src + displacement */
            px[n] = bx; py[n] = by;
            qx[n] = bx - ox; qy[n] = by - oy;
            mean_ox += -ox; mean_oy += -oy;
            ++n;
        }
    }
    if (n == 0)
        return m;

    const double sx = (double)ctx->base_w / (double)ctx->aw;
    const double sy = (double)ctx->base_h / (double)ctx->ah;

    double theta = 0, lns = 0, dcx = 0, dcy = 0;
    if (n >= 3 &&
        stab_fit_similarity(px, py, qx, qy, n, ctx->aw * 0.5, ctx->ah * 0.5,
                            &theta, &lns, &dcx, &dcy)) {
        m.dtx = dcx * sx;
        m.dty = dcy * sy;
        m.dtheta = theta;
        m.dlns = lns;
    } else {
        /* too few reliable blocks: translation only */
        m.dtx = (mean_ox / n) * sx;
        m.dty = (mean_oy / n) * sy;
        m.dtheta = 0.0;
        m.dlns = 0.0;
    }
    m.valid = true;
    return m;
}

/* ---- obs_source_info callbacks -------------------------------------------------------- */
static const char *stab_get_name(void *type_data) { (void)type_data; return "ViewLab Enhancer"; }

static void stab_read_settings(struct viewlab_stab *ctx, obs_data_t *settings)
{
    ctx->stab_enabled = p_obs_data_get_bool(settings, "enabled");
    ctx->stab_rotation = p_obs_data_get_bool(settings, "stab_rotation");
    ctx->stab_zoom = p_obs_data_get_bool(settings, "stab_zoom");
    ctx->smoothing = clampd(p_obs_data_get_double(settings, "smoothing"), 0.0, 100.0);
    ctx->max_crop = clampd(p_obs_data_get_double(settings, "max_crop"), 0.0, 50.0);
    ctx->sharpness = clampd(p_obs_data_get_double(settings, "sharpness"), 0.0, 100.0);
    ctx->saturation = clampd(p_obs_data_get_double(settings, "saturation"), 0.0, 200.0);
    ctx->vibrance = clampd(p_obs_data_get_double(settings, "vibrance"), -100.0, 100.0);
    ctx->contrast = clampd(p_obs_data_get_double(settings, "contrast"), 0.0, 200.0);
    ctx->brightness = clampd(p_obs_data_get_double(settings, "brightness"), 0.0, 200.0);
    ctx->gamma = clampd(p_obs_data_get_double(settings, "gamma"), 10.0, 300.0);
}

static bool stab_image_active(const struct viewlab_stab *ctx)
{
    return ctx->sharpness > 0.05 || fabs(ctx->saturation - 100.0) > 0.05 ||
           fabs(ctx->vibrance) > 0.05 || fabs(ctx->contrast - 100.0) > 0.05 ||
           fabs(ctx->brightness - 100.0) > 0.05 || fabs(ctx->gamma - 100.0) > 0.05;
}

static void *stab_create(obs_data_t *settings, obs_source_t *source)
{
    struct viewlab_stab *ctx = calloc(1, sizeof(*ctx));
    if (!ctx)
        return NULL;
    ctx->source = source;
    stab_read_settings(ctx, settings);
    p_blog(LOG_INFO, "[viewlab-enhancer] filter created");
    return ctx;
}

static void stab_destroy(void *data)
{
    struct viewlab_stab *ctx = data;
    if (!ctx)
        return;
    if (ctx->effect) p_gs_effect_destroy(ctx->effect);
    if (ctx->texrender) p_gs_texrender_destroy(ctx->texrender);
    if (ctx->stage) p_gs_stagesurface_destroy(ctx->stage);
    stab_free_buffers(ctx);
    free(ctx);
}

static void stab_update(void *data, obs_data_t *settings)
{
    stab_read_settings((struct viewlab_stab *)data, settings);
}

static void stab_get_defaults(obs_data_t *settings)
{
    p_obs_data_set_default_bool(settings, "enabled", true);
    p_obs_data_set_default_bool(settings, "stab_rotation", true);
    p_obs_data_set_default_bool(settings, "stab_zoom", false);
    p_obs_data_set_default_double(settings, "smoothing", 75.0);   /* clearly steady by default */
    p_obs_data_set_default_double(settings, "max_crop", 16.0);    /* enough budget for VR shake */
    p_obs_data_set_default_double(settings, "sharpness", 0.0);
    p_obs_data_set_default_double(settings, "saturation", 100.0);
    p_obs_data_set_default_double(settings, "vibrance", 0.0);
    p_obs_data_set_default_double(settings, "contrast", 100.0);
    p_obs_data_set_default_double(settings, "brightness", 100.0);
    p_obs_data_set_default_double(settings, "gamma", 100.0);
}

static obs_properties_t *stab_get_properties(void *data)
{
    (void)data;
    obs_properties_t *props = p_obs_properties_create();

    /* --- stabilization (low-latency; causal, no buffered frame delay) --- */
    p_obs_properties_add_bool(props, "enabled", "Stabilize (smooth VR head shake — low latency)");
    p_obs_properties_add_bool(props, "stab_rotation", "  · also correct rotation (head roll / tilt)");
    p_obs_properties_add_bool(props, "stab_zoom", "  · also correct zoom (dolly / breathing)");
    p_obs_properties_add_float_slider(props, "smoothing",
        "  Steadiness — higher locks the view harder (0 = off, 75 default)", 0.0, 100.0, 1.0);
    obs_property_t *cr = p_obs_properties_add_float_slider(props, "max_crop",
        "  Correction range — border zoomed in to hide shake (more = stronger)", 0.0, 50.0, 0.5);
    if (p_obs_property_float_set_suffix) p_obs_property_float_set_suffix(cr, " %");

    /* --- image enhancement (single cheap GPU pass; neutral values = no cost) --- */
    obs_property_t *sh = p_obs_properties_add_float_slider(props, "sharpness", "Sharpness", 0.0, 100.0, 1.0);
    if (p_obs_property_float_set_suffix) p_obs_property_float_set_suffix(sh, " %");
    obs_property_t *sa = p_obs_properties_add_float_slider(props, "saturation", "Saturation", 0.0, 200.0, 1.0);
    if (p_obs_property_float_set_suffix) p_obs_property_float_set_suffix(sa, " %");
    obs_property_t *vi = p_obs_properties_add_float_slider(props, "vibrance",
        "Vibrance (boosts muted colours, protects vivid ones)", -100.0, 100.0, 1.0);
    if (p_obs_property_float_set_suffix) p_obs_property_float_set_suffix(vi, " %");
    obs_property_t *co = p_obs_properties_add_float_slider(props, "contrast", "Contrast", 0.0, 200.0, 1.0);
    if (p_obs_property_float_set_suffix) p_obs_property_float_set_suffix(co, " %");
    obs_property_t *br = p_obs_properties_add_float_slider(props, "brightness", "Brightness", 0.0, 200.0, 1.0);
    if (p_obs_property_float_set_suffix) p_obs_property_float_set_suffix(br, " %");
    obs_property_t *ga = p_obs_properties_add_float_slider(props, "gamma", "Gamma", 10.0, 300.0, 1.0);
    if (p_obs_property_float_set_suffix) p_obs_property_float_set_suffix(ga, " %");
    return props;
}

/* Causal low-pass one path parameter and return the clamped residual (with anti-windup:
 * the excess beyond the clamp is absorbed into the smoothed value so it never sticks). */
static double stab_track(double *smooth, double path, double alpha, double limit)
{
    *smooth += alpha * (path - *smooth);
    double r = path - *smooth;
    double rc = clampd(r, -limit, limit);
    *smooth += (r - rc);
    return rc;
}

static void stab_video_render(void *data, gs_effect_t *unused)
{
    (void)unused;
    struct viewlab_stab *ctx = data;
    obs_source_t *target = p_filter_get_target(ctx->source);
    if (!target) { p_skip_video_filter(ctx->source); return; }

    bool image_active = stab_image_active(ctx);
    if (!ctx->stab_enabled && !image_active) { p_skip_video_filter(ctx->source); return; }

    uint32_t base_w = p_get_base_width(target);
    uint32_t base_h = p_get_base_height(target);

    if (!ctx->effect) {
        ctx->effect = p_gs_effect_create(STAB_EFFECT, NULL, NULL);
        if (ctx->effect) {
            ctx->p_aspect = p_gs_effect_get_param_by_name(ctx->effect, "aspect");
            ctx->p_zoom = p_gs_effect_get_param_by_name(ctx->effect, "zoom");
            ctx->p_rot = p_gs_effect_get_param_by_name(ctx->effect, "rot");
            ctx->p_trans = p_gs_effect_get_param_by_name(ctx->effect, "trans");
            ctx->p_texel = p_gs_effect_get_param_by_name(ctx->effect, "texel");
            ctx->p_sharpen = p_gs_effect_get_param_by_name(ctx->effect, "sharpen");
            ctx->p_saturation = p_gs_effect_get_param_by_name(ctx->effect, "saturation");
            ctx->p_vibrance = p_gs_effect_get_param_by_name(ctx->effect, "vibrance");
            ctx->p_contrast = p_gs_effect_get_param_by_name(ctx->effect, "contrast");
            ctx->p_brightness = p_gs_effect_get_param_by_name(ctx->effect, "brightness");
            ctx->p_gamma_inv = p_gs_effect_get_param_by_name(ctx->effect, "gamma_inv");
        }
    }
    if (!ctx->effect) { p_skip_video_filter(ctx->source); return; }

    /* --- stabilization --------------------------------------------------------------- */
    double crop_frac = 0.0, res_rot = 0.0, res_lns = 0.0, res_tx = 0.0, res_ty = 0.0;
    if (ctx->stab_enabled && stab_ensure_analysis(ctx, base_w, base_h)) {
        if (stab_capture_luma(ctx, target)) {
            if (ctx->has_prev) {
                struct stab_motion m = stab_estimate(ctx);
                if (m.valid) {
                    ctx->path_x += m.dtx;
                    ctx->path_y += m.dty;
                    if (ctx->stab_rotation) ctx->path_rot += m.dtheta;
                    if (ctx->stab_zoom) ctx->path_ls += m.dlns;
                }
            }
            uint8_t *tmp = ctx->lum_prev; ctx->lum_prev = ctx->lum_cur; ctx->lum_cur = tmp;
            ctx->has_prev = true;
        }
        /* Perceptual smoothing: map the 0..100 slider geometrically onto the low-pass
         * coefficient so the middle of the range is already clearly steady. alpha is the
         * fraction of the raw path followed per frame — small alpha = long time constant =
         * heavier stabilization. 0 -> 0.60 (follows almost everything), 100 -> 0.006 (very
         * locked, ~160-frame time constant). */
        double s01 = clampd(ctx->smoothing / 100.0, 0.0, 1.0);
        double alpha = 0.60 * pow(0.006 / 0.60, s01);
        crop_frac = ctx->max_crop / 100.0;
        double aspect = (double)base_w / (double)base_h;
        double corner = 0.5 * sqrt(aspect * aspect + 1.0);   /* corner radius in square space */

        res_tx = stab_track(&ctx->smooth_x, ctx->path_x, alpha, 0.5 * crop_frac * base_w);
        res_ty = stab_track(&ctx->smooth_y, ctx->path_y, alpha, 0.5 * crop_frac * base_h);
        double rot_lim = (corner > 1e-6) ? (0.5 * crop_frac / corner) : 0.0;
        res_rot = ctx->stab_rotation ? stab_track(&ctx->smooth_rot, ctx->path_rot, alpha, rot_lim) : 0.0;
        double ls_lim = log(1.0 + 0.5 * crop_frac);
        res_lns = ctx->stab_zoom ? stab_track(&ctx->smooth_ls, ctx->path_ls, alpha, ls_lim) : 0.0;
    }

    float aspect = base_h ? (float)base_w / (float)base_h : 1.f;
    float zoom = (float)((1.0 - crop_frac) * exp(res_lns));
    struct vec2 trans = { (float)(base_w ? res_tx / (double)base_w : 0.0),
                          (float)(base_h ? res_ty / (double)base_h : 0.0) };
    struct vec2 texel = { base_w ? 1.0f / (float)base_w : 0.f, base_h ? 1.0f / (float)base_h : 0.f };

    /* --- emit the re-framed, enhanced frame ------------------------------------------ */
    if (!p_process_filter_begin(ctx->source, GS_RGBA, OBS_ALLOW_DIRECT_RENDERING)) {
        p_skip_video_filter(ctx->source);
        return;
    }
    if (ctx->p_aspect) p_gs_effect_set_float(ctx->p_aspect, aspect);
    if (ctx->p_zoom) p_gs_effect_set_float(ctx->p_zoom, zoom);
    if (ctx->p_rot) p_gs_effect_set_float(ctx->p_rot, (float)res_rot);
    if (ctx->p_trans) p_gs_effect_set_vec2(ctx->p_trans, &trans);
    if (ctx->p_texel) p_gs_effect_set_vec2(ctx->p_texel, &texel);
    if (ctx->p_sharpen) p_gs_effect_set_float(ctx->p_sharpen, (float)(ctx->sharpness / 100.0));
    if (ctx->p_saturation) p_gs_effect_set_float(ctx->p_saturation, (float)(ctx->saturation / 100.0));
    if (ctx->p_vibrance) p_gs_effect_set_float(ctx->p_vibrance, (float)(ctx->vibrance / 100.0));
    if (ctx->p_contrast) p_gs_effect_set_float(ctx->p_contrast, (float)(ctx->contrast / 100.0));
    if (ctx->p_brightness) p_gs_effect_set_float(ctx->p_brightness, (float)(ctx->brightness / 100.0));
    if (ctx->p_gamma_inv) p_gs_effect_set_float(ctx->p_gamma_inv, (float)(100.0 / ctx->gamma));
    p_process_filter_end(ctx->source, ctx->effect, base_w, base_h);
}

/* ---- module entry points (stable OBS module ABI) -------------------------------------- */
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
    info.get_name = stab_get_name;
    info.create = stab_create;
    info.destroy = stab_destroy;
    info.update = stab_update;
    info.get_defaults = stab_get_defaults;
    info.get_properties = stab_get_properties;
    info.video_render = stab_video_render;
    p_obs_register_source_s(&info, sizeof(info));
    p_blog(LOG_INFO, "[viewlab-enhancer] module loaded (ViewLab Enhancer filter registered, id=viewlab_enhancer)");
    return true;
}

__declspec(dllexport) void obs_module_unload(void) {}
__declspec(dllexport) const char *obs_module_name(void) { return "ViewLab Enhancer"; }
__declspec(dllexport) const char *obs_module_description(void)
{
    return "Stabilizes and enhances a mirrored VR view (or any source): low-latency shake smoothing (translation, roll and zoom) plus sharpness, saturation, vibrance, contrast, brightness and gamma.";
}
