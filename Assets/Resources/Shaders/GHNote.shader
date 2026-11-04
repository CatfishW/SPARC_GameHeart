Shader "GameHeart/Note"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (2,0.5,0.7,1)
        [HDR] _Rim ("Rim", Color) = (3,2,2,1)
        _Alpha ("Alpha", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+5" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "GHCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Color; float4 _Rim; float _Alpha;
            CBUFFER_END
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 wp : TEXCOORD1; float2 uv : TEXCOORD2; };
            V vert(A i) { V o; o.wp = TransformObjectToWorld(i.pos.xyz); o.pos = TransformWorldToHClip(o.wp); o.n = TransformObjectToWorldNormal(i.n); o.uv = i.uv; return o; }
            half4 frag(V i) : SV_Target
            {
                float3 n = normalize(i.n);
                float3 v = normalize(_WorldSpaceCameraPos - i.wp);
                float fres = pow(1 - saturate(dot(n, v)), 2.5);
                float top = saturate(n.y);
                // glassy top gradient + bright core stripe
                float stripe = exp(-pow((i.uv.y - 0.5) * 5.0, 2)) * top;
                float3 col = _Color.rgb * (0.35 + 0.65 * top) + _Rim.rgb * (fres * 0.8 + stripe * 0.6);
                col *= 1 + _GH_Beat * 0.15;
                return half4(col, _Alpha);
            }
            ENDHLSL
        }
    }
}
