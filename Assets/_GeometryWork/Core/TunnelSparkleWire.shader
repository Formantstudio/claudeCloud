// Wireframe + sparkle skin for the manifold / FlowPath tunnel meshes.
// Deliberately separate from CurvedChamber / CurvedGeometryAccent so the original chamber and
// Flowpath looks stay untouched. Curved World bend ID 1, as the rest of the geometry set.
//
// What changed, and why the old version looked like a printed grid:
//
// It drew one fixed-density UV lattice (_LatticeU x _LatticeV, pushed straight from the mesh's
// own side/ring counts). A parametric manifold's UV->world Jacobian varies by orders of magnitude
// across a single mesh — the Klein bottle's figure-8 pinch against its open sweep — so one density
// is wrong everywhere at once: it smears into fat blobs where UV compresses, and falls under a
// pixel and turns to grain where UV stretches. The two failures average into flat grey mush, which
// is the "wires blow up, there is no detail" read.
//
// Now the wire is built from four things, none of which depends on the parameterisation being even:
//   1. the mesh's own edges, from the barycentric coordinate in UV1 — structure that follows the
//      actual geometry rather than the UV chart;
//   2. a hierarchical lattice whose density is picked per *pixel* (see EscherHierarchicalWire) —
//      structural bands, major wires, minor wires, and a micro level that fades in only when the
//      pixels exist to hold it;
//   3. energy travelling along the major wires, each wire on its own phase;
//   4. a geometric normal, for silhouette rim and facet shading, so the result reads as a solid
//      object instead of as a flat pattern laid over one.
//
// _LatticeU/_LatticeV keep their meaning for the callers that set them, but they are now the
// *finest* level the surface may reach, not the level it is forced to draw. Nothing can blow up,
// because on-screen cell size is what chooses the density.
Shader "PsychedelicLab/Tunnel Sparkle Wire"
{
 Properties
 {
  _WireColor      ("Wire colour", Color)                 = (0.18,0.85,0.9,1)
  _SparkColor     ("Spark colour", Color)                = (0.8,0.65,0.3,1)
  _WireOpacity    ("Wire opacity", Range(0,2))           = 0.5
  // Only still read when the mesh writes index-space UVs. Normalised UVs want (1,1).
  _UVScale        ("UV scale (1/uRange, 1/vRange)", Vector) = (1,1,0,0)
  _LatticeU       ("Finest lines around", Range(2,512))  = 16
  _LatticeV       ("Finest lines along", Range(2,512))   = 40

  [Header(Hierarchy)]
  _CellPixels     ("Target cell size (pixels)", Range(6,96))  = 24
  _WirePixels     ("Wire width (pixels)", Range(0.4,8))       = 1.4
  _Hierarchy      ("Coarse-over-fine contrast", Range(0,1))   = 0.7
  _LineVariation  ("Per-wire brightness variation", Range(0,1)) = 0.5
  _EdgeWire       ("Mesh edge wire", Range(0,2))              = 1
  _EdgePixels     ("Mesh edge width (pixels)", Range(0.4,8))  = 1.5
  _MinQuadPixels  ("Drop mesh edges below quad pixels", Range(2,64)) = 9
  _NodeGlow       ("Crossing glow", Range(0,4))               = 1.1

  [Header(Motion)]
  _Flow           ("Flow along tunnel", Range(-2,2))     = 0.12
  _Pulse          ("Travelling energy", Range(0,4))      = 1
  _PulseSpeed     ("Energy speed", Range(-4,4))          = 0.35
  _PulseSpacing   ("Energy spacing (cells)", Range(1,64))= 14

  [Header(Form)]
  _Rim            ("Silhouette rim", Range(0,4))         = 1.3
  _Shade          ("Facet shading", Range(0,1))          = 0.45

  [Header(Sparkle)]
  _Sparkle        ("Sparkle amount", Range(0,4))         = 1
  _SparkleSparsity("Sparkle sparsity", Range(0,0.99))    = 0.82
  _SparkleSnap    ("Snap sparkles to lattice nodes", Range(0,1)) = 1
  _SparkleSize    ("Sparkle size", Range(0.02,0.6))      = 0.18
  _SparkleSpeed   ("Sparkle speed", Range(0,8))          = 1.6
  _MinCellPixels  ("Fade sparkles below cell pixels", Range(1,24)) = 4
  _Glow           ("Sparkle glow", Range(0,8))           = 2.5

  _ScanTint       ("Scan recolour", Range(0,1))          = 0
  _Floor          ("Base glow", Range(0,0.2))            = 0.006
  _TriplanarScale ("Fallback triplanar scale", Range(0.1,20)) = 4
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
  Pass
  {
   Tags { "LightMode"="SRPDefaultUnlit" }
   Blend One One
   ZWrite Off
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
   #include "Assets/_GeometryWork/ShaderSets/EscherWireCore.hlsl"

   CBUFFER_START(UnityPerMaterial)
   half4 _WireColor, _SparkColor;
   float4 _UVScale;
   float _WireOpacity, _LatticeU, _LatticeV, _Flow;
   float _CellPixels, _WirePixels, _Hierarchy, _LineVariation, _EdgeWire, _EdgePixels, _MinQuadPixels, _NodeGlow;
   float _Pulse, _PulseSpeed, _PulseSpacing, _Rim, _Shade;
   float _Sparkle, _SparkleSparsity, _SparkleSnap, _SparkleSize, _SparkleSpeed;
   float _MinCellPixels, _Glow, _ScanTint, _Floor, _TriplanarScale;
   CBUFFER_END

   struct Attributes
   {
    float4 positionOS : POSITION;
    float2 uv         : TEXCOORD0;
    float3 bary       : TEXCOORD1;   // written by the chamber meshes; absent elsewhere, so zero
    UNITY_VERTEX_INPUT_INSTANCE_ID
   };
   struct Varyings
   {
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 bary       : TEXCOORD2;
    float3 positionOS : TEXCOORD3;
    float  fog        : TEXCOORD4;
    UNITY_VERTEX_OUTPUT_STEREO
   };

   Varyings vert(Attributes v)
   {
    Varyings o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.positionOS = v.positionOS.xyz;
    CURVEDWORLD_TRANSFORM_VERTEX(v.positionOS);
    o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
    o.positionCS = TransformWorldToHClip(o.positionWS);
    o.uv = v.uv;
    o.bary = v.bary;
    o.fog = ComputeFogFactor(o.positionCS.z);
    return o;
   }

   half4 frag(Varyings i) : SV_Target
   {
    float time = _Time.y;
    float2 uv = i.uv * _UVScale.xy;

    // Marching-cubes and SDF output reaches this shader with no usable chart at all, so the
    // lattice would collapse onto a single value. Detecting that per pixel — rather than per
    // material — lets one material cover both the parametric and the implicit meshes.
    float uvLive = step(1e-7, length(fwidth(uv)));

    EscherWireBundle wire = EscherHierarchicalWire(
        uv, float2(_LatticeU, _LatticeV), _WirePixels, _CellPixels, _LineVariation,
        _PulseSpeed, _PulseSpacing, _Flow, time);

    // Triplanar fallback on object space, so the shape still carries a lattice without UVs.
    float3 planarWeights = EscherGeometricPlanarWeights(i.positionWS, 4);
    float3 pg = i.positionOS * _TriplanarScale;
    // The min-cell figures are deliberately not 2: EscherStableLines' own fade is nearly a no-op at
    // that setting, so the fallback would fill on a dense mesh for the same reason the lattice did.
    float planarWire = EscherTriplanarLines(pg, planarWeights, _WirePixels * 1.1, 10)
                     + EscherTriplanarLines(pg * .25, planarWeights, _WirePixels * 1.5, 14);
    float2 planarG = EscherDominantPlane(pg * .25, planarWeights);

    // Coarse levels carry the form, fine levels carry the texture. Holding the fine levels down
    // is what keeps a dense surface readable instead of washing to a solid sheet.
    float fine = lerp(1, .32, _Hierarchy);
    float lattice = max(max(wire.bands, wire.major * lerp(1, .72, _Hierarchy)),
                        max(wire.minor * fine, wire.micro * fine * .7));
    lattice = lerp(saturate(planarWire), lattice, uvLive);

    // Mesh edges: constant screen width, and independent of the chart, so the quads read evenly
    // even where the lattice has had to coarsen. Two gates, both load-bearing. Meshes without UV1
    // feed in zeros, which would otherwise light every fragment. And the faded variant is required
    // rather than optional: these meshes run to thousands of quads, so without the quad-size fade
    // the edge term alone paints the surface solid and the wire stops having holes in it.
    float hasBary = step(.25, i.bary.x + i.bary.y + i.bary.z);
    float edges = EscherBaryEdgesFaded(i.bary, _EdgePixels, _MinQuadPixels) * hasBary * _EdgeWire;

    float structure = saturate(max(lattice, edges));

    // Form. The geometric normal is the only one these meshes offer, and it is what stops the
    // wireframe reading as a flat pattern: grazing faces rim up, faces square to the camera sit back.
    float3 n = EscherGeometricNormal(i.positionWS);
    float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
    float facing = saturate(abs(dot(n, viewDir)));
    float rim = pow(1 - facing, 3) * _Rim;
    float shade = lerp(1, .45 + .55 * facing, _Shade);

    // A sparkle is a point, so its grid has to still be fine enough that one cell *is* roughly a
    // point on screen. EscherSparkLayer's own fade only guards the dense end; at the coarse end a
    // cell can cover a large patch of surface, `round(g)` is then constant across it, and the
    // gaussian has nothing to fall off over — so it fills that patch solid at full glow.
    float2 sparkleG = lerp(planarG, wire.majorG, uvLive);
    float sparkGate = 1 - smoothstep(90, 220, 1 / max(length(fwidth(sparkleG)), 1e-6));
    float spark = EscherSparkLayer(sparkleG, 1, 0,
                                   _SparkleSparsity, _SparkleSnap, _SparkleSize, _SparkleSpeed, _MinCellPixels)
                + EscherSparkLayer(sparkleG * .25 + .37, 1.5, 11,
                                   _SparkleSparsity, _SparkleSnap, _SparkleSize, _SparkleSpeed, _MinCellPixels) * .7;
    spark *= sparkGate;

    float scan; WorldGridHighlighting_float(i.positionWS, time, scan);
    float3 tint = lerp(_WireColor.rgb, _SparkColor.rgb, saturate(scan * _ScanTint));

    float3 color = _WireColor.rgb * _Floor
                 + tint * structure * shade * _WireOpacity
                 + _SparkColor.rgb * wire.nodes * _NodeGlow * uvLive * _WireOpacity
                 + _SparkColor.rgb * wire.pulse * _Pulse * uvLive
                 // Rim rides the wire only. Giving it a base term lights the gaps between wires,
                 // which is the one thing an open wireframe cannot afford.
                 + _WireColor.rgb * rim * structure
                 + _SparkColor.rgb * spark * _Sparkle * _Glow;

    color = MixFogColor(color, half3(0,0,0), i.fog);
    return half4(color, 1);
   }
   ENDHLSL
  }
 }
 Fallback Off
}
