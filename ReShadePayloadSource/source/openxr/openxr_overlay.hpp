#pragma once

#include "reshade_api.hpp"
#include <openxr/openxr.h>
#include <cmath>

namespace reshade::openxr
{
	static constexpr uint32_t OVERLAY_WIDTH  = 500;
	static constexpr uint32_t OVERLAY_HEIGHT = 500;

	bool get_active_overlay_resources(api::resource *texture, api::resource_view *target);
	bool get_overlay_resources(XrSession session, api::resource *texture, api::resource_view *target, XrSwapchain *swapchain);
	void set_overlay_visible(XrSession session, bool visible);
	void set_overlay_visible_active(bool visible);

	struct overlay_transform
	{
		float quad_width  = 0.5f;
		float quad_height = 0.5f;
		float distance    = 1.5f;
		float offset_x    = 0.0f;
		float offset_y    = 0.0f;
	};
	bool get_overlay_transform_active(overlay_transform *out);
	void set_overlay_transform_active(const overlay_transform &t);
	bool is_overlay_visible_active();
	bool copy_overlay_to_swapchain(XrSession session);

	void preview_update(const void *rgba_pixels, int width, int height, int row_pitch);
	bool preview_is_visible();
	bool preview_get_mouse_state(float *x, float *y, bool buttons[3], float *wheel);
	bool preview_consume_keyboard_input(uint16_t *characters, size_t capacity, size_t *count,
	                                    bool *backspace, bool *tab, bool *enter);

	// Accumulated drag deltas from the preview window for quad editing.
	// Each call consumes (resets) the accumulators.
	// button 0=LMB, 1=RMB, 2=MMB. Returns false if no edit mode is active.
	bool preview_consume_edit_delta(float *lmb_dx, float *lmb_dy,
	                                float *rmb_dx, float *rmb_dy,
	                                float *mmb_dx, float *mmb_dy,
	                                float *wheel);

	// -----------------------------------------------------------------------
	// Shared memory control block
	// Named mapping:  Local\ReShadeXRControl
	// ViewLab writes, ReShade reads. Increment revision on every write.
	//
	// Quad transform convention:
	//   pos_z positive = forward (ReShade negates when submitting to OpenXR).
	//   Set pos_z to NaN to tell ReShade to use its built-in OKB defaults.
	//
	// Edit modes:
	//   0 = none
	//   1 = Reposition  (blue border):  LMB drag = X/Y,  RMB drag = Z
	//   2 = Transform   (orange border): LMB drag = scale, RMB drag = rotate,
	//                                    scroll = alpha,   MMB drag = curve
	// -----------------------------------------------------------------------
	struct XRControlBlock
	{
		// header — never reorder
		uint32_t version;            // must be 1
		uint32_t size;               // sizeof(XRControlBlock) — layout guard

		// mode
		uint32_t xr_mode;            // 0=Gameplay, 1=Tuning
		uint32_t revision;           // increment on every write

		// preview window
		uint32_t win_headless;       // 1 = WS_POPUP  (no title bar / border)
		uint32_t win_always_on_top;  // 1 = HWND_TOPMOST
		uint32_t win_snap_cursor;    // 1 = window follows cursor; LMB click places it
		uint32_t menu_visible;       // 1 = overlay + preview window visible (was win_reserved)

		// VR quad edit mode (ViewLab sets, ReShade clears when done is irrelevant — stays until changed)
		uint32_t quad_edit_mode;     // 0=none, 1=Reposition, 2=Transform
		uint32_t heartbeat;          // incremented on every write (was quad_reserved)

		// VR quad transform — ViewLab writes desired state; ReShade reads and applies.
		// ReShade also writes back the live state so ViewLab can read current position.
		float quad_pos_x;
		float quad_pos_y;
		float quad_pos_z;            // NaN = use ReShade OKB defaults
		float quad_quat_x;
		float quad_quat_y;
		float quad_quat_z;
		float quad_quat_w;
		float quad_width;            // metres
		float quad_height;           // metres
		float quad_alpha;            // 0.0=transparent … 1.0=opaque
	};
	static constexpr wchar_t XR_CONTROL_SHMEM_NAME[] = L"Local\\ReShadeXRControl";

	static constexpr uint32_t XR_MODE_GAMEPLAY = 0;
	static constexpr uint32_t XR_MODE_TUNING   = 1;

	void                 control_init();
	void                 control_shutdown();
	bool                 control_is_tuning();
	XRControlBlock*      control_block(); // live pointer; nullptr if not connected
	void                 quad_transform_persist();
}
