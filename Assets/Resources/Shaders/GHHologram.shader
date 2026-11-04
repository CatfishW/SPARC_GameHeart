Shader "GameHeart/Hologram"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (2,0.3,0.5,1)
        [HDR] _Rim ("Rim", Color) = (4,1.5,2,1)
        _Pulse ("Pulse", Float) = 0
        _Alpha ("Alpha", Range(0,1)) = 1
        _MainTex ("Albedo", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "GHCommon.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _Color; float4 _Rim; float _Pulse; float _Alpha; float4 _MainTex_ST;
            CBUFFER_END
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 wp : TEXCOORD1; float2 uv : TEXCOORD2; float3 op : TEXCOORD3; };
            V vert(A i)
            {
                V o;
                float3 p = i.pos.xyz + i.n * _Pulse * 0.012;
                o.op = p;
                o.wp = TransformObjectToWorld(p); o.pos = TransformWorldToHClip(o.wp); o.n = TransformObjectToWorldNormal(i.n); o.uv = i.uv; return o;
            }
            half4 frag(V i) : SV_Target
            {
                float3 n = normalize(i.n);
                float3 v = normalize(_WorldSpaceCameraPos - i.wp);
                float ndv = saturate(dot(n, v));
                float fres = pow(1 - ndv, 2.2);
                half3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
                float scan = 0.75 + 0.25 * sin(i.wp.y * 22.0 - _Time.y * 6.0);
                float body = (0.18 + 0.25 * ndv) * dot(albedo, half3(0.4, 0.4, 0.4));
                float3 col = _Color.rgb * body * scan + _Rim.rgb * fres * (0.7 + 0.6 * _Pulse);
                col += _Rim.rgb * _GH_Hit * 0.25 * fres;
                return half4(col * _Alpha, 1);
            }
            ENDHLSL
        }
    }
}
