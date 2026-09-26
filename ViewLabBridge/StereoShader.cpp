#include "StereoShader.h"
#include <d3d11shader.h>
#include <d3dcompiler.h>
#include <wrl/client.h>
#include <vector>
#include <cstring>
namespace viewlab::bridge::stereo {
ShaderAdapter BuildGeometryAdapter(const void* bytecode, std::size_t bytes) {
    Microsoft::WRL::ComPtr<ID3D11ShaderReflection> reflection;
    if (!bytecode || !bytes || FAILED(D3DReflect(bytecode,bytes,IID_PPV_ARGS(&reflection))))
        return {false,{},"Invalid shader bytecode"};
    D3D11_SHADER_DESC shader{};
    if(FAILED(reflection->GetDesc(&shader)) || D3D11_SHVER_GET_TYPE(shader.Version)!=D3D11_SHVER_VERTEX_SHADER)
        return {false,{},"Expected vertex shader"};
    // UAV writes and other shader stages need an explicit side-effect contract.
    for(UINT i=0;i<shader.BoundResources;i++) {
        D3D11_SHADER_INPUT_BIND_DESC resource{};
        if(FAILED(reflection->GetResourceBindingDesc(i,&resource))) return {false,{},"Resource reflection failed"};
        if(resource.Type!=D3D_SIT_CBUFFER && resource.Type!=D3D_SIT_TBUFFER &&
           resource.Type!=D3D_SIT_TEXTURE && resource.Type!=D3D_SIT_SAMPLER &&
           resource.Type!=D3D_SIT_STRUCTURED && resource.Type!=D3D_SIT_BYTEADDRESS)
            return {false,{},"Shader side effects require separate validation"};
    }
    std::string fields; int position=-1,right=-1; bool xOnly=false;
    for(UINT i=0;i<shader.OutputParameters;i++) {
        D3D11_SIGNATURE_PARAMETER_DESC p{};
        if(FAILED(reflection->GetOutputParameterDesc(i,&p))) return {false,{},"Signature reflection failed"};
        if(p.Stream || p.MinPrecision!=D3D_MIN_PRECISION_DEFAULT)
            return {false,{},"Stream or reduced-precision output unsupported"};
        if(p.SystemValueType!=D3D_NAME_UNDEFINED && p.SystemValueType!=D3D_NAME_POSITION)
            return {false,{},"System output requires explicit translation"};
        UINT count=0; for(UINT mask=p.Mask;mask;mask>>=1) count++;
        if(count<1 || count>4 || p.Mask!=((1u<<count)-1)) return {false,{},"Non-contiguous output mask"};
        const char* type=p.ComponentType==D3D_REGISTER_COMPONENT_FLOAT32?"float":
            p.ComponentType==D3D_REGISTER_COMPONENT_UINT32?"uint":
            p.ComponentType==D3D_REGISTER_COMPONENT_SINT32?"int":nullptr;
        if(!type) return {false,{},"Unknown component type"};
        if(p.SystemValueType==D3D_NAME_POSITION) {
            if(position>=0 || count!=4 || p.ComponentType!=D3D_REGISTER_COMPONENT_FLOAT32)
                return {false,{},"Position must be one float4"};
            position=int(i);
        }
        bool isX=_stricmp(p.SemanticName,"NV_X_RIGHT")==0;
        bool isFull=_stricmp(p.SemanticName,"NV_XYZW_RIGHT")==0;
        if(isX || isFull) {
            if(right>=0 || p.SemanticIndex!=0 || p.ComponentType!=D3D_REGISTER_COMPONENT_FLOAT32 || count!=(isX?1u:4u))
                return {false,{},"Invalid secondary position"};
            right=int(i); xOnly=isX;
        } else if(_strnicmp(p.SemanticName,"NV_",3)==0)
            return {false,{},"Additional NVIDIA semantic needs translation"};
        fields += std::string(p.ComponentType==D3D_REGISTER_COMPONENT_FLOAT32?"":"nointerpolation ")+
            type+std::to_string(count)+" f"+std::to_string(i)+":"+p.SemanticName+std::to_string(p.SemanticIndex)+";\n";
    }
    if(position<0 || right<0) return {false,{},"Both primary and secondary clip positions are required"};
    std::string source="struct Input {\n"+fields+"};\nstruct Output {\n"+fields+
        "uint slice:SV_RenderTargetArrayIndex;};\n[maxvertexcount(6)]\n"
        "void main(triangle Input v[3],inout TriangleStream<Output> stream){"
        "for(uint eye=0;eye<2;eye++){for(uint i=0;i<3;i++){Output o;";
    for(UINT i=0;i<shader.OutputParameters;i++) source+="o.f"+std::to_string(i)+"=v[i].f"+std::to_string(i)+";";
    source+="if(eye==1)o.f"+std::to_string(position)+(xOnly?".x":"")+"=v[i].f"+std::to_string(right)+
        ";o.slice=eye;stream.Append(o);}stream.RestartStrip();}}";
    return {true,source,{}};
}
}
