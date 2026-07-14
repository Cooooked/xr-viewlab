#pragma once
#include <array>
#include <cctype>
#include <string>

namespace viewlab::sticky_note {
struct WrappedText { std::array<std::string,4> lines{}; size_t count=0; };
inline WrappedText Wrap(const std::wstring& source,size_t width=24) {
    WrappedText out{};std::string clean;clean.reserve(source.size());
    bool space=false;for(wchar_t wc:source){char c=(wc>=L'a'&&wc<=L'z')?(char)(wc-L'a'+'A'):(wc>=32&&wc<=126?(char)wc:' ');if(c==' '){if(!clean.empty())space=true;continue;}if(space){clean.push_back(' ');space=false;}clean.push_back(c);if(clean.size()>=120)break;}
    size_t pos=0;while(pos<clean.size()&&out.count<out.lines.size()){size_t take=(std::min)(width,clean.size()-pos);if(pos+take<clean.size()){size_t split=clean.rfind(' ',pos+take);if(split!=std::string::npos&&split>=pos)take=split-pos;}if(take==0){++pos;continue;}out.lines[out.count++]=clean.substr(pos,take);pos+=take;while(pos<clean.size()&&clean[pos]==' ')++pos;}
    if(pos<clean.size()&&out.count){auto& last=out.lines[out.count-1];if(last.size()>width-3)last.resize(width-3);last+="...";}
    return out;
}
}
