// Included after the OpenXR dispatch table and renderer helpers.
namespace native_panels {
using Microsoft::WRL::ComPtr;
struct Surface {
    XrSwapchain chain=XR_NULL_HANDLE;
    std::vector<XrSwapchainImageD3D11KHR> images;
    uint32_t width=0,height=0,index=0;
    bool acquired=false,ready=false,blocked=false,pending=false;
    uint64_t window=0,serial=0;
    ComPtr<ID3D11Texture2D> latest;
};
struct Engine {
    HANDLE mapping=nullptr,statusMapping=nullptr;
    const viewlab::panels::State* shared=nullptr;
    uint64_t* status=nullptr;
    uint64_t nextConnect=0;
    XrSession session=XR_NULL_HANDLE;
    XrSpace local=XR_NULL_HANDLE,view=XR_NULL_HANDLE;
    XrPosef anchor{{0,0,0,1},{0,0,0}};
    bool anchored=false,blocked=false,loggedFirstConsume=false;
    int64_t swapchainFormat=DXGI_FORMAT_B8G8R8A8_UNORM_SRGB;
    uint32_t recenter=0,reportedFailures=0;
    uint64_t nextHealthLog=0,scCopies=0,scWaitTimeouts=0,scAcquireFails=0;
    std::unique_ptr<viewlab::panels::Capture> capture;
    std::array<Surface,viewlab::panels::MaxPanels> surfaces;
    std::array<XrCompositionLayerQuad,viewlab::panels::MaxPanels> quads;
    ~Engine(){Shutdown();}
    void Shutdown(){
        capture.reset();
        for(auto& s:surfaces){if(s.chain&&nextXrDestroySwapchain)nextXrDestroySwapchain(s.chain);s=Surface{};}
        if(local&&nextXrDestroySpace)nextXrDestroySpace(local);
        if(view&&nextXrDestroySpace)nextXrDestroySpace(view);
        local=view=XR_NULL_HANDLE;session=XR_NULL_HANDLE;anchored=false;blocked=false;
        if(status){status[0]=0;status[1]=0;UnmapViewOfFile(status);status=nullptr;}
        if(statusMapping){CloseHandle(statusMapping);statusMapping=nullptr;}
        if(shared){UnmapViewOfFile(shared);shared=nullptr;}
        if(mapping){CloseHandle(mapping);mapping=nullptr;}
    }
    bool Snapshot(viewlab::panels::State& s){
        const uint64_t now=GetTickCount64();
        if(!shared&&now>=nextConnect){
            nextConnect=now+500;
            mapping=OpenFileMappingW(FILE_MAP_READ,FALSE,L"Local\\XRViewLabNativePanels");
            if(mapping){shared=static_cast<const viewlab::panels::State*>(MapViewOfFile(mapping,FILE_MAP_READ,0,0,sizeof(s)));if(!shared){CloseHandle(mapping);mapping=nullptr;}}
        }
        if(!shared)return false;
        const uint32_t gen=*reinterpret_cast<const volatile uint32_t*>(&shared->generation);
        if(gen&1)return false;
        MemoryBarrier();memcpy(&s,shared,sizeof(s));MemoryBarrier();
        return gen==*reinterpret_cast<const volatile uint32_t*>(&shared->generation)&&viewlab::panels::Valid(s,now);
    }
    bool Initialize(XrSession current){
        if(blocked)return false;
        if(session==current&&capture)return true;
        if(session!=XR_NULL_HANDLE)Shutdown();
        if(!g_d3d11Mask.device||!nextXrCreateReferenceSpace||!nextXrLocateSpace||!nextXrEnumerateSwapchainFormats||
            !nextXrCreateSwapchain||!nextXrDestroySwapchain||!nextXrEnumerateSwapchainImages||!nextXrAcquireSwapchainImage||
            !nextXrWaitSwapchainImage||!nextXrReleaseSwapchainImage||!nextXrDestroySpace||!g_nativePanelLayerLimit)return false;
        if(g_d3d11Mask.device->GetCreationFlags()&D3D11_CREATE_DEVICE_SINGLETHREADED){
            Log("native panels: single-threaded D3D11 device cannot support cross-device worker; panels disabled\n");blocked=true;return false;
        }
        uint32_t n=0;if(XR_FAILED(nextXrEnumerateSwapchainFormats(current,0,&n,nullptr)))return false;
        std::vector<int64_t> formats(n);if(XR_FAILED(nextXrEnumerateSwapchainFormats(current,n,&n,formats.data())))return false;
        if(std::find(formats.begin(),formats.end(),DXGI_FORMAT_B8G8R8A8_UNORM_SRGB)!=formats.end()){
            swapchainFormat=DXGI_FORMAT_B8G8R8A8_UNORM_SRGB;
        } else if(std::find(formats.begin(),formats.end(),DXGI_FORMAT_B8G8R8A8_UNORM)!=formats.end()){
            swapchainFormat=DXGI_FORMAT_B8G8R8A8_UNORM;
        } else {
            Log("native panels: runtime has no BGRA8 swapchain format; capture panels disabled\n");blocked=true;return false;
        }
        XrReferenceSpaceCreateInfo info{XR_TYPE_REFERENCE_SPACE_CREATE_INFO};info.poseInReferenceSpace.orientation.w=1;
        info.referenceSpaceType=XR_REFERENCE_SPACE_TYPE_LOCAL;
        if(XR_FAILED(nextXrCreateReferenceSpace(current,&info,&local))){blocked=true;return false;}
        info.referenceSpaceType=XR_REFERENCE_SPACE_TYPE_VIEW;
        if(XR_FAILED(nextXrCreateReferenceSpace(current,&info,&view))){nextXrDestroySpace(local);local=XR_NULL_HANDLE;blocked=true;return false;}
        session=current;capture=std::make_unique<viewlab::panels::Capture>(g_d3d11Mask.device);

        Log("native panels: dedicated window capture device and OpenXR panel spaces initialized (format=%lld)\n",static_cast<long long>(swapchainFormat));return true;
    }
    void Append(XrSession current,XrTime time,std::vector<const XrCompositionLayerBaseHeader*>& layers,const XrFrameEndInfo* original=nullptr){
        if(!statusMapping)statusMapping=CreateFileMappingW(INVALID_HANDLE_VALUE,nullptr,PAGE_READWRITE,0,16,L"Local\\XRViewLabPanelSession");
        if(statusMapping&&!status)status=static_cast<uint64_t*>(MapViewOfFile(statusMapping,FILE_MAP_WRITE,0,0,16));
        if(status){status[0]=g_nativePanelFocused.load()?GetCurrentProcessId():0;MemoryBarrier();status[1]=g_nativePanelFocused.load()?GetTickCount64():0;}
        viewlab::panels::State s{};
        if(!Snapshot(s)){if(capture)capture->Configure(viewlab::panels::State{});return;}
        if(!s.count||!s.visible||g_rendererDeviceLost.load()){if(capture)capture->Configure(viewlab::panels::State{});return;}
        if(!Initialize(current))return;
        if(layers.empty()&&original&&original->layerCount){
            layers.reserve(original->layerCount+viewlab::panels::MaxPanels+1);
            layers.assign(original->layers,original->layers+original->layerCount);
        }
        if(status){status[0]=g_nativePanelFocused.load()?GetCurrentProcessId():0;MemoryBarrier();status[1]=g_nativePanelFocused.load()?GetTickCount64():0;}
        if(!g_nativePanelFocused.load())s.editing=0;
        capture->Configure(s);
        if(GetTickCount64()>=nextHealthLog){
            nextHealthLog=GetTickCount64()+2000;const auto health=capture->Diagnostics();
            if(health.failures!=reportedFailures){reportedFailures=health.failures;Log("native panels: capture retry count=%u last HRESULT=0x%08X\n",health.failures,static_cast<uint32_t>(health.lastError));}
            Log("native panels: stats cap=%llu pub=%llu con=%llu wake=%llu fail=%u err=0x%08X s0=(win=0x%llX ser=%llu pend=%d acq=%d blk=%d)\n",
                static_cast<unsigned long long>(health.capturedFrames),
                static_cast<unsigned long long>(health.publishedFrames),
                static_cast<unsigned long long>(health.consumedFrames),
                static_cast<unsigned long long>(health.workerWakeups),
                health.failures,static_cast<uint32_t>(health.lastError),
                static_cast<unsigned long long>(s.panels[0].window),
                static_cast<unsigned long long>(surfaces[0].serial),
                surfaces[0].pending?1:0,surfaces[0].acquired?1:0,surfaces[0].blocked?1:0);
            Log("native panels: swapchain stats sc=%llu swt=%llu saf=%llu si0=%u si1=%u\n",
                static_cast<unsigned long long>(scCopies),static_cast<unsigned long long>(scWaitTimeouts),
                static_cast<unsigned long long>(scAcquireFails),
                surfaces[0].images.size(),surfaces[1].images.size());
        }
        if(!anchored||recenter!=s.recenter){
            XrSpaceLocation loc{XR_TYPE_SPACE_LOCATION};
            if(XR_FAILED(nextXrLocateSpace(view,local,time,&loc))||
                (loc.locationFlags&(XR_SPACE_LOCATION_POSITION_VALID_BIT|XR_SPACE_LOCATION_ORIENTATION_VALID_BIT))!=
                 (XR_SPACE_LOCATION_POSITION_VALID_BIT|XR_SPACE_LOCATION_ORIENTATION_VALID_BIT))return;
            anchor=loc.pose;
            // Recenter uses level forward direction; leaning/pitch does not tilt the layout.
            auto q=DirectX::XMVectorSet(anchor.orientation.x,anchor.orientation.y,anchor.orientation.z,anchor.orientation.w);
            DirectX::XMFLOAT3 forward{};DirectX::XMStoreFloat3(&forward,DirectX::XMVector3Rotate(DirectX::XMVectorSet(0,0,-1,0),q));
            const float yaw=std::atan2(-forward.x,-forward.z);
            anchor.orientation={0,std::sin(yaw*.5f),0,std::cos(yaw*.5f)};
            recenter=s.recenter;anchored=true;
        }
        for(uint32_t i=0;i<s.count;++i){
            if(layers.size()>=g_nativePanelLayerLimit)break;
            const auto& p=s.panels[i];auto& surface=surfaces[i];
            if(!p.enabled||!p.window)continue;
            if(surface.window!=p.window) {
                if(surface.chain)nextXrDestroySwapchain(surface.chain);
                surface=Surface{};surface.window=p.window;
            }
            if(surface.blocked)continue;
            ComPtr<ID3D11Texture2D> latest;uint64_t serial=0;
            if(capture->Latest(i,p.window,g_d3d11Mask.context,&latest,serial)&&serial!=surface.serial){
                D3D11_TEXTURE2D_DESC incoming{};latest->GetDesc(&incoming);
                // Finish a pending old-size image with its retained content first.
                // Next frame accepts the new size; never flash an empty resize image.
                if(!surface.acquired||(incoming.Width==surface.width&&incoming.Height==surface.height)){
                    surface.latest=latest;surface.serial=serial;surface.pending=true;
                }
            }
            if(!surface.latest)continue;
            D3D11_TEXTURE2D_DESC desc{};surface.latest->GetDesc(&desc);
            const uint32_t w=desc.Width,h=desc.Height;
            if(surface.chain&&(surface.width!=w||surface.height!=h)&&!surface.acquired){
                nextXrDestroySwapchain(surface.chain);surface.chain=XR_NULL_HANDLE;
                surface.images.clear();surface.ready=false;surface.pending=true;
            }
            // A pending wait owns its old-size chain. Finish that image before resizing.
            if(!surface.chain){
                XrSwapchainCreateInfo ci{XR_TYPE_SWAPCHAIN_CREATE_INFO};ci.usageFlags=XR_SWAPCHAIN_USAGE_COLOR_ATTACHMENT_BIT|XR_SWAPCHAIN_USAGE_SAMPLED_BIT|XR_SWAPCHAIN_USAGE_TRANSFER_DST_BIT;
                ci.format=swapchainFormat;ci.sampleCount=1;ci.width=w;ci.height=h;ci.faceCount=1;ci.arraySize=1;ci.mipCount=1;
                if(XR_FAILED(nextXrCreateSwapchain(current,&ci,&surface.chain))){surface.blocked=true;continue;}
                surface.width=w;surface.height=h;uint32_t n=0;
                if(XR_FAILED(nextXrEnumerateSwapchainImages(surface.chain,0,&n,nullptr))||!n){surface.blocked=true;continue;}
                surface.images.resize(n,{XR_TYPE_SWAPCHAIN_IMAGE_D3D11_KHR});
                if(XR_FAILED(nextXrEnumerateSwapchainImages(surface.chain,n,&n,reinterpret_cast<XrSwapchainImageBaseHeader*>(surface.images.data())))){surface.blocked=true;continue;}
            }
            if(surface.pending||surface.acquired) {
                if(!surface.acquired){XrSwapchainImageAcquireInfo ai{XR_TYPE_SWAPCHAIN_IMAGE_ACQUIRE_INFO};
                    if(XR_SUCCEEDED(nextXrAcquireSwapchainImage(surface.chain,&ai,&surface.index)))surface.acquired=true;else ++scAcquireFails;}
                if(surface.acquired){
                    XrSwapchainImageWaitInfo wi{XR_TYPE_SWAPCHAIN_IMAGE_WAIT_INFO};wi.timeout=0;
                    const XrResult waited=nextXrWaitSwapchainImage(surface.chain,&wi);
                    if(waited==XR_TIMEOUT_EXPIRED){
                        // Do not hold an acquired image across frames: the runtime may need it
                        // back to make compositor progress. Release and retry fresh next frame.
                        XrSwapchainImageReleaseInfo ri{XR_TYPE_SWAPCHAIN_IMAGE_RELEASE_INFO};
                        nextXrReleaseSwapchainImage(surface.chain,&ri);surface.acquired=false;++scWaitTimeouts;
                    } else {
                        if(XR_FAILED(waited)||surface.index>=surface.images.size()){
                            surface.blocked=true;Log("native panels: swapchain wait failed (%d); panel disabled until source/session changes\n",waited);continue;
                        }
                        auto* target=surface.images[surface.index].texture;
                        // Always initialize every successfully waited image, including resize races.
                        // A zero-timeout capture miss never advances the runtime image.
                        if(surface.width==w&&surface.height==h){
                            g_d3d11Mask.context->CopyResource(target,surface.latest.Get());
                            surface.pending=false;
                        } else {
                            ComPtr<ID3D11RenderTargetView> rtv;
                            if(SUCCEEDED(g_d3d11Mask.device->CreateRenderTargetView(target,nullptr,&rtv))){
                                const FLOAT black[]={0,0,0,1};g_d3d11Mask.context->ClearRenderTargetView(rtv.Get(),black);
                            } else {surface.blocked=true;continue;}
                        }
                        XrSwapchainImageReleaseInfo ri{XR_TYPE_SWAPCHAIN_IMAGE_RELEASE_INFO};
                        if(XR_FAILED(nextXrReleaseSwapchainImage(surface.chain,&ri))){surface.blocked=true;continue;}
                        surface.acquired=false;surface.ready=true;
                    }
                }
            }
            // OpenXR composes the most recently released image. Reuse it until updated.
            if(!surface.ready)continue;
            auto& q=quads[i];q={XR_TYPE_COMPOSITION_LAYER_QUAD};q.eyeVisibility=XR_EYE_VISIBILITY_BOTH;
            q.space=p.anchor==1?view:local;q.pose.position={p.x,p.y,p.z};
            DirectX::XMFLOAT4 orientation{};DirectX::XMStoreFloat4(&orientation,DirectX::XMQuaternionRotationRollPitchYaw(p.pitch,p.yaw,p.roll));
            q.pose.orientation={orientation.x,orientation.y,orientation.z,orientation.w};
            if(p.anchor==0){
                auto aq=DirectX::XMVectorSet(anchor.orientation.x,anchor.orientation.y,anchor.orientation.z,anchor.orientation.w);
                DirectX::XMFLOAT3 position{};DirectX::XMStoreFloat3(&position,DirectX::XMVector3Rotate(DirectX::XMVectorSet(p.x,p.y,p.z,0),aq));
                q.pose.position={anchor.position.x+position.x,anchor.position.y+position.y,anchor.position.z+position.z};
                q.pose.orientation=MultiplyQuaternion(anchor.orientation,q.pose.orientation);
            }
            q.size={p.width,(s.reserved&(1u<<(16+i)))?(std::clamp)(p.width*surface.height/surface.width,.05f,10.f):p.height};q.subImage.swapchain=surface.chain;
            q.subImage.imageRect.extent={static_cast<int32_t>(surface.width),static_cast<int32_t>(surface.height)};
            layers.push_back(reinterpret_cast<const XrCompositionLayerBaseHeader*>(&q));
        }
    }
};
std::unique_ptr<Engine> engine;
}
void ClearNativePanelInputStatus(){
    std::lock_guard<std::recursive_mutex> lock(g_rendererMutex);
    if(native_panels::engine&&native_panels::engine->status){native_panels::engine->status[0]=0;MemoryBarrier();native_panels::engine->status[1]=0;}
}
void StopNativePanels(){native_panels::engine.reset();}
void AppendNativePanels(XrSession session,XrTime time,std::vector<const XrCompositionLayerBaseHeader*>& layers,const XrFrameEndInfo* original=nullptr){
    try {if(!native_panels::engine)native_panels::engine=std::make_unique<native_panels::Engine>();native_panels::engine->Append(session,time,layers,original);}
    catch(...){static uint64_t nextError=0;if(GetTickCount64()>=nextError){nextError=GetTickCount64()+5000;Log("native panels: frame bypassed after panel error\n");}ClearNativePanelInputStatus();}
}

