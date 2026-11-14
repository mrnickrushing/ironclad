// Unlit, vertex colour times _Color times a texture: tracers, muzzle flashes, smoke, dust, rings on the ground.
Shader "Ironclad/FxAlpha"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} _Color ("Color", Color) = (1, 1, 1, 1) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 p : POSITION; float2 uv : TEXCOORD0; float4 c : COLOR; };
            struct V { float4 p : SV_POSITION; float2 uv : TEXCOORD0; float4 c : COLOR; };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST; float4 _Color;
            CBUFFER_END
            V vert(A i) { V o; o.p = TransformObjectToHClip(i.p.xyz); o.uv = TRANSFORM_TEX(i.uv, _MainTex); o.c = i.c * _Color; return o; }
            half4 frag(V i) : SV_Target { return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.c; }
            ENDHLSL
        }
    }
}
