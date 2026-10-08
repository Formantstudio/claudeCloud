// F4 — hyperspace depth. The wire shader for anything that arrives here by projection from 4-D.
//
// The problem it solves: `Projection4D.Stereographic/Perspective/Orthographic` turns a 4-D point into
// a 3-D one and **throws the w coordinate away**. After that a strut that is near in W and one that is
// far in W are drawn identically, so a tesseract looks like a cube in a cube, a 120-cell looks like
// wire wool, and the whole point of the shape is lost. Perspective foreshortening cannot recover it,
// because foreshortening encodes depth in Z, and W is a different axis.
//
// So the mesh hands w through in TEXCOORD2.w and this shader uses it for colour, brightness and
// optional slicing. `_WChannel` set to Off makes it behave as an ordinary wire, which is what happens
// automatically on a mesh that writes no TEXCOORD2 (the channel reads zero), so this shader is safe to
// assign before the mesh builders are updated — it simply looks like EscherShapeWire until they are.
//
// The slice mode is the direct tie to the portal system: a W slab of fixed width centred on `_WSlice`
// is exactly "same position in 3-space, different slice of the 4th dimension". Animating `_WSlice` is
// the camera-free dimensional travel the portals are built around — nothing moves in 3-D, the visible
// slab changes.
//
// Reads two globals published by `Hyperspace4DAxis` when its `publishToShaders` is on:
//   _Hyper4DParams = (wDistance, cameraW * activation, projection, scale)
//   _Hyper4DActivation = 0 at rest, 1 fully engaged
// Both are optional — zero is a valid neutral.
Shader "PsychedelicLab/Escher Hyper Wire"
{
    Properties
    {
        [KeywordEnum(UV, Triplanar, Bary)]
        _GridSpace      ("Grid space", Float) = 2

        [Header(Hyperspace)]
        [KeywordEnum(Off, Vertex, Follow)]
        _WChannel       ("W source", Float) = 1
        _WRange         ("W extent (|w| at the far shell)", Range(0.01, 8)) = 1
        _WNearColor     ("Near in W", Color)                = (0.18, 0.92, 1.0, 1)
        _WFarColor      ("Far in W", Color)                 = (0.95, 0.35, 0.75, 1)
        _WFade          ("Dim far in W", Range(0, 1))       = 0.55
        _WBands         ("W shell bands (0 = smooth)", Range(0, 24)) = 0

        [Header(W slice)]
        [Toggle(_WSLICE_ON)]
        _WSliceOn       ("Isolate a W slab", Float) = 0
        _WSlice         ("Slab centre in W", Range(-4, 4))  = 0
        _WSliceWidth    ("Slab width in W", Range(0.01, 4)) = 0.5
        _WSliceSoft     ("Slab edge softness", Range(0, 1)) = 0.35
        _WOutside       ("Keep outside the slab", Range(0, 1)) = 0.08

        [Header(Activation)]
        _ActivationBoost ("Brighten with 4D activation", Range(0, 3)) = 1
        _ActivationFloor ("Brightness at rest", Range(0, 1)) = 1

        [Header(Colour)]
        _SparkColor     ("Spark colour", Color)             = (1, 0.9, 0.65, 1)
        _WireOpacity    ("Wire opacity", Range(0, 2))       = 0.55
        _Floor          ("Base glow", Range(0, 0.2))        = 0.006
        _ScanTint       ("WorldGridScan recolour", Range(0, 1)) = 0.5
        _Glow           ("Sparkle glow", Range(0, 8))       = 2.5

        [Header(Line)]
        _LinePixels     ("Line width in pixels", Range(0.5, 4)) = 1.15
        _MinCellPixels  ("Fade below cell pixels", Range(1, 24)) = 4
        _BaryWidth      ("Edge width (Bary mode)", Range(0.2, 6)) = 1.25

        [Header(UV mode)]
        _UVScale        ("UV scale (1/uRange, 1/vRange)", Vector) = (1, 1, 0, 0)
        _LatticeU       ("Lattice lines across", Range(2, 256)) = 24
        _LatticeV       ("Lattice lines along", Range(2, 256))  = 24
        _Flow           ("Flow along V", Range(-2, 2))      = 0.12

        [Header(Triplanar mode)]
        _WorldCellSize  ("World grid cell size", Range(0.02, 4)) = 0.3
        _PlanarSharpness ("Planar blend sharpness", Range(1, 16)) = 8

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
            #pragma shader_feature_local _WCHANNEL_OFF _WCHANNEL_VERTEX _WCHANNEL_FOLLOW
            #pragma shader_feature_local _WSLICE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #pragma multi_compile_local CURVEDWORLD_BEND_TYPE_CLASSICRUNNER_X_POSITIVE CURVEDWORLD_BEND_TYPE_LITTLEPLANET_Y CURVEDWORLD_BEND_TYPE_CYLINDRICALTOWER_X CURVEDWORLD_BEND_TYPE_CYLINDRICALROLLOFF_Z CURVEDWORLD_BEND_TYPE_TWISTEDSPIRAL_X_POSITIVE CURVEDWORLD_BEND_TYPE_TWISTEDSPIRAL_Z_POSITIVE
            #define CURVEDWORLD_BEND_ID_1
            #include "Assets/Amazing Assets/Curved World/Shaders/Core/CurvedWorldTransform.cginc"
            #include "Assets/PsychedelicLab/Shaders/WorldGridScan.hlsl"
            #include "Assets/_GeometryWork/ShaderSets/EscherWireCore.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _WNearColor, _WFarColor, _SparkColor;
                float4 _UVScale;
                float _GridSpace, _WChannel, _WSliceOn;
                float _WRange, _WFade, _WBands;
                float _WSlice, _WSliceWidth, _WSliceSoft, _WOutside;
                float _ActivationBoost, _ActivationFloor;
                float _WireOpacity, _Floor, _ScanTint, _Glow;
                float _LinePixels, _MinCellPixels, _BaryWidth;
                float _LatticeU, _LatticeV, _Flow;
                float _WorldCellSize, _PlanarSharpness;
                float _Sparkle, _SparkleSparsity, _SparkleSnap, _SparkleSize, _SparkleSpeed;
            CBUFFER_END

            // Published by Hyperspace4DAxis. Deliberately outside UnityPerMaterial: they are
            // Shader.SetGlobal* values, and a global declared inside the per-material CBUFFER
            // silently reads whatever the material happens to hold instead.
            float4 _Hyper4DParams;
            float  _Hyper4DActivation;

            #define SPARK(g, sizeScale, seed) EscherSparkLayer(g, sizeScale, seed, \
                _SparkleSparsity, _SparkleSnap, _SparkleSize, _SparkleSpeed, _MinCellPixels)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 bary       : TEXCOORD1;
                // (sideSign, fieldValue, iteration, w) — the shared extra channel. A mesh that does
                // not write it reads zero here, which this shader treats as "mid-W, no banding".
                float4 shapeData  : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 gridPosition : TEXCOORD2;
                float3 bary         : TEXCOORD3;
                float  w            : TEXCOORD4;
                float  fog          : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Before the bend: the pattern belongs to the shape, not to the deformation.
                o.gridPosition = TransformObjectToWorld(v.positionOS.xyz);

                CURVEDWORLD_TRANSFORM_VERTEX(v.positionOS);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                o.bary = v.bary;

            #if defined(_WCHANNEL_VERTEX)
                o.w = v.shapeData.w;
            #elif defined(_WCHANNEL_FOLLOW)
                // Fallback for meshes that genuinely have no w: track the camera's own W offset, so
                // the shape still responds to the 4-D rig even though it cannot be shaded per-vertex.
                // Honest about what it is — a global tint, not real hyperspace depth.
                o.w = _Hyper4DParams.y;
            #else
                o.w = 0;
            #endif

                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float wire = 0;
                float spark = 0;

            #if defined(_GRIDSPACE_BARY)
                wire = EscherBaryEdges(i.bary, _BaryWidth);
                float3 gb = i.gridPosition / max(_WorldCellSize, .02);
                spark = SPARK(gb.xy, 1.0, 0.0);
            #elif defined(_GRIDSPACE_UV)
                float2 g = i.uv * _UVScale.xy * float2(_LatticeU, _LatticeV)
                         + float2(0, _Time.y * _Flow * _LatticeV);
                wire = EscherStableLines(g, _LinePixels, _MinCellPixels);
                spark = SPARK(g, 1.0, 0.0);
            #else
                float3 g = i.gridPosition / max(_WorldCellSize, .02);
                float3 weights = EscherGeometricPlanarWeights(i.gridPosition, _PlanarSharpness);
                wire = EscherTriplanarLines(g, weights, _LinePixels, _MinCellPixels);
                spark = SPARK(EscherDominantPlane(g, weights), 1.0, 0.0);
            #endif

                // --- hyperspace depth ------------------------------------------------------------
                // 0 at the far-negative shell, 1 at the far-positive one. Clamped rather than
                // wrapped: w outside the stated extent should read as "as far as it goes", not fold
                // back and alias with the near shell.
                float wNorm = saturate(i.w / max(_WRange, .01) * .5 + .5);

                // Quantising wNorm makes the concentric shells of a polytope separable. A 120-cell
                // has genuinely distinct cell shells; smooth gradient blends them into one fog.
                float wBanded = _WBands >= 1
                    ? floor(wNorm * _WBands) / max(_WBands - 1, 1)
                    : wNorm;

                float3 wTint = lerp(_WNearColor.rgb, _WFarColor.rgb, wBanded);

                // Far in W recedes. |wNorm - .5| * 2 is distance from the hyperplane the camera sits
                // in, so struts in the camera's own W slice stay brightest regardless of sign.
                float wDepth = abs(wNorm - .5) * 2;
                float wDim = lerp(1, 1 - wDepth, _WFade);

            #if defined(_WSLICE_ON)
                // A slab of W, which is what a portal to another slice actually shows: identical
                // 3-D position, different w. Outside the slab is kept at `_WOutside` rather than
                // discarded, so the rest of the structure stays as a ghost for context.
                float slabEdge = _WSliceWidth * .5;
                float soft = max(slabEdge * _WSliceSoft, 1e-4);
                float inside = 1 - smoothstep(slabEdge - soft, slabEdge + soft, abs(i.w - _WSlice));
                wDim *= lerp(_WOutside, 1, inside);
            #endif

                // The 4-D rig's activation envelope: neutral at rest by default
                // (_ActivationFloor 1), and able to bloom as the rotation engages.
                float activation = 1 + _Hyper4DActivation * _ActivationBoost;
                activation *= lerp(1, _ActivationFloor, 1 - saturate(_Hyper4DActivation));

                float scan;
                WorldGridHighlighting_float(i.positionWS, _Time.y, scan);
                float3 tint = lerp(wTint, _SparkColor.rgb, saturate(scan * _ScanTint));

                float3 colour = wTint * _Floor
                              + tint * wire * _WireOpacity * wDim * activation
                              + _SparkColor.rgb * spark * _Sparkle * _Glow * wDim;

                colour = MixFogColor(colour, half3(0, 0, 0), i.fog);
                return half4(colour, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
