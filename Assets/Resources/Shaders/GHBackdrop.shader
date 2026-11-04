Shader "GameHeart/Backdrop"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
        _MainTex2 ("Texture (crossfade)", 2D) = "black" {}
        _Blend ("Crossfade", Range(0,1)) = 0
        [HDR] _Tint ("Tint", Color) = (1,1,1,1)
        _Exposure ("Exposure", Float) = 1
        _Zoom ("Zoom", Float) = 1
        _BeatGain ("Beat gain", Float) = 0.15
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Background+10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "GHCommon.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_MainTex2); SAMPLER(sampler_MainTex2);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST; float4 _MainTex2_ST; float _Blend; float4 _Tint; float _Exposure; float _Zoom; float _BeatGain;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = i.uv; return o; }
            half4 frag(V i) : SV_Target
            {
                float z = _Zoom * (1 + _GH_Beat * 0.012);
                float2 uv = (i.uv - 0.5) / z + 0.5;
                // slow swirl toward the vanishing point
                float2 c = uv - 0.5;
                float r = length(c);
                float ang = 0.015 * sin(_GH_Time * 0.2) * (1 - r);
                float s = sin(ang), co = cos(ang);
                uv = float2(c.x * co - c.y * s, c.x * s + c.y * co) + 0.5;
                half3 a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
                half3 b = SAMPLE_TEXTURE2D(_MainTex2, sampler_MainTex2, uv).rgb;
                half3 col = lerp(a, b, _Blend);
                float lum = dot(col, half3(0.3, 0.59, 0.11));
                col *= _Tint.rgb * _Exposure * (1 + (_GH_Beat * _BeatGain + _GH_Hit * 0.08) * smoothstep(0.15, 0.8, lum));
                // darken the reading lane so notes stay legible
                float lane = smoothstep(0.30, 0.0, abs(i.uv.x - 0.5)) * smoothstep(0.62, 0.05, i.uv.y);
                col *= 1 - lane * 0.35;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
