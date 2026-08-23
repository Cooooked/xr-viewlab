/*
 * ViewLab Stabilizer OBS module — a focused port of LiveVisionKit 1.2.2's video stabilizer.
 *
 * Upstream copyright (C) 2022 Sebastian Di Marco. Modifications copyright (C) 2026 Cooooked.
 * GPL-3.0-or-later. See LICENSE-LiveVisionKit.txt and README.md.
 */
#include <obs-module.h>
#include <opencv2/core/ocl.hpp>

#include "OBS/Interop/InteropContext.hpp"
#include "OBS/Utility/Logging.hpp"
#include "OBS/Effects/FSREffect.hpp"

#define VIEWLAB_STABILIZER_VERSION "1.0.0-lvk1.2.2"

OBS_DECLARE_MODULE()

MODULE_EXPORT const char *obs_module_name(void)
{
	return "ViewLab Stabilizer";
}

MODULE_EXPORT const char *obs_module_description(void)
{
	return "Buffered OpenCV/OpenCL video stabilization ported from LiveVisionKit 1.2.2: optical-flow tracking, affine/homography motion, Gaussian trajectory smoothing and crop-safe warping.";
}

extern void register_vs_effect_source();

static void attach_ocl_interop_context(void *, uint32_t, uint32_t)
{
	lvk::ocl::InteropContext::TryAttach();
}

bool obs_module_load(void)
{
	const bool has_opencl = cv::ocl::haveOpenCL();
	const bool has_interop = lvk::ocl::InteropContext::Supported();
	const bool has_fsr_effect = lvk::FSREffect::IsCompiled();

	lvk::log::print_block(
		"Initializing focused LVK stabilizer port..."
		"\n    Version: %s"
		"\n    Upstream: LiveVisionKit 1.2.2 (faff156)"
		"\n    OpenCL Support: %s"
		"\n    OpenCL Interop Support: %s"
		"\n    FSR Effect Loaded: %s",
		VIEWLAB_STABILIZER_VERSION,
		has_opencl ? "Yes" : "No",
		has_interop ? "Yes" : "No",
		has_fsr_effect ? "Yes" : "No");

	if (!has_opencl || !has_fsr_effect) {
		lvk::log::error("Required OpenCL or FSR support is unavailable; filter not registered");
		return true;
	}

	if (has_interop)
		obs_add_main_render_callback(&attach_ocl_interop_context, nullptr);

	register_vs_effect_source();
	lvk::log::print("ViewLab Stabilizer filter registered (id=viewlab_stabilizer)");
	return true;
}

void obs_module_unload(void)
{
	obs_remove_main_render_callback(&attach_ocl_interop_context, nullptr);
	if (lvk::ocl::InteropContext::Supported())
		lvk::ocl::InteropContext::Release();
}
