/*
 * Copyright (C) 2021 Patrick Mours
 * SPDX-License-Identifier: BSD-3-Clause
 */

#if RESHADE_GUI

#include "runtime.hpp"
#include "version.h"
#include "dll_log.hpp"
#include "dll_resources.hpp"
#include "imgui_widgets.hpp"
#include "localization.hpp"
#include "addon_manager.hpp"
#include "vulkan/vulkan_impl_device.hpp"
#include "openxr/openxr_overlay.hpp"
#include <openvr.h>
#include <stb_image.h>

static vr::VROverlayHandle_t s_vr_overlay_handle = vr::k_ulOverlayHandleInvalid;
static vr::VROverlayHandle_t s_vr_thumbnail_handle = vr::k_ulOverlayHandleInvalid;

constexpr uint32_t VR_OVERLAY_WIDTH  = reshade::openxr::OVERLAY_WIDTH;
constexpr uint32_t VR_OVERLAY_HEIGHT = reshade::openxr::OVERLAY_HEIGHT;

bool reshade::runtime::init_gui_vr()
{
	if (s_vr_overlay_handle != vr::k_ulOverlayHandleInvalid)
		return true;

	// OpenComposite aborts the process when ReShade requests the OpenVR dashboard overlay interface
	// (IVROverlay_028) that it does not implement. Skip the OpenVR overlay entirely under OpenComposite;
	// the in-HMD menu is rendered through the OpenXR quad-layer path instead.
	extern bool reshade_is_opencomposite();
	if (reshade_is_opencomposite())
	{
		log::message(log::level::info, "Skipping OpenVR dashboard overlay because OpenComposite is active (using OpenXR overlay path).");
		return true; // Do not prevent effect runtime from initializing
	}

	if (vr::VROverlay() == nullptr)
	{
		log::message(log::level::error, "Failed to create VR dashboard overlay because SteamVR is not loaded!");
		return true; // Do not prevent effect runtime from initializing
	}

	const vr::EVROverlayError overlay_e = vr::VROverlay()->CreateDashboardOverlay("reshade", "ReShade " VERSION_STRING_PRODUCT, &s_vr_overlay_handle, &s_vr_thumbnail_handle);
	if (overlay_e != vr::VROverlayError_None)
	{
		log::message(log::level::error, "Failed to create VR dashboard overlay with error code %d!", static_cast<int>(overlay_e));
		return false;
	}

	if (s_vr_thumbnail_handle != vr::k_ulOverlayHandleInvalid)
	{
		const resources::data_resource icon = resources::load_data_resource(IDB_MAIN_ICON);

		int width = 0, height = 0, channels = 0;
		stbi_uc *const icon_data = stbi_load_from_memory(static_cast<const stbi_uc *>(icon.data), static_cast<int>(icon.data_size), &width, &height, &channels, 0);
		vr::VROverlay()->SetOverlayRaw(s_vr_thumbnail_handle, icon_data, width, height, channels);
		stbi_image_free(icon_data);
	}

	const vr::HmdVector2_t window_size = { static_cast<float>(VR_OVERLAY_WIDTH), static_cast<float>(VR_OVERLAY_HEIGHT) };
	vr::VROverlay()->SetOverlayMouseScale(s_vr_overlay_handle, &window_size);
	vr::VROverlay()->SetOverlayInputMethod(s_vr_overlay_handle, vr::VROverlayInputMethod_Mouse);

	vr::VROverlay()->SetOverlayFlag(s_vr_overlay_handle, vr::VROverlayFlags_SendVRSmoothScrollEvents, true);

	vr::VROverlay()->SetOverlayWidthInMeters(s_vr_overlay_handle, 1.5f);

	if (!_device->create_resource(api::resource_desc(VR_OVERLAY_WIDTH, VR_OVERLAY_HEIGHT, 1, 1, api::format::r8g8b8a8_unorm, 1, api::memory_heap::gpu_only, api::resource_usage::render_target | api::resource_usage::copy_source), nullptr, api::resource_usage::copy_source, &_vr_overlay_tex))
	{
		log::message(log::level::error, "Failed to create VR dashboard overlay texture!");
		return false;
	}
	if (!_device->create_resource_view(_vr_overlay_tex, api::resource_usage::render_target, api::resource_view_desc(api::format::r8g8b8a8_unorm), &_vr_overlay_target))
	{
		log::message(log::level::error, "Failed to create VR dashboard overlay render target!");
		return false;
	}

	return true;
}

void reshade::runtime::deinit_gui_vr()
{
	if (s_vr_overlay_handle == vr::k_ulOverlayHandleInvalid)
		return;

	_device->destroy_resource(_vr_overlay_tex);
	_vr_overlay_tex = {};
	_device->destroy_resource_view(_vr_overlay_target);
	_vr_overlay_target = {};

	vr::VROverlay()->DestroyOverlay(s_vr_overlay_handle);
	s_vr_overlay_handle = vr::k_ulOverlayHandleInvalid;
	vr::VROverlay()->DestroyOverlay(s_vr_thumbnail_handle);
	s_vr_thumbnail_handle = vr::k_ulOverlayHandleInvalid;
}

void reshade::runtime::draw_gui_vr()
{
	_gather_gpu_statistics = false;

	const bool has_steamvr = s_vr_overlay_handle != vr::k_ulOverlayHandleInvalid && vr::VROverlay() != nullptr;
	reshade::api::resource oxr_tex = {};
	reshade::api::resource_view oxr_target = {};
	const bool has_openxr = reshade::openxr::get_active_overlay_resources(&oxr_tex, &oxr_target);

	if (!has_steamvr && !has_openxr)
		return;

	build_font_atlas();

	ImGuiContext *const backup_context = ImGui::GetCurrentContext();
	ImGui::SetCurrentContext(_imgui_context);

	ImGuiIO &imgui_io = ImGui::GetIO();
	imgui_io.DeltaTime = _last_frame_duration.count() * 1e-9f;
	imgui_io.DisplaySize.x = static_cast<float>(VR_OVERLAY_WIDTH);
	imgui_io.DisplaySize.y = static_cast<float>(VR_OVERLAY_HEIGHT);

	imgui_io.AddKeyEvent(ImGuiKey_Backspace, false);
	imgui_io.AddKeyEvent(ImGuiKey_Tab, false);
	imgui_io.AddKeyEvent(ImGuiKey_Enter, false);

	imgui_io.MouseDrawCursor = true;

	// Feed desktop preview window mouse input
	if (has_openxr)
	{
		float mx, my, mwheel;
		bool mbuttons[3];
		if (reshade::openxr::preview_get_mouse_state(&mx, &my, mbuttons, &mwheel))
		{
			imgui_io.AddMousePosEvent(mx, my);
			imgui_io.AddMouseButtonEvent(ImGuiMouseButton_Left,   mbuttons[0]);
			imgui_io.AddMouseButtonEvent(ImGuiMouseButton_Right,  mbuttons[1]);
			imgui_io.AddMouseButtonEvent(ImGuiMouseButton_Middle, mbuttons[2]);
			if (mwheel != 0.0f)
				imgui_io.AddMouseWheelEvent(0.0f, mwheel);
		}
		else
		{
			imgui_io.AddMousePosEvent(-FLT_MAX, -FLT_MAX);
		}

		uint16_t characters[64];
		size_t character_count = 0;
		bool backspace = false, tab = false, enter = false;
		if (reshade::openxr::preview_consume_keyboard_input(characters, 64, &character_count, &backspace, &tab, &enter))
		{
			if (backspace) imgui_io.AddKeyEvent(ImGuiKey_Backspace, true);
			if (tab) imgui_io.AddKeyEvent(ImGuiKey_Tab, true);
			if (enter) imgui_io.AddKeyEvent(ImGuiKey_Enter, true);
			for (size_t i = 0; i < character_count; ++i)
				imgui_io.AddInputCharacterUTF16(characters[i]);
		}
	}

	bool keyboard_closed = false;

	if (has_steamvr)
	{
		vr::VREvent_t ev;
		while (vr::VROverlay()->PollNextOverlayEvent(s_vr_overlay_handle, &ev, sizeof(ev)))
		{
			switch (ev.eventType)
			{
			case vr::VREvent_MouseMove:
				imgui_io.AddMousePosEvent(
					ev.data.mouse.x,
					(_renderer_id & 0x10000) != 0 ? ev.data.mouse.y : VR_OVERLAY_HEIGHT - ev.data.mouse.y);
				break;
			case vr::VREvent_MouseButtonDown:
				switch (ev.data.mouse.button)
				{
				case vr::VRMouseButton_Left:
					imgui_io.AddMouseButtonEvent(ImGuiMouseButton_Left, true);
					break;
				case vr::VRMouseButton_Right:
					imgui_io.AddMouseButtonEvent(ImGuiMouseButton_Right, true);
					break;
				case vr::VRMouseButton_Middle:
					imgui_io.AddMouseButtonEvent(ImGuiMouseButton_Middle, true);
					break;
				}
				break;
			case vr::VREvent_MouseButtonUp:
				switch (ev.data.mouse.button)
				{
				case vr::VRMouseButton_Left:
					imgui_io.AddMouseButtonEvent(ImGuiMouseButton_Left, false);
					break;
				case vr::VRMouseButton_Right:
					imgui_io.AddMouseButtonEvent(ImGuiMouseButton_Right, false);
					break;
				case vr::VRMouseButton_Middle:
					imgui_io.AddMouseButtonEvent(ImGuiMouseButton_Middle, false);
					break;
				}
				break;
			case vr::VREvent_ScrollSmooth:
				imgui_io.AddMouseWheelEvent(ev.data.scroll.xdelta, ev.data.scroll.ydelta);
				break;
			case vr::VREvent_KeyboardClosed:
				ImGui::ClearActiveID();
				keyboard_closed = true;
				break;
			case vr::VREvent_KeyboardCharInput:
				if (ev.data.keyboard.cNewInput[0] == '\b')
					imgui_io.AddKeyEvent(ImGuiKey_Backspace, true);
				if (ev.data.keyboard.cNewInput[0] == '\t')
					imgui_io.AddKeyEvent(ImGuiKey_Tab, true);
				if (ev.data.keyboard.cNewInput[0] == '\n')
					imgui_io.AddKeyEvent(ImGuiKey_Enter, true);
				imgui_io.AddInputCharactersUTF8(ev.data.keyboard.cNewInput);
				break;
			}
		}
	}

	ImGui::NewFrame();

	if (has_steamvr)
	{
		vr::VROverlay()->SetOverlayAlpha(s_vr_overlay_handle, ImGui::GetStyle().Alpha);

		if (imgui_io.WantTextInput && !keyboard_closed)
		{
			vr::VROverlay()->ShowKeyboardForOverlay(s_vr_overlay_handle, vr::k_EGamepadTextInputModeNormal, vr::k_EGamepadTextInputLineModeSingleLine, vr::KeyboardFlag_Minimal, nullptr, 0, "", 0);
			// Avoid keyboard showing up behind dashboard overlay
			vr::VROverlay()->SetKeyboardPositionForOverlay(s_vr_overlay_handle, vr::HmdRect2_t { { 0.0f, 1.0f }, { 1.0f, 0.0f } });
		}
	}

#if RESHADE_LOCALIZATION
	const std::string prev_language = resources::set_current_language(_selected_language);
	_current_language = resources::get_current_language();
#endif

	const std::pair<std::string, void(runtime::*)()> overlay_callbacks[] = {
		{ _("Home###home"), &runtime::draw_gui_home },
#if RESHADE_ADDON
		{ _("Add-ons###addons"), &runtime::draw_gui_addons },
#endif
		{ _("Settings###settings"), &runtime::draw_gui_settings },
		{ _("Statistics###statistics"), &runtime::draw_gui_statistics },
		{ _("Log###log"), &runtime::draw_gui_log },
		{ _("About###about"), &runtime::draw_gui_about },
		{ "Overlay###vr_overlay_settings", &runtime::draw_gui_vr_overlay_settings }
	};

	const ImGuiViewport *const viewport = ImGui::GetMainViewport();

	ImGui::SetNextWindowPos(viewport->Pos);
	ImGui::SetNextWindowSize(viewport->Size);
	ImGui::SetNextWindowViewport(viewport->ID);
	ImGui::Begin("VR Viewport", nullptr,
		ImGuiWindowFlags_NoDecoration |
		ImGuiWindowFlags_NoMove |
		ImGuiWindowFlags_NoDocking);

	int overlay_index = 0;
	int selected_overlay_index = ImGui::GetStateStorage()->GetInt(ImGui::GetID("##overlay_index"), 0);

	ImGui::BeginChild("##overlay", ImVec2(0, ImGui::GetFrameHeight()), false, ImGuiWindowFlags_NoScrollbar);
	ImGui::PushStyleVar(ImGuiStyleVar_FramePadding, ImVec2(_imgui_context->Style.FramePadding.x, 0));

	for (const std::pair<std::string, void(runtime:: *)()> &widget : overlay_callbacks)
	{
		if (bool state = (overlay_index == selected_overlay_index);
			imgui::toggle_button(widget.first.c_str(), state, 0.0f, ImGuiButtonFlags_AlignTextBaseLine))
			selected_overlay_index = overlay_index;
		ImGui::SameLine();

		++overlay_index;
	}

#if RESHADE_ADDON == 1
	if (addon_enabled)
#endif
#if RESHADE_ADDON
	{
		for (const addon_info &info : addon_loaded_info)
		{
			for (const addon_info::overlay_callback &widget : info.overlay_callbacks)
			{
				if (bool state = (overlay_index == selected_overlay_index);
					imgui::toggle_button(widget.title.c_str(), state, 0.0f, ImGuiButtonFlags_AlignTextBaseLine))
					selected_overlay_index = overlay_index;
				ImGui::SameLine();

				++overlay_index;
			}
		}
	}
#endif

	ImGui::PopStyleVar();
	ImGui::EndChild();

	if (selected_overlay_index < static_cast<int>(std::size(overlay_callbacks)))
	{
		(this->*overlay_callbacks[selected_overlay_index].second)();
	}
#if RESHADE_ADDON
	else if (selected_overlay_index < overlay_index)
	{
#if RESHADE_ADDON == 1
		assert(addon_enabled);
#endif

		overlay_index = static_cast<int>(std::size(overlay_callbacks));

		for (const addon_info &info : addon_loaded_info)
		{
			for (const addon_info::overlay_callback &widget : info.overlay_callbacks)
			{
				if (selected_overlay_index == overlay_index++)
				{
					widget.callback(this);
					break;
				}
			}
		}
	}
#endif
	else
	{
		selected_overlay_index = 0;
	}

	ImGui::GetStateStorage()->SetInt(ImGui::GetID("##overlay_index"), selected_overlay_index);

	ImGui::End(); // VR Viewport window

	// Draw edit-mode border on VR overlay
	if (has_openxr)
	{
		const reshade::openxr::XRControlBlock *ctrl = reshade::openxr::control_block();
		if (ctrl && ctrl->version == 1 && ctrl->quad_edit_mode != 0)
		{
			ImU32 col = (ctrl->quad_edit_mode == 1)
				? IM_COL32(0, 120, 255, 200)
				: IM_COL32(255, 140, 0, 200);
			ImDrawList *dl = ImGui::GetForegroundDrawList();
			const ImVec2 sz = imgui_io.DisplaySize;
			const float t = 4.0f;
			dl->AddRect(ImVec2(0, 0), ImVec2(sz.x, sz.y), col, 0.0f, 0, t);
			dl->AddRect(ImVec2(t, t), ImVec2(sz.x - t, sz.y - t), col, 0.0f, 0, t);
		}
	}

#if RESHADE_LOCALIZATION
	resources::set_current_language(prev_language);
#endif

	ImGui::Render();

	ImDrawData *const draw_data = ImGui::GetDrawData();
	const bool has_draw_data = draw_data != nullptr && draw_data->CmdListsCount != 0 && draw_data->TotalVtxCount != 0;

	if (has_draw_data && has_steamvr)
	{
		api::command_list *const cmd_list = _graphics_queue->get_immediate_command_list();
		cmd_list->barrier(_vr_overlay_tex, api::resource_usage::copy_source, api::resource_usage::render_target);
		render_imgui_draw_data(cmd_list, draw_data, _vr_overlay_target);
		cmd_list->barrier(_vr_overlay_tex, api::resource_usage::render_target, api::resource_usage::copy_source);
	}

	if (has_openxr && oxr_tex.handle != 0)
	{
		api::command_list *const cmd_list = _graphics_queue->get_immediate_command_list();
		cmd_list->barrier(oxr_tex, api::resource_usage::copy_source, api::resource_usage::render_target);
		const float clear_color[4] = { 0.0f, 0.0f, 0.0f, 0.0f };
		cmd_list->clear_render_target_view(oxr_target, clear_color);
		if (has_draw_data)
			render_imgui_draw_data(cmd_list, draw_data, oxr_target);
		cmd_list->barrier(oxr_tex, api::resource_usage::render_target, api::resource_usage::copy_source);
	}

	ImGui::SetCurrentContext(backup_context);

	if (has_steamvr)
	{
		vr::Texture_t texture = {};
		texture.eColorSpace = vr::ColorSpace_Auto;
		union
		{
			vr::D3D12TextureData_t d3d12;
			vr::VRVulkanTextureData_t vulkan;
		} texture_data;

		switch (_device->get_api())
		{
		case api::device_api::d3d10:
		case api::device_api::d3d11:
			texture.handle = reinterpret_cast<void *>(_vr_overlay_tex.handle);
			texture.eType = vr::TextureType_DirectX;
			break;
		case api::device_api::d3d12:
			texture_data.d3d12.m_pResource = reinterpret_cast<ID3D12Resource *>(_vr_overlay_tex.handle);
			texture_data.d3d12.m_pCommandQueue = reinterpret_cast<ID3D12CommandQueue *>(_graphics_queue->get_native());
			texture_data.d3d12.m_nNodeMask = 0;
			texture.handle = &texture_data.d3d12;
			texture.eType = vr::TextureType_DirectX12;
			break;
		case api::device_api::opengl:
			texture.handle = reinterpret_cast<void *>(_vr_overlay_tex.handle & 0xFFFFFFFF);
			texture.eType = vr::TextureType_OpenGL;
			break;
		case api::device_api::vulkan:
			texture_data.vulkan.m_nImage = _vr_overlay_tex.handle;
			texture_data.vulkan.m_pDevice = reinterpret_cast<VkDevice_T *>(_device->get_native());
			texture_data.vulkan.m_pPhysicalDevice = static_cast<vulkan::device_impl *>(_device)->_physical_device;
			texture_data.vulkan.m_pInstance = VK_NULL_HANDLE;
			texture_data.vulkan.m_pQueue = reinterpret_cast<VkQueue_T *>(_graphics_queue->get_native());
			texture_data.vulkan.m_nQueueFamilyIndex = static_cast<vulkan::device_impl *>(_device)->_primary_graphics_queue_family_index;
			texture_data.vulkan.m_nWidth = VR_OVERLAY_WIDTH;
			texture_data.vulkan.m_nHeight = VR_OVERLAY_HEIGHT;
			texture_data.vulkan.m_nFormat = VK_FORMAT_R8G8B8A8_UNORM;
			texture_data.vulkan.m_nSampleCount = 1;
			texture.handle = &texture_data.vulkan;
			texture.eType = vr::TextureType_Vulkan;
			break;
		}

		vr::VROverlay()->SetOverlayTexture(s_vr_overlay_handle, &texture);
	}
}

void reshade::runtime::draw_gui_vr_overlay_settings()
{
	reshade::openxr::overlay_transform t = {};
	if (!reshade::openxr::get_overlay_transform_active(&t))
	{
		ImGui::TextDisabled("OpenXR overlay not initialized.");
		return;
	}

	bool changed = false;

	ImGui::SeparatorText("VR Quad (HMD)");
	changed |= ImGui::SliderFloat("Width (m)##vr_w",    &t.quad_width,  0.1f, 3.0f, "%.2f");
	changed |= ImGui::SliderFloat("Height (m)##vr_h",   &t.quad_height, 0.1f, 2.0f, "%.2f");
	changed |= ImGui::SliderFloat("Distance (m)##vr_d", &t.distance,    0.3f, 5.0f, "%.2f");
	changed |= ImGui::SliderFloat("Offset X (m)##vr_x", &t.offset_x,   -2.0f, 2.0f, "%.2f");
	changed |= ImGui::SliderFloat("Offset Y (m)##vr_y", &t.offset_y,   -2.0f, 2.0f, "%.2f");
	if (ImGui::Button("Reset##vr_reset"))
	{
		t = {};
		changed = true;
	}

	if (changed)
		reshade::openxr::set_overlay_transform_active(t);

	ImGui::Spacing();
	ImGui::SeparatorText("Desktop Preview Window");
	ImGui::TextWrapped("Resize the 'ReShade VR Overlay' window freely — it locks to the correct 4:3 aspect ratio automatically.");
}

#endif
