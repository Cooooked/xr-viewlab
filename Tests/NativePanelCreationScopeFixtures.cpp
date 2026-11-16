#include "../NativePanelCreationScope.h"
#include <cassert>
#include <thread>
#include <stdexcept>
int main() {
    using Scope = viewlab::panels::NativePanelCreationScope;
    assert(!Scope::Active());
    {
        Scope outer;
        assert(Scope::Active());
        { Scope inner; assert(Scope::Active()); }
        assert(Scope::Active());
        std::thread other([] { assert(!Scope::Active()); }); other.join();
    }
    assert(!Scope::Active());
    try { Scope creation; throw std::runtime_error("create failed"); } catch (...) {}
    assert(!Scope::Active());
}
