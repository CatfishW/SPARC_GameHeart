Shader "GameHeart/Cell"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.8,0.08,0.12,1)
        [HDR] _Rim ("Rim", Color) = (1.5,0.3,0.35,1)
        _Alpha ("Alpha", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-15" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "GHCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Color; float4 _Rim; float _Alpha;
            CBUFFER_END
            struct A { float4 pos : POSITION; float3 n : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 wp : TEXCOORD1; };
            V vert(A i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                V o; o.wp = TransformObjectToWorld(i.pos.xyz); o.pos = TransformWorldToHClip(o.wp); o.n = TransformObjectToWorldNormal(i.n); return o;
            }
            half4 frag(V i) : SV_Target
            {
                float3 n = normalize(i.n);
                float3 v = normalize(_WorldSpaceCameraPos - i.wp);
                float3 l = normalize(float3(0.3, 0.8, -0.5));
                float wrap = saturate((dot(n, l) + 0.6) / 1.6);
                float fres = pow(1 - saturate(dot(n, v)), 3);
                float3 col = _Color.rgb * (0.25 + 0.85 * wrap) + _Rim.rgb * fres;
                col = lerp(col, _GH_Fog.rgb, GH_FogFactor(i.wp));
                float near = saturate((distance(i.wp, _WorldSpaceCameraPos) - 7.0) / 9.0);
                return half4(col, _Alpha * near * (1 - GH_FogFactor(i.wp) * 0.6));
            }
            ENDHLSL
        }
    }
}
