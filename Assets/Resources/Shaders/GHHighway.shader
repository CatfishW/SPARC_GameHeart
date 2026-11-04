Shader "GameHeart/Highway"
{
    Properties
    {
        _Lanes ("Lanes", Float) = 4
        _Length ("Length", Float) = 70
        [HDR] _Edge ("Edge colour", Color) = (2,0.4,0.6,1)
        [HDR] _Line ("Divider colour", Color) = (1,0.6,0.7,1)
        _Base ("Base colour", Color) = (0.02,0.01,0.02,0.82)
        _BeatSpacing ("Beat line spacing", Float) = 10
        _Press ("Lane press (x,y,z,w)", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-5" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "GHCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Lanes; float _Length; float4 _Edge; float4 _Line; float4 _Base; float _BeatSpacing; float4 _Press;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wp : TEXCOORD1; };
            V vert(A i) { V o; o.wp = TransformObjectToWorld(i.pos.xyz); o.pos = TransformWorldToHClip(o.wp); o.uv = i.uv; return o; }
            half4 frag(V i) : SV_Target
            {
                float x = i.uv.x;             // 0..1 across
                float zf = i.uv.y;            // 0 at judgement line .. 1 far end
                float lanePos = x * _Lanes;
                float laneIdx = floor(lanePos);
                float inLane = frac(lanePos);
                float aa = fwidth(lanePos) * 1.5;
                float divider = 1 - smoothstep(0.0, 0.02 + aa, min(inLane, 1 - inLane));
                float edge = smoothstep(0.03 + aa, 0.0, min(x, 1 - x));
                float edgeGlow = exp(-min(x, 1 - x) * 40.0);
                // beat lines travelling toward the player
                float z = zf * _Length;
                float bp = frac((z + _GH_Scroll) / _BeatSpacing);
                float beatLine = exp(-pow((bp - 0.5) * 30.0, 2)) * 0.12;
                // pressed-lane glow
                float press = laneIdx < 0.5 ? _Press.x : laneIdx < 1.5 ? _Press.y : laneIdx < 2.5 ? _Press.z : _Press.w;
                float laneLight = press * (1 - smoothstep(0.0, 0.55, zf)) * (0.6 + 0.4 * sin(inLane * 3.14159));
                float3 col = _Base.rgb;
                col += _Line.rgb * divider * 0.22;
                col += _Edge.rgb * (edge * 0.9 + edgeGlow * 0.18) * (0.8 + 0.5 * _GH_Beat);
                col += _GH_ColorB.rgb * 0.25 * beatLine * (0.6 + _GH_Energy);
                col += _GH_ColorA.rgb * laneLight * 0.18;
                // subtle hex scanlines
                col += _GH_ColorA.rgb * 0.004 * step(0.5, frac(z * 0.5 - _GH_Time * 2.0));
                float a = _Base.a + divider * 0.25 + edge * 0.5 + beatLine * 0.3 + laneLight * 0.3;
                float far = 1 - smoothstep(0.55, 1.0, zf);
                float nearFade = smoothstep(-0.12, 0.0, zf);
                return half4(col, saturate(a) * far * nearFade);
            }
            ENDHLSL
        }
    }
}
