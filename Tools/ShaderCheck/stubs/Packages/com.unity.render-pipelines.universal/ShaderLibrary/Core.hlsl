// Stand-in for URP's ShaderLibrary/Core.hlsl: only the declarations this project's shaders use,
// with the real signatures, so glslang can type-check them. Not a renderer.
#ifndef STUB_URP_CORE
#define STUB_URP_CORE

#define half float
#define half2 float2
#define half3 float3
#define half4 float4
#define half3x3 float3x3
#define real float
#define real2 float2
#define real3 float3
#define real4 float4

#define PI 3.14159265359
#define TWO_PI 6.28318530718
#define HALF_PI 1.57079632679
#define INV_PI 0.31830988618

#define CBUFFER_START(name) cbuffer name {
#define CBUFFER_END };

#define UNITY_VERTEX_INPUT_INSTANCE_ID
#define UNITY_VERTEX_OUTPUT_STEREO
#define UNITY_SETUP_INSTANCE_ID(v)
#define UNITY_TRANSFER_INSTANCE_ID(a, b)
#define UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o)

#define TEXTURE2D(name) Texture2D name
#define SAMPLER(name) SamplerState name
#define SAMPLE_TEXTURE2D(tex, smp, uv) tex.Sample(smp, uv)
#define TRANSFORM_TEX(tex, name) ((tex.xy) * name##_ST.xy + name##_ST.zw)

float4x4 unity_ObjectToWorld;
float4x4 unity_WorldToObject;
float4x4 unity_MatrixVP;
float4 _Time;
float3 _WorldSpaceCameraPos;
float4 _ProjectionParams;
float4 _ScreenParams;
float4 unity_FogParams;

float3 TransformObjectToWorld(float3 p) { return mul(unity_ObjectToWorld, float4(p, 1.0)).xyz; }
float4 TransformWorldToHClip(float3 p) { return mul(unity_MatrixVP, float4(p, 1.0)); }
float4 TransformObjectToHClip(float3 p) { return TransformWorldToHClip(TransformObjectToWorld(p)); }
float3 TransformObjectToWorldNormal(float3 n) { return normalize(mul(n, (float3x3)unity_WorldToObject)); }
float3 GetWorldSpaceViewDir(float3 positionWS) { return _WorldSpaceCameraPos - positionWS; }
float3 GetCameraPositionWS() { return _WorldSpaceCameraPos; }
real ComputeFogFactor(float z) { return saturate(z * unity_FogParams.z + unity_FogParams.w); }
half3 MixFogColor(half3 color, half3 fogColor, real fogFactor) { return lerp(fogColor, color, fogFactor); }
half3 MixFog(half3 color, real fogFactor) { return color; }

#endif
