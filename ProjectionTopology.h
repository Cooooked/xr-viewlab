#pragma once

#include <cstddef>
#include <vector>

namespace viewlab::projection {

// A projection layer owns the ordered view set. Swapchains are only destinations
// referenced by each view's XrSwapchainSubImage; they do not define stereo context.
template <typename SwapchainHandle, typename View>
struct FrameTopology {
    struct BoundView {
        SwapchainHandle swapchain{};
        View view{};
    };

    std::vector<BoundView> views;

    std::vector<View> AllViews() const {
        std::vector<View> result;
        result.reserve(views.size());
        for (const BoundView& bound : views) result.push_back(bound.view);
        return result;
    }

    std::vector<View> TargetsFor(SwapchainHandle swapchain) const {
        std::vector<View> result;
        for (const BoundView& bound : views) {
            if (bound.swapchain == swapchain) result.push_back(bound.view);
        }
        return result;
    }
};

} // namespace viewlab::projection
