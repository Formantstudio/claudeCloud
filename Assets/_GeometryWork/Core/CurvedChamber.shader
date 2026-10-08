Shader "PsychedelicLab/Curved Geometry Chamber"
{
 Properties { _WireOpacity("Wire opacity",Range(0,1))=.5 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
  Pass
  {
   Tags { "LightMode"="SRPDefaultUnlit" }
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.5
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #pragma multi_compile_local CURVEDWORLD_BEND_TYPE_CLASSICRUNNER_X_POSITIVE CURVEDWORLD_BEND_TYPE_LITTLEPLANET_Y CURVEDWORLD_BEND_TYPE_CYLINDRICALTOWER_X CURVEDWORLD_BEND_TYPE_CYLINDRICALROLLOFF_Z CURVEDWORLD_BEND_TYPE_TWISTEDSPIRAL_X_POSITIVE CURVEDWORLD_BEND_TYPE_TWISTEDSPIRAL_Z_POSITIVE
   #define CURVEDWORLD_BEND_ID_1
   #include "Assets/Amazing Assets/Curved World/Shaders/Core/CurvedWorldTransform.cginc"
   #include "Assets/PsychedelicLab/Shaders/WorldGridScan.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float _WireOpacity;
   CBUFFER_END
   struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float3 bary:TEXCOORD1; };
   struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 bary:TEXCOORD1; float3 positionWS:TEXCOORD2; };
   V vert(A v) { V o; CURVEDWORLD_TRANSFORM_VERTEX(v.positionOS); o.positionWS=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.uv=v.uv;o.bary=v.bary;return o; }
   half4 frag(V i):SV_Target
   {
    float3 edge=smoothstep(0,fwidth(i.bary)*1.25,i.bary);
    float wire=1-min(edge.x,min(edge.y,edge.z));
    float scan; WorldGridHighlighting_float(i.positionWS,_Time.y,scan);
    float3 color=float3(.004,.008,.014)+wire*_WireOpacity*lerp(float3(.035,.19,.23),float3(.3,.19,.055),scan);
    return half4(color,1);
   }
   ENDHLSL
  }
 }
}

