// One wire shader for every shape family, with the **grid space** as the axis of variation.
//
// Why that is the right axis, and not colour or density: a lattice drawn in UV space has cells
// whose size follows the surface's parameterisation. On a cylinder or a torus that is uniform and
// desirable — the lines land on the mesh's own rings and sides. On Enneper, Kuen, Breather or a
// supershape the parameterisation stretches enormously toward the lobes, so a UV lattice gives
// cells that are dense in the middle and smeared at the edges. On a marching-cubes surface there
// is no parameterisation at all. On a stereographically projected 4-D shape the UV stretches
// without bound near the projection pole.
//
// So the mode is chosen by what the shape's UVs are actually like:
//
//   UV         - the mesh's own grid is meaningful and should be seen (chamber rings and sides,
//                accordion bands, manifold grids with even parameterisation)
//   TRIPLANAR  - uniform cells regardless of parameterisation; also the only option when there
//                are no usable UVs (implicit/marching-cubes surfaces, 4-D projections, Enneper)
//   BARY       - the real triangle/quad edges rather than a lattice, for low-poly figures such as
//                polytope struts where the edge itself is the subject
//
// Two techniques here come from Codex's EnneperStableGrid and are applied to all three modes:
//
//  1. **Line width in pixels.** `|frac(g) - .5| / fwidth(g)` gives distance to the line measured
//     in pixels, so a line is the same thickness near and far and does not thin out under
//     stretching. A plain smoothstep in UV units cannot do that.
//  2. **The grid is sampled before the bend.** `gridPosition` is taken ahead of
//     CURVEDWORLD_TRANSFORM_VERTEX, so the pattern is locked to the undeformed shape and does not
//     smear when Curved World bends it.
//
// Leaves TunnelSparkleWire, EnneperStableGrid, CurvedChamber and CurvedGeometryAccent untouched.
Shader "PsychedelicLab/Escher Shape Wire"
{
    Properties
    {
        [KeywordEnum(UV, Triplanar, Bary)]
        _GridSpace      ("Grid space", Float) = 1

        [Header(Colour)]
        _WireColor      ("Wire colour", Color)              = (0.18, 0.85, 0.9, 1)
        _SparkColor     ("Spark colour", Color)             = (0.8, 0.65, 0.3, 1)
        _WireOpacity    ("Wire opacity", Range(0, 2))       = 0.5
        _Floor          ("Base glow", Range(0, 0.2))        = 0.006
        _ScanTint       ("WorldGridScan recolour", Range(0, 1)) = 1
        _Glow           ("Sparkle glow", Range(0, 8))       = 2.5

        [Header(Line)]
        _LinePixels     ("Line width in pixels", Range(0.5, 4)) = 1.15
        _MinCellPixels  ("Fade below cell pixels", Range(1, 24)) = 4

        [Header(UV mode)]
        // Set to (1/uvRangeU, 1/uvRangeV) for a mesh writing index-space UVs.
        // CurvedGeometryChamber used to: its UV0 ran 0..sides and 0..rings, not 0..1.
        _UVScale        ("UV scale (1/uRange, 1/vRange)", Vector) = (1, 1, 0, 0)
        _LatticeU       ("Lattice lines across", Range(2, 256)) = 16
        _LatticeV       ("Lattice lines along", Range(2, 256))  = 40
        _Flow           ("Flow along V", Range(-2, 2))      = 0.12

        [Header(Triplanar mode)]
        _WorldCellSize  ("World grid cell size", Range(0.02, 4)) = 0.3
        _PlanarSharpness ("Planar blend sharpness", Range(1, 16)) = 8

        [Header(Bary mode)]
        _BaryWidth      ("Edge width", Range(0.2, 6))       = 1.25

        [Header(Sparkle)]
        _Sparkle        ("Sparkle amount", Range(0, 4))     = 1
        _SparkleSparsity("Sparkle sparsity", Range(0, 0.99)) = 0.82
        _SparkleSnap    ("Snap to grid nodes", Range(0, 1)) = 1
        _SparkleSize    ("Sparkle size", Range(0.02, 0.6))  = 0.18
        _SparkleSpeed   ("Sparkle speed", Range(0, 8))      = 1.6
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma shader_feature_local _GRIDSPACE_UV _GRIDSPACE_TRIPLANAR _GRIDSPACE_BARY

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #pragma multi_compile_local CURVEDWORLD_BEND_TYPE_CLASSICRUNNER_X_POSITIVE CURVEDWORLD_BEND_TYPE_LITTLEPLANET_Y CURVEDWORLD_BEND_TYPE_CYLINDRICALTOWER_X CURVEDWORLD_BEND_TYPE_CYLINDRICALROLLOFF_Z CURVEDWORLD_BEND_TYPE_TWISTEDSPIRAL_X_POSITIVE CURVEDWORLD_BEND_TYPE_TWISTEDSPIRAL_Z_POSITIVE
            #define CURVEDWORLD_BEND_ID_1
            #include "Assets/Amazing Assets/Curved World/Shaders/Core/CurvedWorldTransform.cginc"
            #include "Assets/PsychedelicLab/Shaders/WorldGridScan.hlsl"
            #include "Assets/_GeometryWork/ShaderSets/EscherWireCore.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _WireColor, _SparkColor;
                float4 _UVScale;
                float _GridSpace;
                float _WireOpacity, _Floor, _ScanTint, _Glow;
                float _LinePixels, _MinCellPixels;
                float _LatticeU, _LatticeV, _Flow;
                float _WorldCellSize, _PlanarSharpness;
                float _BaryWidth;
                float _Sparkle, _SparkleSparsity, _SparkleSnap, _SparkleSize, _SparkleSpeed;
            CBUFFER_END

            // Shorthand: the core's spark layer takes its knobs explicitly so it needs no globals,
            // which makes the call sites long. This binds this material's set once.
            #define SPARK(g, sizeScale, seed) EscherSparkLayer(g, sizeScale, seed,                 _SparkleSparsity, _SparkleSnap, _SparkleSize, _SparkleSpeed, _MinCellPixels)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 bary       : TEXCOORD1;   // written unwelded by the chambers
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 gridPosition : TEXCOORD2;   // pre-bend, so the grid does not smear
                float3 bary         : TEXCOORD3;
                float  fog          : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Taken before the bend on purpose: the pattern belongs to the shape, not to the
                // deformation, so it stays put when Curved World warps the geometry.
                o.gridPosition = TransformObjectToWorld(v.positionOS.xyz);

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
                float wire = 0;
                float spark = 0;

            #if defined(_GRIDSPACE_BARY)
                // The real edges. Barycentric distance in pixels, same as the lattice modes, so a
                // strut reads at any distance. The chambers add 1 to the component that vanishes
                // along a quad's shared diagonal, which keeps that diagonal out of the minimum.
                wire = EscherBaryEdges(i.bary, _BaryWidth);
                // Sparkles ride the triplanar grid, since bary has no nodes to sit on.
                float3 gb = i.gridPosition / max(_WorldCellSize, .02);
                spark = SPARK(gb.xy, 1.0, 0.0);

            #elif defined(_GRIDSPACE_UV)
                // The mesh's own grid. Lines land on real rings and sides when the lattice counts
                // match the mesh resolution.
                float2 uv = i.uv * _UVScale.xy;
                float2 g = uv * float2(_LatticeU, _LatticeV)
                         + float2(0, _Time.y * _Flow * _LatticeV);
                wire = EscherStableLines(g, _LinePixels, _MinCellPixels);
                spark = SPARK(g, 1.0, 0.0)
                      + SPARK(g * 0.25 + 0.37, 1.5, 11.0) * 0.7;

            #else
                // Triplanar world cells: uniform whatever the parameterisation does, and it needs
                // no vertex normals, so it works on marching-cubes output. The geometric normal
                // comes from the screen-space derivatives of the position.
                float3 g = i.gridPosition / max(_WorldCellSize, .02);
                float3 weights = EscherGeometricPlanarWeights(i.gridPosition, _PlanarSharpness);
                wire = EscherTriplanarLines(g, weights, _LinePixels, _MinCellPixels);
                // Sparkle on the dominant plane only, so nodes do not triple up at the seams.
                spark = SPARK(EscherDominantPlane(g, weights), 1.0, 0.0);
            #endif

                float scan;
                WorldGridHighlighting_float(i.positionWS, _Time.y, scan);
                float3 tint = lerp(_WireColor.rgb, _SparkColor.rgb, saturate(scan * _ScanTint));

                float3 colour = _WireColor.rgb * _Floor
                              + tint * wire * _WireOpacity
                              + _SparkColor.rgb * spark * _Sparkle * _Glow;

                colour = MixFogColor(colour, half3(0, 0, 0), i.fog);
                return half4(colour, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
