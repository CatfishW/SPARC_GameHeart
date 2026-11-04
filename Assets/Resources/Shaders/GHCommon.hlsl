#ifndef GH_COMMON_INCLUDED
#define GH_COMMON_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
// Globals driven by StageWorld every frame.
float _GH_Time;      // song time in seconds
float _GH_Beat;      // 0..1 decaying pulse on each beat
float _GH_Hit;       // 0..1 decaying pulse on each successful hit
float _GH_Energy;    // 0..1 musical energy
float _GH_Scroll;    // world units scrolled
float4 _GH_ColorA;   // theme primary (HDR)
float4 _GH_ColorB;   // theme secondary (HDR)
float4 _GH_Fog;      // fog colour
float _GH_FogStart;
float _GH_FogEnd;

float GH_FogFactor(float3 worldPos)
{
    float d = distance(worldPos, _WorldSpaceCameraPos);
    return saturate((d - _GH_FogStart) / max(1.0, _GH_FogEnd - _GH_FogStart));
}

float GH_Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
float GH_Noise(float2 p)
{
    float2 i = floor(p); float2 f = frac(p);
    float a = GH_Hash(i), b = GH_Hash(i + float2(1, 0)), c = GH_Hash(i + float2(0, 1)), d = GH_Hash(i + float2(1, 1));
    float2 u = f * f * (3 - 2 * f);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}
float GH_Fbm(float2 p)
{
    float v = 0, a = 0.5;
    for (int k = 0; k < 4; k++) { v += a * GH_Noise(p); p *= 2.03; a *= 0.5; }
    return v;
}
#endif
