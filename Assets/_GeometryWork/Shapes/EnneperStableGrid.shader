// Wireframe + sparkle skin for the Shapes FlowPath tunnel meshes.
// Deliberately separate from CurvedChamber / CurvedGeometryAccent so the original
// chamber and Flowpath looks stay untouched. Same ingredients as the geometry combo:
// barycentric-free lattice wire, WorldGridScan recolouring, Curved World bend ID 1.
Shader "PsychedelicLab/Enneper Stable Grid"
{
 Properties
 {
  _WorldCellSize ("World grid cell size", Range(0.05,2)) = 0.3
  _LinePixels ("Line width in pixels", Range(0.5,3)) = 1.15
  _WireColor      ("Wire colour", Color)                 = (0.18,0.85,0.9,1)
  _SparkColor     ("Spark colour", Color)                = (0.8,0.65,0.3,1)
  _WireOpacity    ("Wire opacity", Range(0,2))           = 0.5
  // Set this to (1/uvRangeU, 1/uvRangeV) when the mesh writes index-space UVs.
  // CurvedGeometryChamber does: its UV0 runs 0..sides and 0..rings, not 0..1.
  _UVScale        ("UV scale (1/uRange, 1/vRange)", Vector) = (1,1,0,0)
  _LatticeU       ("Lattice lines around", Range(2,256)) = 16
  _LatticeV       ("Lattice lines along", Range(2,256))  = 40
  _LineWidth      ("Line width", Range(0.001,0.25))      = 0.03
  _Flow           ("Flow along tunnel", Range(-2,2))     = 0
  _Sparkle        ("Sparkle amount", Range(0,4))         = 1
  _SparkleSparsity("Sparkle sparsity", Range(0,0.99))    = 0.82
  _SparkleSnap    ("Snap sparkles to lattice nodes", Range(0,1)) = 1
  _SparkleSize    ("Sparkle size", Range(0.02,0.6))      = 0.18
  _SparkleSpeed   ("Sparkle speed", Range(0,8))          = 1.6
  _MinCellPixels  ("Fade below cell pixels", Range(1,24))= 4
  _Glow           ("Sparkle glow", Range(0,8))           = 2.5
  _ScanTint       ("Scan recolour", Range(0,1))          = 1
  _Floor          ("Base glow", Range(0,0.2))            = 0.006
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" "IgnoreProjector"="True" }
  Pass
  {
   Tags { "LightMode"="SRPDefaultUnlit" }
   Blend One Zero
   ZWrite On
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.5
   #pragma multi_compile_instancing
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #pragma multi_compile_local CURVEDWORLD_BEND_TYPE_CLASSICRUNNER_X_POSITIVE CURVEDWORLD_BEND_TYPE_LITTLEPLANET_Y CURVEDWORLD_BEND_TYPE_CYLINDRICALTOWER_X CURVEDWORLD_BEND_TYPE_CYLINDRICALROLLOFF_Z CURVEDWORLD_BEND_TYPE_TWISTEDSPIRAL_X_POSITIVE CURVEDWORLD_BEND_TYPE_TWISTEDSPIRAL_Z_POSITIVE
   #define CURVEDWORLD_BEND_ID_1
   #include "Assets/Amazing Assets/Curved World/Shaders/Core/CurvedWorldTransform.cginc"
   #include "Assets/PsychedelicLab/Shaders/WorldGridScan.hlsl"

   CBUFFER_START(UnityPerMaterial)
   half4 _WireColor, _SparkColor;
   float4 _UVScale; float _WorldCellSize, _LinePixels;
   float _WireOpacity, _LatticeU, _LatticeV, _LineWidth, _Flow;
   float _Sparkle, _SparkleSparsity, _SparkleSnap, _SparkleSize, _SparkleSpeed;
   float _MinCellPixels, _Glow, _ScanTint, _Floor;
   CBUFFER_END

   struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 positionWS:TEXCOORD1; float fog:TEXCOORD2; float3 gridPosition:TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };

   Varyings vert(Attributes v)
   {
    Varyings o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.gridPosition = TransformObjectToWorld(v.positionOS.xyz);
    CURVEDWORLD_TRANSFORM_VERTEX(v.positionOS);
    o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
    o.positionCS = TransformWorldToHClip(o.positionWS);
    o.uv = v.uv;
    o.fog = ComputeFogFactor(o.positionCS.z);
    return o;
   }

   float2 Hash22(float2 c)
   {
    float3 p = float3(c.xy, c.x + c.y * 7.0);
    p = frac(p * float3(443.897, 441.423, 437.195));
    p += dot(p, p.yzx + 19.19);
    return frac(float2(p.x + p.y, p.y + p.z) * p.z);
   }

   // Fades a cell grid out before it goes sub-pixel. Without this a dense grid
   // saturates every fragment and reads as grain rather than as lines or points.
   float CellFade(float2 g)
   {
    float px = 1.0 / max(length(fwidth(g)), 1e-6);
    return saturate((px - _MinCellPixels) / max(_MinCellPixels, 1e-4));
   }

   // One twinkling point per lattice *node*, not per cell, so sparkles land on the
   // wire intersections instead of scattering across the faces. Most nodes are left
   // dark by the sparsity gate; that is what keeps it sparse instead of noisy.
   float SparkLayer(float2 g, float sizeScale, float seed)
   {
    float2 node = round(g);
    float2 d = g - node;
    float2 h = Hash22(node + seed);
    if (h.x < _SparkleSparsity) return 0;

    float2 offset = (h - 0.5) * 0.9 * (1.0 - _SparkleSnap);
    float dist = length(d - offset) / max(_SparkleSize * sizeScale, 1e-4);
    float point_ = exp(-dist * dist * 5.0);

    float phase = (h.x + h.y * 3.1) * 6.2831853;
    float twinkle = 0.5 + 0.5 * sin(_Time.y * _SparkleSpeed * 6.2831853 + phase);
    return point_ * twinkle * twinkle * twinkle * CellFade(g);
   }

   float StableLines(float2 g)
   {
    float2 derivative = max(fwidth(g), 1e-5);
    float2 distancePixels = abs(frac(g + .5) - .5) / derivative;
    // Not named `line`: reserved HLSL word (geometry-shader primitive type), fails to parse.
    float2 lineMask = 1 - smoothstep(max(0, _LinePixels * .5 - .5), _LinePixels * .5 + .5, distancePixels);
    // Fade each direction independently before its cells become subpixel.
    lineMask *= smoothstep(2, 5, 1 / derivative);
    return max(lineMask.x, lineMask.y);
   }
   half4 frag(Varyings i):SV_Target
   {
    float3 g = i.gridPosition / max(_WorldCellSize, .01);
    float3 n = abs(cross(ddx(i.gridPosition), ddy(i.gridPosition)));
    n /= max(max(n.x,n.y),max(n.z,1e-6));
    float3 weights = pow(n, 8);
    weights /= max(dot(weights,1),1e-6);
    float wire = dot(weights, float3(StableLines(g.yz),StableLines(g.xz),StableLines(g.xy)));
    float scan; WorldGridHighlighting_float(i.positionWS, _Time.y, scan);
    float3 tint = lerp(_WireColor.rgb,_SparkColor.rgb,saturate(scan * _ScanTint));
    float3 color = _WireColor.rgb * _Floor + tint * wire * _WireOpacity;
    return half4(MixFogColor(color,half3(0,0,0),i.fog),1);
   }
   ENDHLSL
  }
 }
 Fallback Off
}

