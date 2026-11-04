Shader "GameHeart/Tunnel"
{
    Properties
    {
        _RingSpacing ("Ring spacing", Float) = 8
        _Intensity ("Intensity", Float) = 1
        _NearFade ("Near fade", Float) = 6
        _FarFade ("Far fade", Float) = 140
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-20" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "GHCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _RingSpacing; float _Intensity; float _NearFade; float _FarFade;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wp : TEXCOORD1; };
            V vert(A i)
            {
                V o; o.wp = TransformObjectToWorld(i.pos.xyz);
                // breathe the vessel with the beat
                float3 c = float3(0, o.wp.y, 0);
                o.pos = TransformWorldToHClip(o.wp);
                o.uv = i.uv; return o;
            }
            half4 frag(V i) : SV_Target
            {
                float z = i.wp.z + _GH_Scroll;
                float ang = i.uv.x; // 0..1 around the tube
                // rings that travel toward the camera
                float rp = frac(z / _RingSpacing);
                float ring = exp(-pow((rp - 0.5) * 9.0, 2)) * (0.55 + 0.8 * _GH_Beat);
                // organic veins
                float2 vp = float2(ang * 18.0, z * 0.045);
                float n = GH_Fbm(vp + float2(0, -_GH_Time * 0.05));
                float vein = smoothstep(0.035, 0.0, abs(n - 0.5)) * (0.35 + 0.65 * _GH_Energy);
                // longitudinal light streaks
                float streak = pow(saturate(sin(ang * 6.2831 * 12 + 1.3) * 0.5 + 0.5), 24) * 0.25;
                float3 col = _GH_ColorA.rgb * ring + _GH_ColorB.rgb * (vein * 0.9 + streak);
                col += _GH_ColorB.rgb * _GH_Hit * 0.25 * ring;
                float d = distance(i.wp, _WorldSpaceCameraPos);
                float fade = smoothstep(2.0, _NearFade, d) * (1 - smoothstep(_FarFade * 0.45, _FarFade, d));
                // keep the floor area (highway) dimmer
                float floorMask = lerp(0.35, 1.0, smoothstep(0.62, 0.9, abs(ang - 0.5) * 2.0 + 0.5));
                return half4(col * fade * _Intensity * floorMask, 1);
            }
            ENDHLSL
        }
    }
}
