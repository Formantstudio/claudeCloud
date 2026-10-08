Shader "PsychedelicLab/Geometry Additive"
{
 Properties {
  _MainTex ("Particle Texture", 2D) = "white" {}
  _TintColor ("Tint Color", Color) = (.5,.5,.5,.5)
  _InvFade ("Soft intersection strength", Range(.01,3)) = 1
 }
 SubShader {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
  Pass {
   Tags { "LightMode"="SRPDefaultUnlit" }
   Blend SrcAlpha One
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
   float4 _MainTex_ST;
   half4 _TintColor;
   float _InvFade;
   CBUFFER_END
   struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; float fog:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
   Varyings vert(Attributes v) {
    Varyings o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
    o.uv=TRANSFORM_TEX(v.uv,_MainTex); o.color=v.color;
    o.fog=ComputeFogFactor(o.positionCS.z); return o;
   }
   half4 frag(Varyings i):SV_Target {
    half4 tex=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
    half4 color=2*i.color*_TintColor*tex;
    color.rgb=MixFogColor(color.rgb,half3(0,0,0),i.fog);
    return color;
   }
   ENDHLSL
  }
 }
}
