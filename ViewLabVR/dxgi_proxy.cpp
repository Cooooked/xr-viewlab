#include "pch_vr.h"
#include "shared_memory.h"
#include "overlay.h"
#include "mouse_input.h"
#include "log.h"

// ---------------------------------------------------------------------------
// Real DXGI function pointers (resolved from %System32%\dxgi.dll at startup)
// ---------------------------------------------------------------------------
namespace RealDXGI {

using PFN_CreateDXGIFactory  = HRESULT(WINAPI*)(REFIID, void**);
using PFN_CreateDXGIFactory1 = HRESULT(WINAPI*)(REFIID, void**);
using PFN_CreateDXGIFactory2 = HRESULT(WINAPI*)(UINT, REFIID, void**);
using PFN_DXGIGetDebugInterface1 = HRESULT(WINAPI*)(UINT, REFIID, void**);
using PFN_DXGIDisableVBlankVirtualization = HRESULT(WINAPI*)();

PFN_CreateDXGIFactory  pCreateDXGIFactory  = nullptr;
PFN_CreateDXGIFactory1 pCreateDXGIFactory1 = nullptr;
PFN_CreateDXGIFactory2 pCreateDXGIFactory2 = nullptr;
PFN_DXGIGetDebugInterface1 pDXGIGetDebugInterface1 = nullptr;
PFN_DXGIDisableVBlankVirtualization pDXGIDisableVBlankVirtualization = nullptr;

} // namespace RealDXGI

// ---------------------------------------------------------------------------
// Shared menu texture (owned by this proxy DLL)
// ---------------------------------------------------------------------------
namespace {

ID3D11Device*        g_d3dDevice    = nullptr;
ID3D11Texture2D*     g_menuTexture  = nullptr;
ID3D11DeviceContext* g_d3dContext   = nullptr;
std::atomic<bool>    g_initialized{false};

void CreateMenuTexture() {
    if (!g_d3dDevice || g_menuTexture) return;

    D3D11_TEXTURE2D_DESC desc{};
    desc.Width     = 1280;
    desc.Height    = 720;
    desc.MipLevels = 1;
    desc.ArraySize = 1;
    desc.Format    = DXGI_FORMAT_B8G8R8A8_UNORM;
    desc.SampleDesc.Count = 1;
    desc.Usage     = D3D11_USAGE_DEFAULT;
    desc.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
    desc.MiscFlags = D3D11_RESOURCE_MISC_SHARED_NTHANDLE | D3D11_RESOURCE_MISC_SHARED_KEYEDMUTEX;

    HRESULT hr = g_d3dDevice->CreateTexture2D(&desc, nullptr, &g_menuTexture);
    if (FAILED(hr)) {
        VrLog("CreateMenuTexture: CreateTexture2D failed (0x%08X)", (unsigned)hr);
        return;
    }

    IDXGIResource1* res1 = nullptr;
    hr = g_menuTexture->QueryInterface(__uuidof(IDXGIResource1), reinterpret_cast<void**>(&res1));
    if (FAILED(hr)) {
        VrLog("CreateMenuTexture: QI IDXGIResource1 failed (0x%08X)", (unsigned)hr);
        return;
    }

    HANDLE sharedHandle = nullptr;
    hr = res1->CreateSharedHandle(nullptr, GENERIC_ALL, L"ViewLabVRMenuTex", &sharedHandle);
    res1->Release();

    if (FAILED(hr) || !sharedHandle) {
        VrLog("CreateMenuTexture: CreateSharedHandle failed (0x%08X)", (unsigned)hr);
        return;
    }

    VRControlBlock* block = GetVRBlock();
    if (block) {
        block->menu_texture_handle  = reinterpret_cast<uint64_t>(sharedHandle);
        block->menu_texture_width   = 1280;
        block->menu_texture_height  = 720;
    }

    VrLog("CreateMenuTexture: OK (handle=0x%p)", sharedHandle);
}

// Staging texture for CPU readback (created once alongside g_menuTexture)
ID3D11Texture2D* g_stagingTexture = nullptr;

void EnsureStagingTexture(UINT w, UINT h) {
    if (g_stagingTexture) return;
    D3D11_TEXTURE2D_DESC desc{};
    desc.Width = w; desc.Height = h;
    desc.MipLevels = 1; desc.ArraySize = 1;
    desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
    desc.SampleDesc.Count = 1;
    desc.Usage = D3D11_USAGE_STAGING;
    desc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
    g_d3dDevice->CreateTexture2D(&desc, nullptr, &g_stagingTexture);
}

// Called from Present when menu_visible==1.
// Copies the current backbuffer (with ReShade menu drawn) into g_menuTexture (for
// the IVROverlay) and into the CPU pixel mapping (for VrMenuWindow in ViewLab).
// The ViewLab-patched ReShade drew its menu onto the swap chain backbuffer.
void CopyMenuTexture(IDXGISwapChain* swapChain) {
    if (!g_menuTexture || !g_d3dContext) return;

    ID3D11Texture2D* backbuffer = nullptr;
    if (FAILED(swapChain->GetBuffer(0, __uuidof(ID3D11Texture2D), reinterpret_cast<void**>(&backbuffer))))
        return;

    D3D11_TEXTURE2D_DESC bbDesc{};
    backbuffer->GetDesc(&bbDesc);

    g_d3dContext->CopyResource(g_menuTexture, backbuffer);

    // CPU readback path for VrMenuWindow
    void* pixelBuf = GetPixelBuffer();
    if (pixelBuf) {
        EnsureStagingTexture(bbDesc.Width, bbDesc.Height);
        if (g_stagingTexture) {
            g_d3dContext->CopyResource(g_stagingTexture, backbuffer);
            D3D11_MAPPED_SUBRESOURCE mapped{};
            if (SUCCEEDED(g_d3dContext->Map(g_stagingTexture, 0, D3D11_MAP_READ, 0, &mapped))) {
                const UINT rowBytes = bbDesc.Width * 4;
                const auto* src = static_cast<const uint8_t*>(mapped.pData);
                auto* dst = static_cast<uint8_t*>(pixelBuf);
                for (UINT row = 0; row < bbDesc.Height; ++row) {
                    memcpy(dst + row * rowBytes, src + row * mapped.RowPitch, rowBytes);
                }
                g_d3dContext->Unmap(g_stagingTexture, 0);
            }
        }
    } else {
        // First frame: initialise pixel mapping at actual backbuffer size
        if (InitPixelMapping(bbDesc.Width, bbDesc.Height)) {
            VRControlBlock* block = GetVRBlock();
            if (block) {
                block->menu_texture_width  = bbDesc.Width;
                block->menu_texture_height = bbDesc.Height;
            }
        }
    }

    backbuffer->Release();

    VRControlBlock* block = GetVRBlock();
    if (block) block->menu_texture_revision++;
}

} // namespace

// ---------------------------------------------------------------------------
// IDXGISwapChain wrapper
// ---------------------------------------------------------------------------
struct ViewLabSwapChain final : IDXGISwapChain1 {
    IDXGISwapChain1* m_real;
    ULONG m_refs;

    explicit ViewLabSwapChain(IDXGISwapChain1* real) : m_real(real), m_refs(1) {
        // Capture device on first swap chain creation
        if (!g_d3dDevice) {
            IUnknown* dev = nullptr;
            if (SUCCEEDED(real->GetDevice(__uuidof(ID3D11Device), reinterpret_cast<void**>(&dev)))) {
                g_d3dDevice = static_cast<ID3D11Device*>(dev);
                g_d3dDevice->GetImmediateContext(&g_d3dContext);
                CreateMenuTexture();
                InitOverlay();
                InitMouseInput();
                g_initialized = true;
                VrLog("ViewLabSwapChain: D3D11 device captured, overlay and mouse input initialised");
            }
        }
    }

    // IUnknown
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppv) override {
        if (riid == __uuidof(IUnknown) ||
            riid == __uuidof(IDXGIObject) ||
            riid == __uuidof(IDXGIDeviceSubObject) ||
            riid == __uuidof(IDXGISwapChain) ||
            riid == __uuidof(IDXGISwapChain1))
        {
            *ppv = this; AddRef(); return S_OK;
        }
        return m_real->QueryInterface(riid, ppv);
    }
    ULONG STDMETHODCALLTYPE AddRef()  override { return ++m_refs; }
    ULONG STDMETHODCALLTYPE Release() override {
        if (--m_refs == 0) { m_real->Release(); delete this; return 0; }
        return m_refs;
    }

    // IDXGIObject
    HRESULT STDMETHODCALLTYPE SetPrivateData(REFGUID g, UINT s, const void* d) override { return m_real->SetPrivateData(g,s,d); }
    HRESULT STDMETHODCALLTYPE SetPrivateDataInterface(REFGUID g, const IUnknown* u) override { return m_real->SetPrivateDataInterface(g,u); }
    HRESULT STDMETHODCALLTYPE GetPrivateData(REFGUID g, UINT* s, void* d) override { return m_real->GetPrivateData(g,s,d); }
    HRESULT STDMETHODCALLTYPE GetParent(REFIID r, void** p) override { return m_real->GetParent(r,p); }

    // IDXGIDeviceSubObject
    HRESULT STDMETHODCALLTYPE GetDevice(REFIID r, void** p) override { return m_real->GetDevice(r,p); }

    // IDXGISwapChain
    HRESULT STDMETHODCALLTYPE Present(UINT SyncInterval, UINT Flags) override {
        OnPrePresent();
        return m_real->Present(SyncInterval, Flags);
    }
    HRESULT STDMETHODCALLTYPE GetBuffer(UINT b, REFIID r, void** p) override { return m_real->GetBuffer(b,r,p); }
    HRESULT STDMETHODCALLTYPE SetFullscreenState(BOOL f, IDXGIOutput* t) override { return m_real->SetFullscreenState(f,t); }
    HRESULT STDMETHODCALLTYPE GetFullscreenState(BOOL* f, IDXGIOutput** t) override { return m_real->GetFullscreenState(f,t); }
    HRESULT STDMETHODCALLTYPE GetDesc(DXGI_SWAP_CHAIN_DESC* d) override { return m_real->GetDesc(d); }
    HRESULT STDMETHODCALLTYPE ResizeBuffers(UINT c, UINT w, UINT h, DXGI_FORMAT f, UINT fl) override { return m_real->ResizeBuffers(c,w,h,f,fl); }
    HRESULT STDMETHODCALLTYPE ResizeTarget(const DXGI_MODE_DESC* d) override { return m_real->ResizeTarget(d); }
    HRESULT STDMETHODCALLTYPE GetContainingOutput(IDXGIOutput** o) override { return m_real->GetContainingOutput(o); }
    HRESULT STDMETHODCALLTYPE GetFrameStatistics(DXGI_FRAME_STATISTICS* s) override { return m_real->GetFrameStatistics(s); }
    HRESULT STDMETHODCALLTYPE GetLastPresentCount(UINT* c) override { return m_real->GetLastPresentCount(c); }

    // IDXGISwapChain1
    HRESULT STDMETHODCALLTYPE GetDesc1(DXGI_SWAP_CHAIN_DESC1* d) override { return m_real->GetDesc1(d); }
    HRESULT STDMETHODCALLTYPE GetFullscreenDesc(DXGI_SWAP_CHAIN_FULLSCREEN_DESC* d) override { return m_real->GetFullscreenDesc(d); }
    HRESULT STDMETHODCALLTYPE GetHwnd(HWND* h) override { return m_real->GetHwnd(h); }
    HRESULT STDMETHODCALLTYPE GetCoreWindow(REFIID r, void** p) override { return m_real->GetCoreWindow(r,p); }
    HRESULT STDMETHODCALLTYPE Present1(UINT si, UINT fl, const DXGI_PRESENT_PARAMETERS* pp) override {
        OnPrePresent();
        return m_real->Present1(si, fl, pp);
    }
    HRESULT STDMETHODCALLTYPE IsTemporaryMonoSupported() override { return m_real->IsTemporaryMonoSupported(); }
    HRESULT STDMETHODCALLTYPE GetRestrictToOutput(IDXGIOutput** o) override { return m_real->GetRestrictToOutput(o); }
    HRESULT STDMETHODCALLTYPE SetBackgroundColor(const DXGI_RGBA* c) override { return m_real->SetBackgroundColor(c); }
    HRESULT STDMETHODCALLTYPE GetBackgroundColor(DXGI_RGBA* c) override { return m_real->GetBackgroundColor(c); }
    HRESULT STDMETHODCALLTYPE SetRotation(DXGI_MODE_ROTATION r) override { return m_real->SetRotation(r); }
    HRESULT STDMETHODCALLTYPE GetRotation(DXGI_MODE_ROTATION* r) override { return m_real->GetRotation(r); }

private:
    void OnPrePresent() {
        const VRControlBlock* block = GetVRBlock();
        if (!block) return;

        if (block->menu_visible) {
            CopyMenuTexture(m_real);
        }

        UpdateOverlay(g_menuTexture);
    }
};

// ---------------------------------------------------------------------------
// IDXGIFactory wrapper — wraps CreateSwapChain* to inject ViewLabSwapChain
// ---------------------------------------------------------------------------
struct ViewLabFactory final : IDXGIFactory2 {
    IDXGIFactory2* m_real;
    ULONG m_refs;

    explicit ViewLabFactory(IDXGIFactory2* real) : m_real(real), m_refs(1) {}

    // IUnknown
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppv) override {
        if (riid == __uuidof(IUnknown) ||
            riid == __uuidof(IDXGIObject) ||
            riid == __uuidof(IDXGIFactory) ||
            riid == __uuidof(IDXGIFactory1) ||
            riid == __uuidof(IDXGIFactory2))
        {
            *ppv = this; AddRef(); return S_OK;
        }
        return m_real->QueryInterface(riid, ppv);
    }
    ULONG STDMETHODCALLTYPE AddRef()  override { return ++m_refs; }
    ULONG STDMETHODCALLTYPE Release() override {
        if (--m_refs == 0) { m_real->Release(); delete this; return 0; }
        return m_refs;
    }

    // IDXGIObject
    HRESULT STDMETHODCALLTYPE SetPrivateData(REFGUID g, UINT s, const void* d) override { return m_real->SetPrivateData(g,s,d); }
    HRESULT STDMETHODCALLTYPE SetPrivateDataInterface(REFGUID g, const IUnknown* u) override { return m_real->SetPrivateDataInterface(g,u); }
    HRESULT STDMETHODCALLTYPE GetPrivateData(REFGUID g, UINT* s, void* d) override { return m_real->GetPrivateData(g,s,d); }
    HRESULT STDMETHODCALLTYPE GetParent(REFIID r, void** p) override { return m_real->GetParent(r,p); }

    // IDXGIFactory
    HRESULT STDMETHODCALLTYPE EnumAdapters(UINT i, IDXGIAdapter** a) override { return m_real->EnumAdapters(i,a); }
    HRESULT STDMETHODCALLTYPE MakeWindowAssociation(HWND h, UINT f) override { return m_real->MakeWindowAssociation(h,f); }
    HRESULT STDMETHODCALLTYPE GetWindowAssociation(HWND* h) override { return m_real->GetWindowAssociation(h); }
    HRESULT STDMETHODCALLTYPE CreateSwapChain(IUnknown* dev, DXGI_SWAP_CHAIN_DESC* desc, IDXGISwapChain** sc) override {
        IDXGISwapChain* real = nullptr;
        HRESULT hr = m_real->CreateSwapChain(dev, desc, &real);
        if (SUCCEEDED(hr) && real) {
            IDXGISwapChain1* sc1 = nullptr;
            if (SUCCEEDED(real->QueryInterface(__uuidof(IDXGISwapChain1), reinterpret_cast<void**>(&sc1)))) {
                real->Release();
                *sc = new ViewLabSwapChain(sc1);
            } else {
                *sc = real; // Can't wrap; pass through
            }
        }
        return hr;
    }
    HRESULT STDMETHODCALLTYPE CreateSoftwareAdapter(HMODULE m, IDXGIAdapter** a) override { return m_real->CreateSoftwareAdapter(m,a); }

    // IDXGIFactory1
    HRESULT STDMETHODCALLTYPE EnumAdapters1(UINT i, IDXGIAdapter1** a) override { return m_real->EnumAdapters1(i,a); }
    BOOL    STDMETHODCALLTYPE IsCurrent() override { return m_real->IsCurrent(); }

    // IDXGIFactory2
    BOOL    STDMETHODCALLTYPE IsWindowedStereoEnabled() override { return m_real->IsWindowedStereoEnabled(); }
    HRESULT STDMETHODCALLTYPE CreateSwapChainForHwnd(IUnknown* dev, HWND hwnd, const DXGI_SWAP_CHAIN_DESC1* desc,
        const DXGI_SWAP_CHAIN_FULLSCREEN_DESC* fullscreen, IDXGIOutput* restrictOutput, IDXGISwapChain1** sc) override
    {
        IDXGISwapChain1* real = nullptr;
        HRESULT hr = m_real->CreateSwapChainForHwnd(dev, hwnd, desc, fullscreen, restrictOutput, &real);
        if (SUCCEEDED(hr) && real) *sc = new ViewLabSwapChain(real);
        return hr;
    }
    HRESULT STDMETHODCALLTYPE CreateSwapChainForCoreWindow(IUnknown* dev, IUnknown* win, const DXGI_SWAP_CHAIN_DESC1* desc,
        IDXGIOutput* restrictOutput, IDXGISwapChain1** sc) override
    {
        IDXGISwapChain1* real = nullptr;
        HRESULT hr = m_real->CreateSwapChainForCoreWindow(dev, win, desc, restrictOutput, &real);
        if (SUCCEEDED(hr) && real) *sc = new ViewLabSwapChain(real);
        return hr;
    }
    HRESULT STDMETHODCALLTYPE GetSharedResourceAdapterLuid(HANDLE r, LUID* l) override { return m_real->GetSharedResourceAdapterLuid(r,l); }
    HRESULT STDMETHODCALLTYPE RegisterStereoStatusWindow(HWND h, UINT m, DWORD* c) override { return m_real->RegisterStereoStatusWindow(h,m,c); }
    HRESULT STDMETHODCALLTYPE RegisterStereoStatusEvent(HANDLE e, DWORD* c) override { return m_real->RegisterStereoStatusEvent(e,c); }
    void    STDMETHODCALLTYPE UnregisterStereoStatus(DWORD c) override { m_real->UnregisterStereoStatus(c); }
    HRESULT STDMETHODCALLTYPE RegisterOcclusionStatusWindow(HWND h, UINT m, DWORD* c) override { return m_real->RegisterOcclusionStatusWindow(h,m,c); }
    HRESULT STDMETHODCALLTYPE RegisterOcclusionStatusEvent(HANDLE e, DWORD* c) override { return m_real->RegisterOcclusionStatusEvent(e,c); }
    void    STDMETHODCALLTYPE UnregisterOcclusionStatus(DWORD c) override { m_real->UnregisterOcclusionStatus(c); }
    HRESULT STDMETHODCALLTYPE CreateSwapChainForComposition(IUnknown* dev, const DXGI_SWAP_CHAIN_DESC1* desc,
        IDXGIOutput* restrictOutput, IDXGISwapChain1** sc) override
    {
        IDXGISwapChain1* real = nullptr;
        HRESULT hr = m_real->CreateSwapChainForComposition(dev, desc, restrictOutput, &real);
        if (SUCCEEDED(hr) && real) *sc = new ViewLabSwapChain(real);
        return hr;
    }
};

// ---------------------------------------------------------------------------
// Helper: wrap any IDXGIFactory* in ViewLabFactory
// ---------------------------------------------------------------------------
static IDXGIFactory2* WrapFactory(IUnknown* unk) {
    IDXGIFactory2* f2 = nullptr;
    if (SUCCEEDED(unk->QueryInterface(__uuidof(IDXGIFactory2), reinterpret_cast<void**>(&f2)))) {
        return new ViewLabFactory(f2);
    }
    // Factory older than IDXGIFactory2: can't wrap CreateSwapChainForHwnd, but still wrap IDXGIFactory
    // For simplicity, return nullptr and let the caller fall through.
    return nullptr;
}

// ---------------------------------------------------------------------------
// Exported DXGI entry points — called by the game instead of the real ones
// ---------------------------------------------------------------------------
extern "C" {

HRESULT WINAPI CreateDXGIFactory(REFIID riid, void** ppFactory) {
    HRESULT hr = RealDXGI::pCreateDXGIFactory(riid, ppFactory);
    if (SUCCEEDED(hr) && ppFactory && *ppFactory) {
        if (auto* w = WrapFactory(static_cast<IUnknown*>(*ppFactory))) {
            static_cast<IUnknown*>(*ppFactory)->Release();
            *ppFactory = w;
        }
    }
    return hr;
}

HRESULT WINAPI CreateDXGIFactory1(REFIID riid, void** ppFactory) {
    HRESULT hr = RealDXGI::pCreateDXGIFactory1(riid, ppFactory);
    if (SUCCEEDED(hr) && ppFactory && *ppFactory) {
        if (auto* w = WrapFactory(static_cast<IUnknown*>(*ppFactory))) {
            static_cast<IUnknown*>(*ppFactory)->Release();
            *ppFactory = w;
        }
    }
    return hr;
}

HRESULT WINAPI CreateDXGIFactory2(UINT Flags, REFIID riid, void** ppFactory) {
    HRESULT hr = RealDXGI::pCreateDXGIFactory2(Flags, riid, ppFactory);
    if (SUCCEEDED(hr) && ppFactory && *ppFactory) {
        if (auto* w = WrapFactory(static_cast<IUnknown*>(*ppFactory))) {
            static_cast<IUnknown*>(*ppFactory)->Release();
            *ppFactory = w;
        }
    }
    return hr;
}

HRESULT WINAPI DXGIGetDebugInterface1(UINT Flags, REFIID riid, void** ppDebug) {
    return RealDXGI::pDXGIGetDebugInterface1 ? RealDXGI::pDXGIGetDebugInterface1(Flags, riid, ppDebug) : E_NOINTERFACE;
}

HRESULT WINAPI DXGIDisableVBlankVirtualization() {
    return RealDXGI::pDXGIDisableVBlankVirtualization ? RealDXGI::pDXGIDisableVBlankVirtualization() : S_OK;
}

} // extern "C"

// ---------------------------------------------------------------------------
// Proxy initialisation / teardown
// ---------------------------------------------------------------------------
namespace {

std::thread       g_initThread;
std::atomic<bool> g_shutdownProxy{false};

void ProxyInit() {
    // Load the real dxgi.dll from System32 — not ourselves.
    wchar_t sysDir[MAX_PATH]{};
    GetSystemDirectoryW(sysDir, static_cast<UINT>(std::size(sysDir)));
    const std::wstring realPath = std::wstring(sysDir) + L"\\dxgi.dll";

    HMODULE realDxgi = LoadLibraryW(realPath.c_str());
    if (!realDxgi) {
        VrLog("ProxyInit: failed to load real dxgi.dll from %ls (%lu)", realPath.c_str(), GetLastError());
        return;
    }

#define RESOLVE(name) RealDXGI::p##name = reinterpret_cast<RealDXGI::PFN_##name>(GetProcAddress(realDxgi, #name))
    RESOLVE(CreateDXGIFactory);
    RESOLVE(CreateDXGIFactory1);
    RESOLVE(CreateDXGIFactory2);
    RESOLVE(DXGIGetDebugInterface1);
    RESOLVE(DXGIDisableVBlankVirtualization);
#undef RESOLVE

    VrLog("ProxyInit: real dxgi.dll loaded, checking for OpenVR HMD...");

    if (!vr::VR_IsHmdPresent()) {
        VrLog("ProxyInit: no HMD present — ViewLabVR staying dormant");
        return;
    }

    VrLog("ProxyInit: HMD present — initialising shared memory");
    if (!InitSharedMemory()) {
        VrLog("ProxyInit: InitSharedMemory failed");
        return;
    }

    VrLog("ProxyInit: ready — swap chain wrapping active");
    // Overlay and mouse input are initialised lazily from the first ViewLabSwapChain creation
    // (we need the D3D11 device first).
}

} // namespace

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(hModule);
        g_initThread = std::thread(ProxyInit);
    } else if (reason == DLL_PROCESS_DETACH) {
        g_shutdownProxy = true;
        if (g_initThread.joinable()) g_initThread.join();

        ShutdownMouseInput();
        ShutdownOverlay();
        ShutdownSharedMemory();

        if (g_menuTexture)  { g_menuTexture->Release();  g_menuTexture  = nullptr; }
        if (g_d3dContext)   { g_d3dContext->Release();   g_d3dContext   = nullptr; }
        if (g_d3dDevice)    { g_d3dDevice->Release();    g_d3dDevice    = nullptr; }
    }
    return TRUE;
}
