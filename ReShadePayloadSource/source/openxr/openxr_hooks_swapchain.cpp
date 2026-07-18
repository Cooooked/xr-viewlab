/*
 * Copyright (C) 2023 Patrick Mours
 * SPDX-License-Identifier: BSD-3-Clause OR MIT
 */

#include "openxr_hooks.hpp"
#include "openxr_overlay.hpp"
#include "openxr_impl_swapchain.hpp"
#include "d3d11/d3d11_device.hpp"
#include "d3d11/d3d11_device_context.hpp"
#include "d3d12/d3d12_device.hpp"
#include "d3d12/d3d12_command_queue.hpp"
#include "opengl/opengl_impl_device_context.hpp"
#include "opengl/opengl_impl_type_convert.hpp"
#include "vulkan/vulkan_hooks.hpp"
#include "vulkan/vulkan_impl_device.hpp"
#include "vulkan/vulkan_impl_command_queue.hpp"
#include "dll_log.hpp"
#include "com_utils.hpp"
#include "hook_manager.hpp"
#include "lockfree_linear_map.hpp"
#include <deque>
#include <vector>
#include <algorithm> // std::find, std::find_if
#include <cmath>
#include <cwchar>

// ── Global quad transform (persisted to disk, ViewLab becomes owner later) ──
static constexpr wchar_t QUAD_TRANSFORM_FILE[]    = L"C:\\ProgramData\\ReShade\\openxr_quad_transform.ini";
static constexpr wchar_t QUAD_TRANSFORM_SECTION[] = L"Transform";

struct quad_transform_t
{
	float pos_x  = -0.32f,  pos_y  = -0.16f,  pos_z  = 0.81f;   // stage-local metres
	float quat_x = -0.0778f, quat_y = 0.1301f, quat_z = 0.0102f, quat_w = 0.9884f;
	float width  =  0.32f,  height =  0.30f;
	float alpha  =  1.0f;
};
static quad_transform_t s_quad_xform;

static float ini_get(const wchar_t *key, float def)
{
	wchar_t buf[32] = {};
	GetPrivateProfileStringW(QUAD_TRANSFORM_SECTION, key, L"", buf, 32, QUAD_TRANSFORM_FILE);
	if (buf[0] == L'\0') return def;
	return static_cast<float>(_wtof(buf));
}
static void ini_set(const wchar_t *key, float v)
{
	wchar_t buf[32];
	swprintf_s(buf, L"%.6f", (double)v);
	WritePrivateProfileStringW(QUAD_TRANSFORM_SECTION, key, buf, QUAD_TRANSFORM_FILE);
}

static void quad_transform_load()
{
	s_quad_xform.pos_x  = ini_get(L"pos_x",  s_quad_xform.pos_x);
	s_quad_xform.pos_y  = ini_get(L"pos_y",  s_quad_xform.pos_y);
	s_quad_xform.pos_z  = ini_get(L"pos_z",  s_quad_xform.pos_z);
	s_quad_xform.quat_x = ini_get(L"quat_x", s_quad_xform.quat_x);
	s_quad_xform.quat_y = ini_get(L"quat_y", s_quad_xform.quat_y);
	s_quad_xform.quat_z = ini_get(L"quat_z", s_quad_xform.quat_z);
	s_quad_xform.quat_w = ini_get(L"quat_w", s_quad_xform.quat_w);
	s_quad_xform.width  = ini_get(L"width",  s_quad_xform.width);
	s_quad_xform.height = ini_get(L"height", s_quad_xform.height);
	s_quad_xform.alpha  = ini_get(L"alpha",  s_quad_xform.alpha);
}
void reshade::openxr::quad_transform_persist()
{
	ini_set(L"pos_x",  s_quad_xform.pos_x);
	ini_set(L"pos_y",  s_quad_xform.pos_y);
	ini_set(L"pos_z",  s_quad_xform.pos_z);
	ini_set(L"quat_x", s_quad_xform.quat_x);
	ini_set(L"quat_y", s_quad_xform.quat_y);
	ini_set(L"quat_z", s_quad_xform.quat_z);
	ini_set(L"quat_w", s_quad_xform.quat_w);
	ini_set(L"width",  s_quad_xform.width);
	ini_set(L"height", s_quad_xform.height);
	ini_set(L"alpha",  s_quad_xform.alpha);
}

#define XR_USE_GRAPHICS_API_D3D11
#define XR_USE_GRAPHICS_API_D3D12
#define XR_USE_GRAPHICS_API_OPENGL
#define XR_USE_GRAPHICS_API_VULKAN
#include <openxr/openxr_platform.h>

struct openxr_session
{
	const XrGeneratedDispatchTable *dispatch_table = nullptr;
	reshade::openxr::swapchain_impl *swapchain_impl = nullptr;
	XrSwapchain overlay_swapchain = XR_NULL_HANDLE;
	XrSpace overlay_space = XR_NULL_HANDLE;
	reshade::api::resource overlay_texture = {};
	reshade::api::resource_view overlay_target = {};
	reshade::api::resource staging_textures[2] = {};
	uint32_t staging_write_index = 0;
	uint32_t staging_read_index = 0;
	bool staging_has_pending_frame = false;
	bool overlay_visible = false;
	// Default position matches OKB Kneeboard 8
	float overlay_quad_width  = 0.32f;
	float overlay_quad_height = 0.30f;
	float overlay_distance    = 0.81f;
	float overlay_offset_x    = -0.32f;
	float overlay_offset_y    = -0.16f;
	// Orientation from OKB Kneeboard 8 (RX=-0.1571, RY=0.2618 Euler → quaternion)
	float overlay_quat_x = -0.0778f;
	float overlay_quat_y =  0.1301f;
	float overlay_quat_z =  0.0102f;
	float overlay_quat_w =  0.9884f;
};
struct openxr_swapchain
{
	const XrGeneratedDispatchTable *dispatch_table = nullptr;
	XrSwapchainCreateFlags create_flags = 0;
	std::vector<reshade::api::resource> surface_images;
	std::deque<uint32_t> acquired_index;
	uint32_t last_released_index = 0;
};

static lockfree_linear_map<XrSession, openxr_session, 16> s_openxr_sessions;
static XrSession s_active_session = XR_NULL_HANDLE;
extern lockfree_linear_map<XrInstance, openxr_instance, 16> g_openxr_instances;
static lockfree_linear_map<XrSwapchain, openxr_swapchain, 16> s_openxr_swapchains;

XrResult XRAPI_CALL xrCreateSession(XrInstance instance, const XrSessionCreateInfo *pCreateInfo, XrSession *pSession)
{
	reshade::log::message(reshade::log::level::info, "Redirecting xrCreateSession(instance = %" PRIx64 ", pCreateInfo = %p, pSession = %p) ...", instance, pCreateInfo, pSession);

	assert(pCreateInfo != nullptr && pSession != nullptr);

	const XrGeneratedDispatchTable &dispatch_table = g_openxr_instances.at(instance).dispatch_table;
	RESHADE_OPENXR_GET_DISPATCH_PTR_FROM(CreateSession, &dispatch_table);

	const XrResult result = trampoline(instance, pCreateInfo, pSession);
	if (XR_FAILED(result))
	{
		reshade::log::message(reshade::log::level::warning, "xrCreateSession failed with error code %d.", static_cast<int>(result));
		return result;
	}

	auto enum_view_configurations = dispatch_table.EnumerateViewConfigurations;
	assert(enum_view_configurations != nullptr);

	uint32_t num_view_configurations = 0;
	enum_view_configurations(instance, pCreateInfo->systemId, num_view_configurations, &num_view_configurations, nullptr);
	std::vector<XrViewConfigurationType> view_configurations(num_view_configurations);
	enum_view_configurations(instance, pCreateInfo->systemId, num_view_configurations, &num_view_configurations, view_configurations.data());

	reshade::openxr::swapchain_impl *swapchain_impl = nullptr;

	if (std::find(view_configurations.begin(), view_configurations.end(), XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO) != view_configurations.end())
	{
		// Initialize OpenVR hooks, in case this OpenXR instance is using the SteamVR runtime, so that the VR dashboard overlay can be added
		extern void check_and_init_openvr_hooks();
		check_and_init_openvr_hooks();

		if (const auto binding_d3d11 = find_in_structure_chain<XrGraphicsBindingD3D11KHR>(pCreateInfo, XR_TYPE_GRAPHICS_BINDING_D3D11_KHR))
		{
			if (const auto device_proxy = get_private_pointer_d3dx<D3D11Device>(binding_d3d11->device))
			{
				swapchain_impl = new reshade::openxr::swapchain_impl(device_proxy, device_proxy->_immediate_context, *pSession);
			}
			else
			{
				reshade::log::message(reshade::log::level::warning, "Skipping OpenXR session because it was created without a proxy Direct3D 11 device.");
			}
		}
		else
		if (const auto binding_d3d12 = find_in_structure_chain<XrGraphicsBindingD3D12KHR>(pCreateInfo, XR_TYPE_GRAPHICS_BINDING_D3D12_KHR))
		{
			if (const auto device_proxy = get_private_pointer_d3dx<D3D12Device>(binding_d3d12->device))
			{
				if (com_ptr<D3D12CommandQueue> command_queue_proxy;
					SUCCEEDED(binding_d3d12->queue->QueryInterface(&command_queue_proxy)))
				{
					swapchain_impl = new reshade::openxr::swapchain_impl(device_proxy, command_queue_proxy.get(), *pSession);
				}
			}
			else
			{
				reshade::log::message(reshade::log::level::warning, "Skipping OpenXR session because it was created without a proxy Direct3D 12 device.");
			}
		}
		else
		if (const auto binding_opengl = find_in_structure_chain<XrGraphicsBindingOpenGLWin32KHR>(pCreateInfo, XR_TYPE_GRAPHICS_BINDING_OPENGL_WIN32_KHR))
		{
			extern thread_local reshade::opengl::device_context_impl *g_opengl_context;

			if (g_opengl_context != nullptr)
			{
				assert(reinterpret_cast<HGLRC>(g_opengl_context->get_native()) == binding_opengl->hGLRC);

				swapchain_impl = new reshade::openxr::swapchain_impl(g_opengl_context->get_device(), g_opengl_context, *pSession);
			}
			else
			{
				reshade::log::message(reshade::log::level::warning, "Skipping OpenXR session because it was created without an OpenGL context current.");
			}
		}
		else
		if (const auto binding_vulkan = find_in_structure_chain<XrGraphicsBindingVulkanKHR>(pCreateInfo, XR_TYPE_GRAPHICS_BINDING_VULKAN_KHR))
		{
			extern lockfree_linear_map<void *, reshade::vulkan::device_impl *, 8> g_vulkan_devices;

			if (reshade::vulkan::device_impl *const device = g_vulkan_devices.at(dispatch_key_from_handle(binding_vulkan->device)))
			{
				VkQueue queue_handle = VK_NULL_HANDLE;
				device->_dispatch_table.GetDeviceQueue(binding_vulkan->device, binding_vulkan->queueFamilyIndex, binding_vulkan->queueIndex, &queue_handle);
				assert(queue_handle != VK_NULL_HANDLE);

				if (const auto queue_it = std::find_if(device->_queues.cbegin(), device->_queues.cend(),
						[queue_handle](reshade::vulkan::command_queue_impl *queue) { return queue->_orig == queue_handle; });
					queue_it != device->_queues.cend())
				{
					swapchain_impl = new reshade::openxr::swapchain_impl(device, *queue_it, *pSession);
				}
			}
			else
			{
				reshade::log::message(reshade::log::level::warning, "Skipping OpenXR session because it was created without a known Vulkan device.");
			}
		}
	}
	else
	{
		reshade::log::message(reshade::log::level::warning, "Skipping OpenXR session because the system does not support the stereo view configuration.");
	}

	s_openxr_sessions.emplace(*pSession, openxr_session { &dispatch_table, swapchain_impl });
	s_active_session = *pSession;
	openxr_session &session_data = s_openxr_sessions.at(*pSession);

	reshade::openxr::control_init();

	// Load persisted quad transform (first launch creates defaults; ViewLab takes over later)
	quad_transform_load();
	session_data.overlay_offset_x    = s_quad_xform.pos_x;
	session_data.overlay_offset_y    = s_quad_xform.pos_y;
	session_data.overlay_distance    = s_quad_xform.pos_z;
	session_data.overlay_quat_x      = s_quad_xform.quat_x;
	session_data.overlay_quat_y      = s_quad_xform.quat_y;
	session_data.overlay_quat_z      = s_quad_xform.quat_z;
	session_data.overlay_quat_w      = s_quad_xform.quat_w;
	session_data.overlay_quad_width  = s_quad_xform.width;
	session_data.overlay_quad_height = s_quad_xform.height;
	// Expose initial transform to ViewLab via ctrl (NaN pos_z means "not set by ViewLab yet")
	if (reshade::openxr::XRControlBlock *ctrl = reshade::openxr::control_block())
	{
		if (std::isnan(ctrl->quad_pos_z))
		{
			ctrl->quad_pos_x = s_quad_xform.pos_x;  ctrl->quad_pos_y = s_quad_xform.pos_y;
			ctrl->quad_pos_z = s_quad_xform.pos_z;  // clear NaN — ViewLab can now read position
			ctrl->quad_quat_x = s_quad_xform.quat_x; ctrl->quad_quat_y = s_quad_xform.quat_y;
			ctrl->quad_quat_z = s_quad_xform.quat_z; ctrl->quad_quat_w = s_quad_xform.quat_w;
			ctrl->quad_width  = s_quad_xform.width;  ctrl->quad_height = s_quad_xform.height;
			ctrl->quad_alpha  = s_quad_xform.alpha;
		}
	}
	// Persist defaults to disk so position survives between sessions
	reshade::openxr::quad_transform_persist();

	// Create overlay swapchain + texture + desktop preview window (OKB pattern)
	if (swapchain_impl != nullptr)
	{
		reshade::api::device *const device = swapchain_impl->get_device();
		constexpr uint32_t OVW = reshade::openxr::OVERLAY_WIDTH, OVH = reshade::openxr::OVERLAY_HEIGHT;

		reshade::log::message(reshade::log::level::info, "Creating OpenXR overlay (%ux%u)...", OVW, OVH);

		// Swapchain (matches OKB D3D11: COLOR_ATTACHMENT_BIT, R8G8B8A8)
		XrSwapchainCreateInfo sci = { XR_TYPE_SWAPCHAIN_CREATE_INFO };
		sci.usageFlags = XR_SWAPCHAIN_USAGE_COLOR_ATTACHMENT_BIT;
		sci.format = 29; // DXGI_FORMAT_R8G8B8A8_UNORM_SRGB — tells compositor data is sRGB, prevents double gamma
		sci.width = OVW; sci.height = OVH;
		sci.sampleCount = 1; sci.faceCount = 1; sci.arraySize = 1; sci.mipCount = 1;
		if (XR_SUCCEEDED(xrCreateSwapchain(*pSession, &sci, &session_data.overlay_swapchain)))
			reshade::log::message(reshade::log::level::info, "Overlay swapchain created.");
		else
			reshade::log::message(reshade::log::level::warning, "Failed to create overlay swapchain.");

		if (session_data.overlay_swapchain != XR_NULL_HANDLE)
		{
			// GPU overlay texture — initial state copy_source so barrier in draw_gui_vr is always copy_source→render_target
			device->create_resource(
				reshade::api::resource_desc(OVW, OVH, 1, 1, reshade::api::format::r8g8b8a8_unorm, 1, reshade::api::memory_heap::gpu_only, reshade::api::resource_usage::render_target | reshade::api::resource_usage::copy_source | reshade::api::resource_usage::copy_dest),
				nullptr, reshade::api::resource_usage::copy_source, &session_data.overlay_texture);
			device->create_resource_view(session_data.overlay_texture, reshade::api::resource_usage::render_target,
				reshade::api::resource_view_desc(reshade::api::format::r8g8b8a8_unorm), &session_data.overlay_target);

			for (reshade::api::resource &staging_texture : session_data.staging_textures)
				device->create_resource(
					reshade::api::resource_desc(OVW, OVH, 1, 1, reshade::api::format::r8g8b8a8_unorm, 1, reshade::api::memory_heap::gpu_to_cpu, reshade::api::resource_usage::copy_dest),
					nullptr, reshade::api::resource_usage::copy_dest, &staging_texture);

			// Preview window always spawns (desktop mirror, always visible)
			extern void preview_init();
			preview_init();

			session_data.overlay_visible = false;

			// Reference space (LOCAL per OKB pattern)
			auto try_space = [&](XrReferenceSpaceType type, const char *name) {
				XrReferenceSpaceCreateInfo si = { XR_TYPE_REFERENCE_SPACE_CREATE_INFO };
				si.referenceSpaceType = type;
				si.poseInReferenceSpace.orientation = { 0,0,0,1 };
				XrSpace space = XR_NULL_HANDLE;
				if (XR_SUCCEEDED(dispatch_table.CreateReferenceSpace(*pSession, &si, &space)))
				{
					session_data.overlay_space = space;
					return true;
				}
				return false;
			};
			if (!try_space(XR_REFERENCE_SPACE_TYPE_LOCAL, "LOCAL"))
				try_space(XR_REFERENCE_SPACE_TYPE_VIEW, "VIEW");
		}
	}

#if RESHADE_VERBOSE_LOG
	reshade::log::message(reshade::log::level::debug, "Returning OpenXR session %" PRIx64 ".", *pSession);
#endif
	return result;
}

XrResult XRAPI_CALL xrDestroySession(XrSession session)
{
	reshade::log::message(reshade::log::level::info, "Redirecting xrDestroySession(session = %" PRIx64 ") ...", session);

	assert(session != XR_NULL_HANDLE);

	const openxr_session &data = s_openxr_sessions.at(session);
	RESHADE_OPENXR_GET_DISPATCH_PTR_FROM(DestroySession, data.dispatch_table);

	// Cleanup overlay resources
	if (data.overlay_space != XR_NULL_HANDLE && data.dispatch_table->DestroySpace)
		data.dispatch_table->DestroySpace(data.overlay_space);
	if (data.overlay_swapchain != XR_NULL_HANDLE)
		xrDestroySwapchain(data.overlay_swapchain);
	if (data.overlay_texture != 0 && data.swapchain_impl)
		data.swapchain_impl->get_device()->destroy_resource(data.overlay_texture);
	if (data.overlay_target != 0 && data.swapchain_impl)
		data.swapchain_impl->get_device()->destroy_resource_view(data.overlay_target);
	if (data.swapchain_impl)
		for (const reshade::api::resource staging_texture : data.staging_textures)
			if (staging_texture != 0)
				data.swapchain_impl->get_device()->destroy_resource(staging_texture);

	delete data.swapchain_impl;

	s_openxr_sessions.erase(session);

	extern void preview_shutdown();
	preview_shutdown();

	return trampoline(session);
}

XrResult XRAPI_CALL xrCreateSwapchain(XrSession session, const XrSwapchainCreateInfo *pCreateInfo, XrSwapchain *pSwapchain)
{
	reshade::log::message(reshade::log::level::info, "Redirecting xrCreateSwapchain(session = %" PRIx64 ", pCreateInfo = %p, pSwapchain = %p) ...", session, pCreateInfo, pSwapchain);

	const openxr_session &data = s_openxr_sessions.at(session);
	RESHADE_OPENXR_GET_DISPATCH_PTR_FROM(CreateSwapchain, data.dispatch_table);

	assert(pCreateInfo != nullptr && pSwapchain != nullptr);

	XrSwapchainCreateInfo create_info = *pCreateInfo;
	// Add required usage flags to create info
	create_info.usageFlags |= XR_SWAPCHAIN_USAGE_TRANSFER_SRC_BIT | XR_SWAPCHAIN_USAGE_TRANSFER_DST_BIT | XR_SWAPCHAIN_USAGE_MUTABLE_FORMAT_BIT;

	// Dump swap chain description
	{
		reshade::log::message(reshade::log::level::info, "> Dumping swap chain description:");
		reshade::log::message(reshade::log::level::info, "  +-----------------------------------------+-----------------------------------------+");
		reshade::log::message(reshade::log::level::info, "  | Parameter                               | Value                                   |");
		reshade::log::message(reshade::log::level::info, "  +-----------------------------------------+-----------------------------------------+");
		reshade::log::message(reshade::log::level::info, "  | createFlags                             |"                               " %-#39x |", static_cast<unsigned int>(create_info.createFlags));
		reshade::log::message(reshade::log::level::info, "  | usageFlags                              |"                               " %-#39x |", static_cast<unsigned int>(create_info.usageFlags));
		reshade::log::message(reshade::log::level::info, "  | format                                  |"                              " %-39lld |", create_info.format);
		reshade::log::message(reshade::log::level::info, "  | sampleCount                             |"                                " %-39u |", create_info.sampleCount);
		reshade::log::message(reshade::log::level::info, "  | width                                   |"                                " %-39u |", create_info.width);
		reshade::log::message(reshade::log::level::info, "  | height                                  |"                                " %-39u |", create_info.height);
		reshade::log::message(reshade::log::level::info, "  | faceCount                               |"                                " %-39u |", create_info.faceCount);
		reshade::log::message(reshade::log::level::info, "  | arraySize                               |"                                " %-39u |", create_info.arraySize);
		reshade::log::message(reshade::log::level::info, "  | mipCount                                |"                                " %-39u |", create_info.mipCount);
		reshade::log::message(reshade::log::level::info, "  +-----------------------------------------+-----------------------------------------+");
	}

	const XrResult result = trampoline(session, &create_info, pSwapchain);
	if (XR_FAILED(result))
	{
		reshade::log::message(reshade::log::level::warning, "xrCreateSwapchain failed with error code %d.", static_cast<int>(result));
		return result;
	}

	std::vector<reshade::api::resource> images;

	if (data.swapchain_impl != nullptr)
	{
		auto enum_swapchain_images = data.dispatch_table->EnumerateSwapchainImages;
		assert(enum_swapchain_images != nullptr);

		uint32_t num_images = 0;
		enum_swapchain_images(*pSwapchain, num_images, &num_images, nullptr);
		images.reserve(num_images);

		switch (data.swapchain_impl->get_device()->get_api())
		{
		case reshade::api::device_api::d3d11:
			{
				std::vector<XrSwapchainImageD3D11KHR> images_d3d11(num_images, { XR_TYPE_SWAPCHAIN_IMAGE_D3D11_KHR });
				enum_swapchain_images(*pSwapchain, num_images, &num_images, reinterpret_cast<XrSwapchainImageBaseHeader *>(images_d3d11.data()));
				for (const XrSwapchainImageD3D11KHR &image_d3d11 : images_d3d11)
					images.push_back({ reinterpret_cast<uintptr_t>(image_d3d11.texture) });
			}
			break;
		case reshade::api::device_api::d3d12:
			{
				std::vector<XrSwapchainImageD3D12KHR> images_d3d12(num_images, { XR_TYPE_SWAPCHAIN_IMAGE_D3D12_KHR });
				enum_swapchain_images(*pSwapchain, num_images, &num_images, reinterpret_cast<XrSwapchainImageBaseHeader *>(images_d3d12.data()));
				for (const XrSwapchainImageD3D12KHR &image_d3d12 : images_d3d12)
					images.push_back({ reinterpret_cast<uintptr_t>(image_d3d12.texture) });
			}
			break;
		case reshade::api::device_api::opengl:
			{
				std::vector<XrSwapchainImageOpenGLKHR> images_opengl(num_images, { XR_TYPE_SWAPCHAIN_IMAGE_OPENGL_KHR });
				enum_swapchain_images(*pSwapchain, num_images, &num_images, reinterpret_cast<XrSwapchainImageBaseHeader *>(images_opengl.data()));
				for (const XrSwapchainImageOpenGLKHR &image_opengl : images_opengl)
					images.push_back(reshade::opengl::make_resource_handle(GL_TEXTURE_2D, image_opengl.image));
			}
			break;
		case reshade::api::device_api::vulkan:
			{
				std::vector<XrSwapchainImageVulkanKHR> images_vulkan(num_images, { XR_TYPE_SWAPCHAIN_IMAGE_VULKAN_KHR });
				enum_swapchain_images(*pSwapchain, num_images, &num_images, reinterpret_cast<XrSwapchainImageBaseHeader *>(images_vulkan.data()));
				for (const XrSwapchainImageVulkanKHR &image_vulkan : images_vulkan)
					images.push_back({ (uint64_t)image_vulkan.image });
			}
			break;
		}
	}

	s_openxr_swapchains.emplace(*pSwapchain, openxr_swapchain { data.dispatch_table, create_info.createFlags, std::move(images) });

#if RESHADE_VERBOSE_LOG
	reshade::log::message(reshade::log::level::debug, "Returning OpenXR swap chain %" PRIx64 ".", *pSwapchain);
#endif
	return result;
}

XrResult XRAPI_CALL xrDestroySwapchain(XrSwapchain swapchain)
{
	reshade::log::message(reshade::log::level::info, "Redirecting xrDestroySwapchain(swapchain = %" PRIx64 ") ...", swapchain);

	const openxr_swapchain &data = s_openxr_swapchains.at(swapchain);
	RESHADE_OPENXR_GET_DISPATCH_PTR_FROM(DestroySwapchain, data.dispatch_table);

	s_openxr_swapchains.erase(swapchain);

	return trampoline(swapchain);
}

XrResult XRAPI_CALL xrAcquireSwapchainImage(XrSwapchain swapchain, const XrSwapchainImageAcquireInfo *pAcquireInfo, uint32_t *pIndex)
{
	openxr_swapchain &data = s_openxr_swapchains.at(swapchain);
	RESHADE_OPENXR_GET_DISPATCH_PTR_FROM(AcquireSwapchainImage, data.dispatch_table);

	const XrResult result = trampoline(swapchain, pAcquireInfo, pIndex);
	if (XR_FAILED(result))
		return result;

	data.acquired_index.push_back(*pIndex);

	return result;
}

XrResult XRAPI_CALL xrReleaseSwapchainImage(XrSwapchain swapchain, const XrSwapchainImageReleaseInfo *pReleaseInfo)
{
	openxr_swapchain &data = s_openxr_swapchains.at(swapchain);
	RESHADE_OPENXR_GET_DISPATCH_PTR_FROM(ReleaseSwapchainImage, data.dispatch_table);

	const XrResult result = trampoline(swapchain, pReleaseInfo);
	if (XR_FAILED(result))
		return result;

	// 'xrReleaseSwapchainImage' releases the last image from 'xrWaitSwapchainImage', which will implicitly wait on the oldest acquired swapchain image which has not yet been successfully waited on
	data.last_released_index = data.acquired_index.front();
	data.acquired_index.pop_front();

	return result;
}

XrResult XRAPI_CALL xrEndFrame(XrSession session, const XrFrameEndInfo *frameEndInfo)
{
	openxr_session &data = s_openxr_sessions.at(session);

	// Signal that the ReShade layer is alive. ViewLab watches heartbeat for the "Connected" status.
	if (reshade::openxr::XRControlBlock *ctrl = reshade::openxr::control_block())
		ctrl->heartbeat++;

	if (data.swapchain_impl != nullptr)
	{
		for (uint32_t layer_index = 0; layer_index < frameEndInfo->layerCount; ++layer_index)
		{
			if (frameEndInfo->layers[layer_index]->type == XR_TYPE_COMPOSITION_LAYER_PROJECTION)
			{
				const XrCompositionLayerProjection *const layer = reinterpret_cast<const XrCompositionLayerProjection *>(frameEndInfo->layers[layer_index]);

				uint32_t view_count = 0;
				temp_mem<reshade::api::resource, 2> view_textures(layer->viewCount);
				temp_mem<reshade::api::subresource_box, 2> view_boxes(layer->viewCount);
				temp_mem<uint32_t, 2> view_layers(layer->viewCount);
				const std::vector<reshade::api::resource> *swapchain_images = nullptr;
				uint32_t swap_index = 0;

				assert(layer->viewCount != 0);

				for (; view_count < layer->viewCount; ++view_count)
				{
					XrSwapchainSubImage const &sub_image = layer->views[view_count].subImage;

					const openxr_swapchain &swapchain = s_openxr_swapchains.at(sub_image.swapchain);

					if ((swapchain.create_flags & XR_SWAPCHAIN_CREATE_STATIC_IMAGE_BIT) != 0)
						break; // Cannot apply effects to a static image, since it would just stack on top of the previous result every frame

					view_textures[view_count] = swapchain.surface_images[swapchain.last_released_index];

					assert(sub_image.imageRect.offset.x >= 0 && sub_image.imageRect.offset.y >= 0 && sub_image.imageRect.extent.width >= 0 && sub_image.imageRect.extent.height >= 0);

					reshade::api::subresource_box &view_box = view_boxes[view_count];
					view_box.left = static_cast<uint32_t>(sub_image.imageRect.offset.x);
					view_box.top = static_cast<uint32_t>(sub_image.imageRect.offset.y);
					view_box.front = 0;
					view_box.right = static_cast<uint32_t>(sub_image.imageRect.offset.x + sub_image.imageRect.extent.width);
					view_box.bottom = static_cast<uint32_t>(sub_image.imageRect.offset.y + sub_image.imageRect.extent.height);
					view_box.back = 1;

					view_layers[view_count] = sub_image.imageArrayIndex;

					swapchain_images = &swapchain.surface_images;
					if (view_textures[view_count] != view_textures[0] || view_layers[view_count] != view_layers[0])
						swapchain_images = nullptr;
					swap_index = swapchain.last_released_index;
				}

				if (view_count == layer->viewCount)
				{
					data.swapchain_impl->on_present(view_count, view_textures.p, view_boxes.p, view_layers.p, swapchain_images, swap_index);
					break;
				}
			}
		}
	}

	// Overlay quad layer — fixed stack array, no heap allocation
	XrCompositionLayerQuad overlay_layer = {};
	const XrCompositionLayerBaseHeader *modified_layers[16];
	XrFrameEndInfo modified_frame_end_info = {};
	bool has_overlay = false;

	// Staging readback for desktop preview — rate-limited to ~30fps, skipped when minimized
	{
		static uint32_t s_preview_skip = 0;
		const bool do_preview = (++s_preview_skip >= 3);
		if (do_preview) s_preview_skip = 0;

		if (do_preview
			&& reshade::openxr::preview_is_visible()
			&& data.overlay_texture != 0
			&& data.staging_textures[0] != 0 && data.staging_textures[1] != 0
			&& data.swapchain_impl != nullptr)
		{
			reshade::api::device *dev = data.swapchain_impl->get_device();
			reshade::api::command_list *cl = data.swapchain_impl->get_graphics_queue()->get_immediate_command_list();

			// Read the previous capture, which has had an entire preview interval to
			// complete, rather than stalling xrEndFrame on the copy submitted below.
			if (data.staging_has_pending_frame)
			{
				reshade::api::subresource_data mapped = {};
				const reshade::api::resource readback = data.staging_textures[data.staging_read_index];
				if (dev->map_texture_region(readback, 0, nullptr, reshade::api::map_access::read_only, &mapped))
				{
					reshade::openxr::preview_update(mapped.data,
						static_cast<int>(reshade::openxr::OVERLAY_WIDTH),
						static_cast<int>(reshade::openxr::OVERLAY_HEIGHT),
						mapped.row_pitch);
					dev->unmap_texture_region(readback, 0);
				}
			}

			const uint32_t write_index = data.staging_write_index;
			cl->copy_texture_region(data.overlay_texture, 0, nullptr, data.staging_textures[write_index], 0, nullptr, reshade::api::filter_mode::min_mag_mip_point);
			data.swapchain_impl->get_graphics_queue()->flush_immediate_command_list();
			data.staging_read_index = write_index;
			data.staging_write_index ^= 1;
			data.staging_has_pending_frame = true;
		}
	}

	// Submit quad layer to HMD only when overlay is visible
	if (data.overlay_swapchain != XR_NULL_HANDLE && data.overlay_visible &&
		data.overlay_texture != 0 && data.swapchain_impl != nullptr &&
		frameEndInfo->layerCount > 0)
	{
		if (reshade::openxr::copy_overlay_to_swapchain(session))
		{
			const uint32_t n = frameEndInfo->layerCount;
			for (uint32_t i = 0; i < n; ++i)
				modified_layers[i] = frameEndInfo->layers[i];

			overlay_layer.type = XR_TYPE_COMPOSITION_LAYER_QUAD;
			overlay_layer.layerFlags = XR_COMPOSITION_LAYER_BLEND_TEXTURE_SOURCE_ALPHA_BIT;
			overlay_layer.space = data.overlay_space != XR_NULL_HANDLE ? data.overlay_space : frameEndInfo->layers[0]->space;
			overlay_layer.eyeVisibility = XR_EYE_VISIBILITY_BOTH;
			overlay_layer.subImage.swapchain = data.overlay_swapchain;
			overlay_layer.subImage.imageRect.offset = { 0, 0 };
			overlay_layer.subImage.imageRect.extent = { static_cast<int32_t>(reshade::openxr::OVERLAY_WIDTH), static_cast<int32_t>(reshade::openxr::OVERLAY_HEIGHT) };
			overlay_layer.subImage.imageArrayIndex = 0;
			// Resolve quad transform: control block takes priority over session defaults.
			// NaN quad_pos_z signals "no override" — use OKB defaults from session struct.
			reshade::openxr::XRControlBlock *ctrl = reshade::openxr::control_block();
			const bool ctrl_has_pos = ctrl && !std::isnan(ctrl->quad_pos_z);

			float pos_x = ctrl_has_pos ? ctrl->quad_pos_x  : data.overlay_offset_x;
			float pos_y = ctrl_has_pos ? ctrl->quad_pos_y  : data.overlay_offset_y;
			float pos_z = ctrl_has_pos ? ctrl->quad_pos_z  : data.overlay_distance;
			float qx    = ctrl_has_pos ? ctrl->quad_quat_x : data.overlay_quat_x;
			float qy    = ctrl_has_pos ? ctrl->quad_quat_y : data.overlay_quat_y;
			float qz    = ctrl_has_pos ? ctrl->quad_quat_z : data.overlay_quat_z;
			float qw    = ctrl_has_pos ? ctrl->quad_quat_w : data.overlay_quat_w;
			float qw2   = ctrl_has_pos ? ctrl->quad_width  : data.overlay_quad_width;
			float qh2   = ctrl_has_pos ? ctrl->quad_height : data.overlay_quad_height;

			// Apply mouse-driven edit deltas from the preview window
			{
				float ldx, ldy, rdx, rdy, mdx, mdy, wheel;
				if (reshade::openxr::preview_consume_edit_delta(&ldx, &ldy, &rdx, &rdy, &mdx, &mdy, &wheel))
				{
					const uint32_t em = ctrl ? ctrl->quad_edit_mode : 0;
					if (em == 1) // Reposition: LMB=X/Y, RMB=Z
					{
						constexpr float s_xy = 0.001f, s_z = 0.002f;
						pos_x += ldx * s_xy;
						pos_y -= ldy * s_xy; // screen-down maps to world-down → invert
						pos_z += rdy * s_z;
						if (pos_z < 0.1f) pos_z = 0.1f;
					}
					else if (em == 2) // Transform: LMB=scale, RMB=rotate Y, scroll=alpha
					{
						constexpr float s_sc = 0.001f, s_rot = 0.003f;
						qw2 = (qw2 + ldx * s_sc) < 0.05f ? 0.05f : (qw2 + ldx * s_sc);
						qh2 = (qh2 + ldy * s_sc) < 0.05f ? 0.05f : (qh2 + ldy * s_sc);
						// Rotate around world Y by rdx: multiply existing quat by (0, sin(a/2), 0, cos(a/2))
						const float a = rdx * s_rot;
						const float sa = std::sin(a * 0.5f), ca = std::cos(a * 0.5f);
						const float nx = qx * ca - qz * sa;
						const float ny = qw * sa + qy * ca;
						const float nz = qx * sa + qz * ca;
						const float nw = qw * ca - qy * sa;
						const float len = std::sqrt(nx*nx + ny*ny + nz*nz + nw*nw);
						if (len > 0.0001f) { qx = nx/len; qy = ny/len; qz = nz/len; qw = nw/len; }
						// Scroll → alpha (written back below; actual ImGui alpha wiring TBD)
						if (ctrl) { float a = ctrl->quad_alpha + wheel * 0.1f; ctrl->quad_alpha = a < 0.0f ? 0.0f : (a > 1.0f ? 1.0f : a); }
					}

					// Write back to ctrl so ViewLab can read the live position
					if (ctrl)
					{
						ctrl->quad_pos_x  = pos_x; ctrl->quad_pos_y  = pos_y; ctrl->quad_pos_z  = pos_z;
						ctrl->quad_quat_x = qx;    ctrl->quad_quat_y = qy;    ctrl->quad_quat_z = qz;    ctrl->quad_quat_w = qw;
						ctrl->quad_width  = qw2;   ctrl->quad_height = qh2;
					}
					// Mirror to global — persisted on edit mode exit
					s_quad_xform.pos_x  = pos_x;  s_quad_xform.pos_y  = pos_y;  s_quad_xform.pos_z  = pos_z;
					s_quad_xform.quat_x = qx;      s_quad_xform.quat_y = qy;     s_quad_xform.quat_z = qz;    s_quad_xform.quat_w = qw;
					s_quad_xform.width  = qw2;     s_quad_xform.height = qh2;
					if (ctrl) s_quad_xform.alpha = ctrl->quad_alpha;
				}
			}

			// Detect edit mode exit → save transform to disk
			{
				static uint32_t s_prev_edit_mode = 0;
				const uint32_t  cur_edit_mode    = ctrl ? ctrl->quad_edit_mode : 0;
				if (s_prev_edit_mode != 0 && cur_edit_mode == 0)
					reshade::openxr::quad_transform_persist();
				s_prev_edit_mode = cur_edit_mode;
			}

			overlay_layer.pose.orientation = { qx, qy, qz, qw };
			overlay_layer.pose.position    = { pos_x, pos_y, -pos_z }; // negate Z: OpenXR forward is -Z
			overlay_layer.size             = { qw2, qh2 };

			modified_layers[n] = reinterpret_cast<const XrCompositionLayerBaseHeader *>(&overlay_layer);
			modified_frame_end_info = *frameEndInfo;
			modified_frame_end_info.layers = modified_layers;
			modified_frame_end_info.layerCount = n + 1;
			has_overlay = true;
		}
	}

	RESHADE_OPENXR_GET_DISPATCH_PTR_FROM(EndFrame, data.dispatch_table);
	return trampoline(session, has_overlay ? &modified_frame_end_info : frameEndInfo);
}

bool reshade::openxr::get_active_overlay_resources(reshade::api::resource *texture, reshade::api::resource_view *target)
{
	if (s_active_session == XR_NULL_HANDLE)
		return false;
	return get_overlay_resources(s_active_session, texture, target, nullptr);
}

bool reshade::openxr::get_overlay_resources(const XrSession session, reshade::api::resource *texture, reshade::api::resource_view *target, XrSwapchain *swapchain)
{
	openxr_session &data = s_openxr_sessions.at(session);
	if (texture) *texture = data.overlay_texture;
	if (target) *target = data.overlay_target;
	if (swapchain) *swapchain = data.overlay_swapchain;
	return data.overlay_swapchain != XR_NULL_HANDLE && data.overlay_texture != 0 && data.overlay_target != 0;
}

void reshade::openxr::set_overlay_visible(const XrSession session, const bool visible)
{
	s_openxr_sessions.at(session).overlay_visible = visible;
}

void reshade::openxr::set_overlay_visible_active(const bool visible)
{
	if (s_active_session != XR_NULL_HANDLE)
		s_openxr_sessions.at(s_active_session).overlay_visible = visible;
}

bool reshade::openxr::is_overlay_visible_active()
{
	if (s_active_session == XR_NULL_HANDLE) return false;
	return s_openxr_sessions.at(s_active_session).overlay_visible;
}

bool reshade::openxr::get_overlay_transform_active(overlay_transform *out)
{
	if (s_active_session == XR_NULL_HANDLE) return false;
	const openxr_session &d = s_openxr_sessions.at(s_active_session);
	out->quad_width  = d.overlay_quad_width;
	out->quad_height = d.overlay_quad_height;
	out->distance    = d.overlay_distance;
	out->offset_x    = d.overlay_offset_x;
	out->offset_y    = d.overlay_offset_y;
	return true;
}

void reshade::openxr::set_overlay_transform_active(const overlay_transform &t)
{
	if (s_active_session == XR_NULL_HANDLE) return;
	openxr_session &d = s_openxr_sessions.at(s_active_session);
	d.overlay_quad_width  = t.quad_width;
	d.overlay_quad_height = t.quad_height;
	d.overlay_distance    = t.distance;
	d.overlay_offset_x    = t.offset_x;
	d.overlay_offset_y    = t.offset_y;
}

bool reshade::openxr::copy_overlay_to_swapchain(const XrSession session)
{
	openxr_session &data = s_openxr_sessions.at(session);
	if (data.overlay_swapchain == XR_NULL_HANDLE || data.overlay_texture == 0 || !data.swapchain_impl)
		return false;

	openxr_swapchain &sc = s_openxr_swapchains.at(data.overlay_swapchain);

	uint32_t idx = 0;
	XrSwapchainImageAcquireInfo ai = { XR_TYPE_SWAPCHAIN_IMAGE_ACQUIRE_INFO };
	if (XR_FAILED(xrAcquireSwapchainImage(data.overlay_swapchain, &ai, &idx)))
		return false;

	XrSwapchainImageWaitInfo wi = { XR_TYPE_SWAPCHAIN_IMAGE_WAIT_INFO };
	wi.timeout = XR_INFINITE_DURATION;
	if (XR_FAILED(data.dispatch_table->WaitSwapchainImage(data.overlay_swapchain, &wi)))
		return false;

	reshade::api::device *dev = data.swapchain_impl->get_device();
	reshade::api::command_list *cl = data.swapchain_impl->get_graphics_queue()->get_immediate_command_list();

	// In D3D11 the overlay texture is always in copy_source; D3D12 needs shader_resource_pixel
	const reshade::api::resource_usage tex_prev = (dev->get_api() == reshade::api::device_api::d3d12)
		? reshade::api::resource_usage::shader_resource_pixel
		: reshade::api::resource_usage::copy_source;
	cl->barrier(data.overlay_texture, tex_prev, reshade::api::resource_usage::copy_source);
	cl->barrier(sc.surface_images[idx], reshade::api::resource_usage::undefined, reshade::api::resource_usage::copy_dest);
	cl->copy_texture_region(data.overlay_texture, 0, nullptr, sc.surface_images[idx], 0, nullptr, reshade::api::filter_mode::min_mag_mip_point);
	cl->barrier(sc.surface_images[idx], reshade::api::resource_usage::copy_dest, reshade::api::resource_usage::shader_resource_pixel);
	cl->barrier(data.overlay_texture, reshade::api::resource_usage::copy_source, tex_prev);
	data.swapchain_impl->get_graphics_queue()->flush_immediate_command_list();

	XrSwapchainImageReleaseInfo ri = { XR_TYPE_SWAPCHAIN_IMAGE_RELEASE_INFO };
	return XR_SUCCEEDED(xrReleaseSwapchainImage(data.overlay_swapchain, &ri));
}
