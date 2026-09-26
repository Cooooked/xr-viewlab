#include <d3d11.h>
#include <d3dcompiler.h>
#include <wrl/client.h>
#include <array>
#include <vector>
#include <iostream>
#include <stdexcept>
#include <string>
#include <cstring>
#include "../../ViewLabBridge/StereoSubmission.h"
#include "../../ViewLabBridge/StereoShader.h"
using Microsoft::WRL::ComPtr;
using namespace viewlab::bridge::stereo;
static void Check(HRESULT hr) { if (FAILED(hr)) throw std::runtime_error("D3D failure: " + std::to_string(hr)); }
static void Require(bool b, const char* s) { if (!b) throw std::runtime_error(s); }
// Independent two-pass reference projects in VS. Instanced path routes via a
// passthrough GS; broadcast path shares VS work and projects in GS.
static const char* Shader = R"(
cbuffer Params : register(b0) { float4 eyes[2]; uint mode; uint currentEye; uint2 padding; };
struct V { float4 p:SV_Position; float4 color:COLOR; uint eye:TEXCOORD; float4 right:NV_XYZW_RIGHT; };
struct G { float4 p:SV_Position; float4 color:COLOR; uint slice:SV_RenderTargetArrayIndex; };
float4 Project(float4 p,uint e) {
    // Perspective division and deliberately asymmetric frusta, plus eye offset.
    return float4(p.x * eyes[e].x + eyes[e].y * p.z + eyes[e].z,
                  p.y * eyes[e].w, p.z * 0.8, p.z);
}
V VS(uint vertex:SV_VertexID,uint instance:SV_InstanceID) {
    uint object = mode == 1 ? instance/2 : instance;
    uint eye = mode == 1 ? instance%2 : currentEye;
    float2 points[3] = {float2(-0.55,-0.5),float2(0,0.6),float2(0.55,-0.5)};
    float z = 0.8 + (object%3)*0.35;
    V o; o.p=float4(points[vertex%3] + float2((object%3)*0.23-0.23,0), z,1);
    o.color=float4((object%3)==0,(object%3)==1,(object%3)==2,0.65);
    o.eye=eye;
    o.right=Project(o.p,1);
    if(mode != 2) o.p=Project(o.p,eye);
    return o;
}
[maxvertexcount(6)]
void GS(triangle V v[3],inout TriangleStream<G> stream) {
    uint count=mode==2 ? 2 : 1;
    for(uint e=0;e<count;e++) {
        for(uint i=0;i<3;i++) {
            G o; o.slice=mode==2 ? e : v[i].eye;
            o.p=mode==2 ? Project(v[i].p,e) : v[i].p;
            o.color=v[i].color; stream.Append(o);
        }
        stream.RestartStrip();
    }
}
float4 PS(G p):SV_Target { return p.color; }
)";
struct alignas(16) Params { float eyes[2][4]; UINT mode, eye, padding[2]; };
struct Result { std::vector<unsigned char> pixels; D3D11_QUERY_DATA_PIPELINE_STATISTICS stats{}; UINT draws=0; };
class Probe {
    ComPtr<ID3D11Device> device;
    ComPtr<ID3D11DeviceContext> context;
    ComPtr<ID3D11VertexShader> vs;
    ComPtr<ID3D11GeometryShader> gs;
    ComPtr<ID3D11GeometryShader> adapterGs;
    ComPtr<ID3D11PixelShader> ps;
    ComPtr<ID3D11Buffer> constants, indices;
public:
    Probe() {
        D3D_FEATURE_LEVEL requested=D3D_FEATURE_LEVEL_11_0, actual{};
        Check(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_WARP,nullptr,0,&requested,1,
            D3D11_SDK_VERSION,&device,&actual,&context));
        auto compile=[&](const char* entry,const char* target) {
            ComPtr<ID3DBlob> code,error;
            HRESULT hr=D3DCompile(Shader,strlen(Shader),"StereoProbe",nullptr,nullptr,entry,target,
                D3DCOMPILE_ENABLE_STRICTNESS,0,&code,&error);
            if(FAILED(hr) && error) std::cerr << static_cast<const char*>(error->GetBufferPointer());
            Check(hr); return code;
        };
        auto v=compile("VS","vs_5_0"),g=compile("GS","gs_5_0"),p=compile("PS","ps_5_0");
        Check(device->CreateVertexShader(v->GetBufferPointer(),v->GetBufferSize(),nullptr,&vs));
        Check(device->CreateGeometryShader(g->GetBufferPointer(),g->GetBufferSize(),nullptr,&gs));
        auto adapter=BuildGeometryAdapter(v->GetBufferPointer(),v->GetBufferSize());
        Require(adapter.supported,adapter.reason.c_str());
        ComPtr<ID3DBlob> adapterCode,adapterError;
        HRESULT adapted=D3DCompile(adapter.source.data(),adapter.source.size(),"GeneratedSpsAdapter",nullptr,nullptr,
            "main","gs_5_0",D3DCOMPILE_ENABLE_STRICTNESS,0,&adapterCode,&adapterError);
        if(FAILED(adapted) && adapterError) std::cerr << static_cast<const char*>(adapterError->GetBufferPointer());
        Check(adapted);
        Check(device->CreateGeometryShader(adapterCode->GetBufferPointer(),adapterCode->GetBufferSize(),nullptr,&adapterGs));
        Require(!BuildGeometryAdapter(p->GetBufferPointer(),p->GetBufferSize()).supported,"pixel shader accepted as SPS producer");
        Require(!BuildGeometryAdapter(nullptr,0).supported,"empty shader accepted");
        Check(device->CreatePixelShader(p->GetBufferPointer(),p->GetBufferSize(),nullptr,&ps));
        D3D11_BUFFER_DESC cb{}; cb.ByteWidth=sizeof(Params); cb.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
        Check(device->CreateBuffer(&cb,nullptr,&constants));
        UINT idx[3]={0,1,2}; cb.ByteWidth=sizeof(idx); cb.BindFlags=D3D11_BIND_INDEX_BUFFER;
        D3D11_SUBRESOURCE_DATA data{idx,0,0}; Check(device->CreateBuffer(&cb,&data,&indices));
    }
    Result Render(Method method,UINT samples,bool blend,UINT objects) {
        constexpr UINT size=64;
        D3D11_TEXTURE2D_DESC desc{}; desc.Width=size; desc.Height=size; desc.MipLevels=1;
        desc.ArraySize=2; desc.Format=DXGI_FORMAT_R8G8B8A8_UNORM; desc.SampleDesc.Count=samples;
        desc.BindFlags=D3D11_BIND_RENDER_TARGET;
        ComPtr<ID3D11Texture2D> color,depth,resolved,staging;
        Check(device->CreateTexture2D(&desc,nullptr,&color));
        ComPtr<ID3D11RenderTargetView> rtv; Check(device->CreateRenderTargetView(color.Get(),nullptr,&rtv));
        desc.Format=DXGI_FORMAT_D24_UNORM_S8_UINT; desc.BindFlags=D3D11_BIND_DEPTH_STENCIL;
        Check(device->CreateTexture2D(&desc,nullptr,&depth));
        ComPtr<ID3D11DepthStencilView> dsv; Check(device->CreateDepthStencilView(depth.Get(),nullptr,&dsv));
        desc.Format=DXGI_FORMAT_R8G8B8A8_UNORM; desc.SampleDesc.Count=1; desc.BindFlags=0;
        Check(device->CreateTexture2D(&desc,nullptr,&resolved));
        desc.Usage=D3D11_USAGE_STAGING; desc.CPUAccessFlags=D3D11_CPU_ACCESS_READ;
        Check(device->CreateTexture2D(&desc,nullptr,&staging));
        D3D11_RASTERIZER_DESC rd{}; rd.FillMode=D3D11_FILL_SOLID; rd.CullMode=D3D11_CULL_NONE;
        rd.DepthClipEnable=TRUE; rd.MultisampleEnable=samples>1;
        ComPtr<ID3D11RasterizerState> raster; Check(device->CreateRasterizerState(&rd,&raster));
        context->RSSetState(raster.Get()); D3D11_VIEWPORT viewport{0,0,float(size),float(size),0,1};
        context->RSSetViewports(1,&viewport);
        D3D11_BLEND_DESC bd{}; auto& b=bd.RenderTarget[0]; b.BlendEnable=blend;
        b.SrcBlend=D3D11_BLEND_SRC_ALPHA; b.DestBlend=D3D11_BLEND_INV_SRC_ALPHA; b.BlendOp=D3D11_BLEND_OP_ADD;
        b.SrcBlendAlpha=D3D11_BLEND_ONE; b.DestBlendAlpha=D3D11_BLEND_ZERO; b.BlendOpAlpha=D3D11_BLEND_OP_ADD;
        b.RenderTargetWriteMask=D3D11_COLOR_WRITE_ENABLE_ALL;
        ComPtr<ID3D11BlendState> bs; Check(device->CreateBlendState(&bd,&bs));
        context->OMSetBlendState(bs.Get(),nullptr,0xffffffff);
        context->OMSetRenderTargets(1,rtv.GetAddressOf(),dsv.Get());
        float clear[4]={0,0,0,0}; context->ClearRenderTargetView(rtv.Get(),clear);
        context->ClearDepthStencilView(dsv.Get(),D3D11_CLEAR_DEPTH|D3D11_CLEAR_STENCIL,1,0);
        context->VSSetShader(vs.Get(),nullptr,0);
        context->GSSetShader(method==Method::SecondaryPosition?adapterGs.Get():gs.Get(),nullptr,0);
        context->PSSetShader(ps.Get(),nullptr,0);
        context->VSSetConstantBuffers(0,1,constants.GetAddressOf());
        context->GSSetConstantBuffers(0,1,constants.GetAddressOf());
        context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
        context->IASetIndexBuffer(indices.Get(),DXGI_FORMAT_R32_UINT,0);
        D3D11_QUERY_DESC qd{D3D11_QUERY_PIPELINE_STATISTICS,0}; ComPtr<ID3D11Query> query;
        Check(device->CreateQuery(&qd,&query)); context->Begin(query.Get());
        Params params{{{1.1f,0.12f,0.05f,1.0f},{0.9f,-0.08f,-0.05f,1.15f}},UINT(method),0,{0,0}};
        auto plan=Select({true,true,true,false,false},method,objects);
        Result result; result.draws=method==Method::TwoPass?2:1;
        for(UINT eye=0;eye<result.draws;eye++) {
            params.eye=eye; context->UpdateSubresource(constants.Get(),0,nullptr,&params,0,0);
            context->DrawIndexedInstanced(3,plan.instances,0,0,0);
        }
        context->End(query.Get()); context->OMSetRenderTargets(0,nullptr,nullptr);
        if(samples>1) for(UINT eye=0;eye<2;eye++)
            context->ResolveSubresource(resolved.Get(),eye,color.Get(),eye,DXGI_FORMAT_R8G8B8A8_UNORM);
        else context->CopyResource(resolved.Get(),color.Get());
        context->CopyResource(staging.Get(),resolved.Get());
        result.pixels.resize(size*size*4*2);
        for(UINT eye=0;eye<2;eye++) {
            D3D11_MAPPED_SUBRESOURCE mapped{}; Check(context->Map(staging.Get(),eye,D3D11_MAP_READ,0,&mapped));
            for(UINT y=0;y<size;y++) memcpy(result.pixels.data()+(eye*size*size+y*size)*4,
                static_cast<const unsigned char*>(mapped.pData)+y*mapped.RowPitch,size*4);
            context->Unmap(staging.Get(),eye);
        }
        Check(context->GetData(query.Get(),&result.stats,sizeof(result.stats),0));
        return result;
    }
};
int main() {
    try {
        Require(Select({},Method::Instanced,1).rejection==Rejection::MissingContract,"unknown accepted");
        Require(Select({true,true,true,true,false},Method::Instanced,1).rejection==Rejection::SideEffects,"UAV accepted");
        Require(Select({true,true,true,false,true},Method::GeometryBroadcast,1).rejection==Rejection::ExistingGeometryStage,"GS accepted");
        Require(Select({true,true,true,false,false},Method::Instanced,0xffffffff).rejection==Rejection::Overflow,"overflow accepted");
        Probe probe; UINT cases=0;
        for(UINT samples:{1u,4u}) for(bool blend:{false,true}) for(UINT objects:{1u,3u,7u}) {
            auto reference=probe.Render(Method::TwoPass,samples,blend,objects);
            auto half=reference.pixels.size()/2;
            Require(memcmp(reference.pixels.data(),reference.pixels.data()+half,half)!=0,"eyes identical");
            for(auto method:{Method::Instanced,Method::GeometryBroadcast,Method::SecondaryPosition}) {
                auto candidate=probe.Render(method,samples,blend,objects);
                Require(reference.pixels==candidate.pixels,"stereo image mismatch");
                Require(candidate.draws*2==reference.draws,"draw count mismatch");
                Require(candidate.stats.PSInvocations>0,"empty rendering");
                if(method==Method::GeometryBroadcast || method==Method::SecondaryPosition)
                    Require(candidate.stats.VSInvocations*2==reference.stats.VSInvocations,"vertex work not halved");
                cases++;
            }
        }
        std::cout << "PASS: " << cases << " exact stereo image comparisons; 4 rejection checks.\n"
                  << "WARP software correctness only. Two submissions -> one; broadcast VS invocations halved.\n"
                  << "No iRacing integration or hardware performance claim.\n";
        return 0;
    } catch(const std::exception& e) { std::cerr << e.what() << '\n'; return 1; }
}
