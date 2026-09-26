#pragma once
#include <cstddef>
#include <string>
namespace viewlab::bridge::stereo {
// Compile an adapter for a restricted, reflected SPS vertex output contract.
// No driver spoofing: caller must first establish the producer's full contract.
struct ShaderAdapter { bool supported=false; std::string source; std::string reason; };
ShaderAdapter BuildGeometryAdapter(const void* bytecode, std::size_t bytes);
}
