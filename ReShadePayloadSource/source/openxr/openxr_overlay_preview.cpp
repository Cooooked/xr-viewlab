#include "openxr_overlay.hpp"
#include "dll_log.hpp"
#include <Windows.h>
#include <windowsx.h>
#include <vector>
#include <thread>
#include <mutex>
#include <atomic>
#include <condition_variable>
#include <cstring>
#include <cmath>

#ifndef SRCCOPY
#define SRCCOPY 0x00CC0020
#endif
#ifndef COLOR_WINDOW
#define COLOR_WINDOW 5
#endif

namespace
{
	struct preview_data
	{
		std::atomic<HWND> hwnd { nullptr };
		std::thread thread;
		std::mutex lifecycle_mutex;
		std::condition_variable lifecycle_cv;
		bool thread_ready = false;
		uint32_t users = 0;
		std::mutex  mutex;
		std::vector<uint8_t> pixels;
		std::vector<uint8_t> back_pixels;
		int    width   = 0;
		int    height  = 0;
		std::atomic_bool running { false };

		// Mouse state — texture-space coords for ImGui passthrough
		std::mutex mouse_mutex;
		float  mouse_x = -1.0f;
		float  mouse_y = -1.0f;
		bool   mouse_in_window = false;
		bool   mouse_buttons[3] = {};
		float  mouse_wheel = 0.0f;

		std::mutex keyboard_mutex;
		std::vector<uint16_t> characters;
		bool backspace = false;
		bool tab = false;
		bool enter = false;

		// Drag state — screen-space deltas for quad editing
		bool   lmb_dragging = false;
		bool   rmb_dragging = false;
		bool   mmb_dragging = false;
		int    drag_last_x = 0;
		int    drag_last_y = 0;
		float  lmb_dx = 0, lmb_dy = 0;
		float  rmb_dx = 0, rmb_dy = 0;
		float  mmb_dx = 0, mmb_dy = 0;

		// Snap-to-cursor mode
		bool   snap_cursor_active = false;
		uint32_t applied_headless = UINT32_MAX;
		uint32_t applied_always_on_top = UINT32_MAX;
		uint32_t applied_visible = UINT32_MAX;
		uint32_t applied_edit_mode = UINT32_MAX;
	};

	static preview_data s_preview;
	static constexpr UINT WM_PREVIEW_FRAME = WM_APP + 1;
	static HANDLE s_ctrl_mapping = nullptr;
	static reshade::openxr::XRControlBlock *s_ctrl = nullptr;

	// Returns edit mode (0/1/2) from the control block. Zero if no ViewLab.
	static uint32_t get_edit_mode()
	{
		if (!s_ctrl || s_ctrl->version != 1 || s_ctrl->size != sizeof(reshade::openxr::XRControlBlock))
			return 0;
		return s_ctrl->quad_edit_mode;
	}

	LRESULT CALLBACK preview_wndproc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)
	{
		switch (msg)
		{
		case WM_ERASEBKGND:
			// The full client area is painted below. Suppressing the class-brush erase
			// prevents an intermediate white frame while a new preview frame arrives.
			return 1;

		case WM_PREVIEW_FRAME:
			InvalidateRect(hwnd, nullptr, FALSE);
			return 0;

		case WM_PAINT:
		{
			PAINTSTRUCT ps;
			HDC hdc = BeginPaint(hwnd, &ps);
			{
				std::lock_guard<std::mutex> lock(s_preview.mutex);
				RECT rect;
				GetClientRect(hwnd, &rect);
				if (!s_preview.pixels.empty() && s_preview.width > 0 && s_preview.height > 0)
				{
					BITMAPINFO bmi = {};
					bmi.bmiHeader.biSize        = sizeof(BITMAPINFOHEADER);
					bmi.bmiHeader.biWidth       = s_preview.width;
					bmi.bmiHeader.biHeight      = -s_preview.height;
					bmi.bmiHeader.biPlanes      = 1;
					bmi.bmiHeader.biBitCount    = 32;
					bmi.bmiHeader.biCompression = BI_RGB;

					SetStretchBltMode(hdc, HALFTONE);
					StretchDIBits(hdc, 0, 0, rect.right, rect.bottom,
						0, 0, s_preview.width, s_preview.height,
						s_preview.pixels.data(), &bmi, DIB_RGB_COLORS, SRCCOPY);

					// Draw colored border when ViewLab has an edit mode active
					const uint32_t edit_mode = get_edit_mode();
					if (edit_mode != 0)
					{
						COLORREF color = (edit_mode == 1) ? RGB(0, 120, 255) : RGB(255, 140, 0);
						HPEN pen       = CreatePen(PS_SOLID, 6, color);
						HPEN old_pen   = (HPEN)SelectObject(hdc, pen);
						HBRUSH old_br  = (HBRUSH)SelectObject(hdc, GetStockObject(NULL_BRUSH));
						Rectangle(hdc, 3, 3, rect.right - 3, rect.bottom - 3);
						SelectObject(hdc, old_pen);
						SelectObject(hdc, old_br);
						DeleteObject(pen);
					}
				}
				else
				{
					FillRect(hdc, &rect, static_cast<HBRUSH>(GetStockObject(BLACK_BRUSH)));
				}
			}
			EndPaint(hwnd, &ps);
			return 0;
		}

		case WM_NCHITTEST:
		{
			LRESULT hit = DefWindowProc(hwnd, msg, wp, lp);
			if (hit == HTCLIENT && s_ctrl &&
				s_ctrl->version == 1 && s_ctrl->size == sizeof(reshade::openxr::XRControlBlock))
			{
				// Headless + no edit mode: only the top 30px acts as caption for dragging
				if (s_ctrl->win_headless && get_edit_mode() == 0)
				{
					const POINT pt = { GET_X_LPARAM(lp), GET_Y_LPARAM(lp) };
					RECT rc;
					GetWindowRect(hwnd, &rc);
					if (pt.y - rc.top < 30)
						return HTCAPTION;
				}
			}
			return hit;
		}

		case WM_TIMER:
		{
			// Apply control-block window flags
			if (!s_ctrl || s_ctrl->version != 1 || s_ctrl->size != sizeof(reshade::openxr::XRControlBlock))
				break;

			// Headless toggle
			const bool want_headless = (s_ctrl->win_headless != 0);
			if (s_preview.applied_headless != s_ctrl->win_headless)
			{
				RECT cr;
				GetClientRect(hwnd, &cr);
				if (want_headless)
				{
					SetWindowLong(hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);
					SetWindowPos(hwnd, nullptr, 0, 0, cr.right, cr.bottom,
						SWP_NOMOVE | SWP_NOZORDER | SWP_FRAMECHANGED);
				}
				else
				{
					RECT wr = { 0, 0, cr.right, cr.bottom };
					AdjustWindowRect(&wr, WS_OVERLAPPEDWINDOW, FALSE);
					SetWindowLong(hwnd, GWL_STYLE, WS_OVERLAPPEDWINDOW | WS_VISIBLE);
					SetWindowPos(hwnd, nullptr, 0, 0, wr.right - wr.left, wr.bottom - wr.top,
						SWP_NOMOVE | SWP_NOZORDER | SWP_FRAMECHANGED);
				}
				s_preview.applied_headless = s_ctrl->win_headless;
			}

			// Always on top
			if (s_preview.applied_always_on_top != s_ctrl->win_always_on_top)
			{
				const HWND z_order = s_ctrl->win_always_on_top ? HWND_TOPMOST : HWND_NOTOPMOST;
				SetWindowPos(hwnd, z_order, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
				s_preview.applied_always_on_top = s_ctrl->win_always_on_top;
			}

			// Hide preview window when Desktop VR Menu is disabled
			if (s_preview.applied_visible != s_ctrl->menu_visible)
			{
				ShowWindow(hwnd, s_ctrl->menu_visible ? SW_SHOW : SW_HIDE);
				s_preview.applied_visible = s_ctrl->menu_visible;
			}

			if (s_preview.applied_edit_mode != s_ctrl->quad_edit_mode)
			{
				InvalidateRect(hwnd, nullptr, FALSE);
				s_preview.applied_edit_mode = s_ctrl->quad_edit_mode;
			}

			// Snap to cursor — arm or keep following
			if (s_ctrl->win_snap_cursor)
				s_preview.snap_cursor_active = true;

			if (s_preview.snap_cursor_active)
			{
				POINT cursor;
				GetCursorPos(&cursor);
				RECT wr;
				GetWindowRect(hwnd, &wr);
				const int w = wr.right - wr.left;
				const int h = wr.bottom - wr.top;
				SetWindowPos(hwnd, nullptr, cursor.x - w / 2, cursor.y - h / 2, 0, 0,
					SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
			}
			break;
		}

		case WM_MOUSEMOVE:
		{
			TRACKMOUSEEVENT tme = { sizeof(tme), TME_LEAVE, hwnd, 0 };
			TrackMouseEvent(&tme);

			const int cx = GET_X_LPARAM(lp);
			const int cy = GET_Y_LPARAM(lp);

			RECT rect;
			GetClientRect(hwnd, &rect);

			std::lock_guard<std::mutex> lock(s_preview.mouse_mutex);
			if (rect.right > 0 && rect.bottom > 0)
			{
				s_preview.mouse_x = (float)cx / rect.right  * s_preview.width;
				s_preview.mouse_y = (float)cy / rect.bottom * s_preview.height;
				s_preview.mouse_in_window = true;
			}

			// Accumulate drag deltas when ViewLab has an edit mode active
			if (get_edit_mode() != 0)
			{
				const float dx = (float)(cx - s_preview.drag_last_x);
				const float dy = (float)(cy - s_preview.drag_last_y);
				if (s_preview.lmb_dragging) { s_preview.lmb_dx += dx; s_preview.lmb_dy += dy; }
				if (s_preview.rmb_dragging) { s_preview.rmb_dx += dx; s_preview.rmb_dy += dy; }
				if (s_preview.mmb_dragging) { s_preview.mmb_dx += dx; s_preview.mmb_dy += dy; }
			}
			s_preview.drag_last_x = cx;
			s_preview.drag_last_y = cy;
			return 0;
		}

		case WM_MOUSELEAVE:
		{
			std::lock_guard<std::mutex> lock(s_preview.mouse_mutex);
			s_preview.mouse_in_window = false;
			s_preview.mouse_x = -1.0f;
			s_preview.mouse_y = -1.0f;
			return 0;
		}

		case WM_CHAR:
		{
			std::lock_guard<std::mutex> lock(s_preview.keyboard_mutex);
			if (wp >= 0x20 && wp != 0x7F && s_preview.characters.size() < 256)
				s_preview.characters.push_back(static_cast<uint16_t>(wp));
			return 0;
		}
		case WM_KEYDOWN:
		{
			std::lock_guard<std::mutex> lock(s_preview.keyboard_mutex);
			if (wp == VK_BACK) s_preview.backspace = true;
			if (wp == VK_TAB) s_preview.tab = true;
			if (wp == VK_RETURN) s_preview.enter = true;
			return 0;
		}

		case WM_LBUTTONDOWN:
		{
			SetFocus(hwnd);
			// Snap mode: LMB click places window, consumes click
			if (s_preview.snap_cursor_active)
			{
				s_preview.snap_cursor_active = false;
				if (s_ctrl && s_ctrl->version == 1 && s_ctrl->size == sizeof(reshade::openxr::XRControlBlock))
					s_ctrl->win_snap_cursor = 0;
				return 0;
			}
			std::lock_guard<std::mutex> l(s_preview.mouse_mutex);
			s_preview.mouse_buttons[0] = true;
			s_preview.lmb_dragging     = true;
			s_preview.drag_last_x      = GET_X_LPARAM(lp);
			s_preview.drag_last_y      = GET_Y_LPARAM(lp);
			SetCapture(hwnd);
			return 0;
		}
		case WM_LBUTTONUP:
		{
			std::lock_guard<std::mutex> l(s_preview.mouse_mutex);
			s_preview.mouse_buttons[0] = false;
			s_preview.lmb_dragging     = false;
			ReleaseCapture();
			return 0;
		}
		case WM_RBUTTONDOWN:
		{
			std::lock_guard<std::mutex> l(s_preview.mouse_mutex);
			s_preview.mouse_buttons[1] = true;
			s_preview.rmb_dragging     = true;
			s_preview.drag_last_x      = GET_X_LPARAM(lp);
			s_preview.drag_last_y      = GET_Y_LPARAM(lp);
			return 0;
		}
		case WM_RBUTTONUP:
		{
			std::lock_guard<std::mutex> l(s_preview.mouse_mutex);
			s_preview.mouse_buttons[1] = false;
			s_preview.rmb_dragging     = false;
			return 0;
		}
		case WM_MBUTTONDOWN:
		{
			std::lock_guard<std::mutex> l(s_preview.mouse_mutex);
			s_preview.mouse_buttons[2] = true;
			s_preview.mmb_dragging     = true;
			s_preview.drag_last_x      = GET_X_LPARAM(lp);
			s_preview.drag_last_y      = GET_Y_LPARAM(lp);
			return 0;
		}
		case WM_MBUTTONUP:
		{
			std::lock_guard<std::mutex> l(s_preview.mouse_mutex);
			s_preview.mouse_buttons[2] = false;
			s_preview.mmb_dragging     = false;
			return 0;
		}
		case WM_MOUSEWHEEL:
		{
			std::lock_guard<std::mutex> l(s_preview.mouse_mutex);
			s_preview.mouse_wheel += (float)GET_WHEEL_DELTA_WPARAM(wp) / WHEEL_DELTA;
			return 0;
		}

		case WM_SIZING:
		{
			// Constrain resize to overlay aspect ratio
			RECT *r = reinterpret_cast<RECT *>(lp);
			const float aspect = static_cast<float>(reshade::openxr::OVERLAY_WIDTH) /
			                     static_cast<float>(reshade::openxr::OVERLAY_HEIGHT);
			RECT border = {};
			AdjustWindowRect(&border, GetWindowLong(hwnd, GWL_STYLE), FALSE);
			const int bw = border.right  - border.left;
			const int bh = border.bottom - border.top;
			int cw = (r->right  - r->left) - bw;
			int ch = (r->bottom - r->top)  - bh;
			if (wp == WMSZ_LEFT || wp == WMSZ_RIGHT)
				ch = static_cast<int>(cw / aspect);
			else
				cw = static_cast<int>(ch * aspect);
			if (wp == WMSZ_LEFT || wp == WMSZ_TOPLEFT || wp == WMSZ_BOTTOMLEFT)
				r->left = r->right - cw - bw;
			else
				r->right = r->left + cw + bw;
			if (wp == WMSZ_TOP || wp == WMSZ_TOPLEFT || wp == WMSZ_TOPRIGHT)
				r->top = r->bottom - ch - bh;
			else
				r->bottom = r->top + ch + bh;
			return TRUE;
		}

		case WM_CLOSE:
			DestroyWindow(hwnd);
			return 0;
		case WM_DESTROY:
			PostQuitMessage(0);
			return 0;
		}
		return DefWindowProc(hwnd, msg, wp, lp);
	}

	static void preview_thread_proc()
	{
		HINSTANCE hinst = GetModuleHandle(nullptr);
		WNDCLASS wc = {};
		wc.lpfnWndProc   = preview_wndproc;
		wc.hInstance     = hinst;
		wc.hCursor       = LoadCursor(nullptr, IDC_ARROW);
		wc.hbrBackground = static_cast<HBRUSH>(GetStockObject(BLACK_BRUSH));
		wc.lpszClassName = L"ReShadeVRPreview";
		RegisterClass(&wc);

		const int client_w = static_cast<int>(reshade::openxr::OVERLAY_WIDTH);
		const int client_h = static_cast<int>(reshade::openxr::OVERLAY_HEIGHT);
		RECT r = { 0, 0, client_w, client_h };
		AdjustWindowRect(&r, WS_OVERLAPPEDWINDOW, FALSE);
		const HWND hwnd = CreateWindow(wc.lpszClassName, L"ReShade VR Overlay",
			WS_OVERLAPPEDWINDOW,
			CW_USEDEFAULT, CW_USEDEFAULT, r.right - r.left, r.bottom - r.top,
			nullptr, nullptr, hinst, nullptr);

		s_preview.hwnd.store(hwnd);
		{
			std::lock_guard<std::mutex> lock(s_preview.lifecycle_mutex);
			s_preview.thread_ready = true;
		}
		s_preview.lifecycle_cv.notify_all();
		if (hwnd)
		{
			// Show immediately if menu_visible is already set (ViewLab connected first)
			if (s_ctrl && s_ctrl->version == 1 && s_ctrl->menu_visible)
				ShowWindow(hwnd, SW_SHOW);
			SetTimer(hwnd, 1, 33, nullptr);
			MSG msg = {};
			while (GetMessage(&msg, nullptr, 0, 0))
			{
				TranslateMessage(&msg);
				DispatchMessage(&msg);
			}
		}
		s_preview.hwnd.store(nullptr);
	}
}

void preview_init()
{
	std::unique_lock<std::mutex> lock(s_preview.lifecycle_mutex);
	if (++s_preview.users > 1)
		return;
	s_preview.running.store(true);
	s_preview.thread_ready = false;
	s_preview.thread = std::thread(preview_thread_proc);
	s_preview.lifecycle_cv.wait(lock, [] { return s_preview.thread_ready; });
}

void preview_shutdown()
{
	std::thread thread;
	{
		std::lock_guard<std::mutex> lock(s_preview.lifecycle_mutex);
		if (s_preview.users == 0 || --s_preview.users != 0)
			return;
		s_preview.running.store(false);
		if (const HWND hwnd = s_preview.hwnd.load())
			PostMessage(hwnd, WM_CLOSE, 0, 0);
		thread = std::move(s_preview.thread);
	}
	if (thread.joinable())
		thread.join();
	{
		std::lock_guard<std::mutex> lock(s_preview.mutex);
		s_preview.pixels.clear();
		s_preview.back_pixels.clear();
		s_preview.width = 0;
		s_preview.height = 0;
	}
	{
		std::lock_guard<std::mutex> lock(s_preview.keyboard_mutex);
		s_preview.characters.clear();
		s_preview.backspace = s_preview.tab = s_preview.enter = false;
	}
	s_preview.applied_headless = UINT32_MAX;
	s_preview.applied_always_on_top = UINT32_MAX;
	s_preview.applied_visible = UINT32_MAX;
	s_preview.applied_edit_mode = UINT32_MAX;
}

bool reshade::openxr::preview_get_mouse_state(float *x, float *y, bool buttons[3], float *wheel)
{
	std::lock_guard<std::mutex> lock(s_preview.mouse_mutex);
	if (!s_preview.mouse_in_window)
		return false;
	*x = s_preview.mouse_x;
	*y = s_preview.mouse_y;
	buttons[0] = s_preview.mouse_buttons[0];
	buttons[1] = s_preview.mouse_buttons[1];
	buttons[2] = s_preview.mouse_buttons[2];
	*wheel = s_preview.mouse_wheel;
	s_preview.mouse_wheel = 0.0f; // consume
	return true;
}

bool reshade::openxr::preview_consume_keyboard_input(uint16_t *characters, const size_t capacity, size_t *count,
	bool *backspace, bool *tab, bool *enter)
{
	std::lock_guard<std::mutex> lock(s_preview.keyboard_mutex);
	const size_t copied = (std::min)(capacity, s_preview.characters.size());
	for (size_t i = 0; i < copied; ++i)
		characters[i] = s_preview.characters[i];
	*count = copied;
	*backspace = s_preview.backspace;
	*tab = s_preview.tab;
	*enter = s_preview.enter;
	const bool had_input = copied != 0 || *backspace || *tab || *enter;
	s_preview.characters.erase(s_preview.characters.begin(), s_preview.characters.begin() + copied);
	s_preview.backspace = s_preview.tab = s_preview.enter = false;
	return had_input;
}

bool reshade::openxr::preview_consume_edit_delta(
	float *lmb_dx, float *lmb_dy,
	float *rmb_dx, float *rmb_dy,
	float *mmb_dx, float *mmb_dy,
	float *wheel)
{
	if (get_edit_mode() == 0)
		return false;
	std::lock_guard<std::mutex> lock(s_preview.mouse_mutex);
	*lmb_dx = s_preview.lmb_dx; *lmb_dy = s_preview.lmb_dy;
	*rmb_dx = s_preview.rmb_dx; *rmb_dy = s_preview.rmb_dy;
	*mmb_dx = s_preview.mmb_dx; *mmb_dy = s_preview.mmb_dy;
	*wheel  = s_preview.mouse_wheel;
	s_preview.lmb_dx = s_preview.lmb_dy = 0.0f;
	s_preview.rmb_dx = s_preview.rmb_dy = 0.0f;
	s_preview.mmb_dx = s_preview.mmb_dy = 0.0f;
	s_preview.mouse_wheel = 0.0f;
	return true;
}

void reshade::openxr::preview_update(const void *rgba_pixels, int width, int height, int row_pitch)
{
	const HWND hwnd = s_preview.hwnd.load();
	if (!hwnd)
		return;

	s_preview.back_pixels.resize(static_cast<size_t>(width) * height * 4);
	const uint8_t *src = static_cast<const uint8_t *>(rgba_pixels);
	uint8_t *dst = s_preview.back_pixels.data();
	for (int y = 0; y < height; y++)
	{
		// StretchDIBits BI_RGB expects BGRA; GPU texture is RGBA — swap R and B
		for (int x = 0; x < width; x++)
		{
			dst[x * 4 + 0] = src[x * 4 + 2]; // B <- R
			dst[x * 4 + 1] = src[x * 4 + 1]; // G
			dst[x * 4 + 2] = src[x * 4 + 0]; // R <- B
			dst[x * 4 + 3] = src[x * 4 + 3]; // A
		}
		src += row_pitch;
		dst += width * 4;
	}
	{
		std::lock_guard<std::mutex> lock(s_preview.mutex);
		s_preview.pixels.swap(s_preview.back_pixels);
		s_preview.width = width;
		s_preview.height = height;
	}
	PostMessage(hwnd, WM_PREVIEW_FRAME, 0, 0);
}

bool reshade::openxr::preview_is_visible()
{
	const HWND hwnd = s_preview.hwnd.load();
	return hwnd && IsWindowVisible(hwnd) && !IsIconic(hwnd);
}

// ── XR control block ─────────────────────────────────────────────────────────

void reshade::openxr::control_init()
{
	if (s_ctrl_mapping)
		return;
	s_ctrl_mapping = CreateFileMappingW(INVALID_HANDLE_VALUE, nullptr, PAGE_READWRITE,
		0, sizeof(XRControlBlock), XR_CONTROL_SHMEM_NAME);
	if (!s_ctrl_mapping)
		return;
	const bool fresh = (GetLastError() != ERROR_ALREADY_EXISTS);
	s_ctrl = static_cast<XRControlBlock *>(
		MapViewOfFile(s_ctrl_mapping, FILE_MAP_ALL_ACCESS, 0, 0, sizeof(XRControlBlock)));
	if (s_ctrl && fresh && s_ctrl->version == 0)
	{
		// First initialiser: Gameplay mode, quad NaN signals "use built-in defaults"
		s_ctrl->version            = 1;
		s_ctrl->size               = sizeof(XRControlBlock);
		s_ctrl->xr_mode            = XR_MODE_GAMEPLAY;
		s_ctrl->revision           = 0;
		s_ctrl->win_headless       = 0;
		s_ctrl->win_always_on_top  = 0;
		s_ctrl->win_snap_cursor    = 0;
		s_ctrl->menu_visible       = 1;
		s_ctrl->quad_edit_mode     = 0;
		s_ctrl->heartbeat          = 0;
		s_ctrl->quad_pos_x         = 0.0f;
		s_ctrl->quad_pos_y         = 0.0f;
		s_ctrl->quad_pos_z         = std::numeric_limits<float>::quiet_NaN(); // use session defaults
		s_ctrl->quad_quat_x        = 0.0f;
		s_ctrl->quad_quat_y        = 0.0f;
		s_ctrl->quad_quat_z        = 0.0f;
		s_ctrl->quad_quat_w        = 1.0f;
		s_ctrl->quad_width         = 0.32f;
		s_ctrl->quad_height        = 0.30f;
		s_ctrl->quad_alpha         = 1.0f;
	}
}

void reshade::openxr::control_shutdown()
{
	if (s_ctrl)         { UnmapViewOfFile(s_ctrl);     s_ctrl = nullptr; }
	if (s_ctrl_mapping) { CloseHandle(s_ctrl_mapping); s_ctrl_mapping = nullptr; }
}

bool reshade::openxr::control_is_tuning()
{
	if (!s_ctrl || s_ctrl->version != 1 || s_ctrl->size != sizeof(XRControlBlock))
		return false;
	return s_ctrl->xr_mode == XR_MODE_TUNING;
}

reshade::openxr::XRControlBlock* reshade::openxr::control_block()
{
	if (!s_ctrl || s_ctrl->version != 1 || s_ctrl->size != sizeof(XRControlBlock))
		return nullptr;
	return s_ctrl;
}
