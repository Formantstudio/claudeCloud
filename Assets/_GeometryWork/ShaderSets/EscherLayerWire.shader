// F3 + F5 — layer depth and iteration index. The wire shader for implicit surfaces: the TPMS family
// (gyroid, Schwarz P/D, Neovius, Lidinoid, Split-P, Goursat), the fractals (Mandelbulb, Mandelbox,
// Menger, Sierpinski) and the bounded algebraics (Barth sextic).
//
// Two problems, one shader, because they are the same problem wearing different hats:
//
// **F3, layer depth.** A triply periodic surface is nested sheets. Standing inside a gyroid you are
// looking through six or eight sheets at once, and a uniform wire draws all of them identically, so
// they composite into grey wool with no readable depth. Banding the colour by the sheet's own level
// makes the nesting visible, and that nesting *is* the shape.
//
// **F5, iteration index.** A fractal's structure is which iteration a point escaped at. A flat wire
// hides the one fact that distinguishes a Mandelbox from a lumpy ball. Colour by iteration and the
// self-similar scaling becomes legible.
//
// Both read the same channel: TEXCOORD2.y is the field value at the vertex, TEXCOORD2.z the iteration
// count. A mesh writing neither reads zero, and this shader then falls back to **world-height
// banding**, which still separates the sheets of a periodic surface usefully because a TPMS sheet
// stack is roughly axis-aligned. So it is worth assigning now and gets better later.
//
// `_Dislocation` exists because `EscherFields.Dislocate` can put a screw dislocation through the
// periodic family — the thing that turns a gyroid into an honest endless staircase rather than a fake
// one. When it is on, flat horizontal bands are wrong: a band has to climb with the screw or it cuts
// across the very continuity the dislocation creates. Setting this to the room's dislocation value
// shears the banding to match. It must stay 0 for bounded fields (Barth, Goursat), which do not tile
// and have no screw; `EscherFields.IsPeriodic` is the authority on which is which.
Shader "PsychedelicLab/Escher Layer Wire"
{
    Properties
    {
        [KeywordEnum(Triplanar, UV, Bary)]
        _GridSpace      ("Grid space", Float) = 0

        [Header(Layer source)]
        [KeywordEnum(World, Field, Iteration)]
        _LayerFrom      ("Layer from", Float) = 0
        _LayerAxis      ("World layer axis", Vector) = (0, 1, 0, 0)
        _LayerPeriod    ("Layer period", Range(0.02, 8))    = 0.8
        _LayerCount     ("Distinct layers", Range(1, 16))   = 5
        _LayerSharp     ("Band hardness", Range(0, 1))      = 0.65
        _Dislocation    ("Screw dislocation (periods per turn)", Range(-4, 4)) = 0
        _DislocAxis     ("Screw axis", Vector) = (0, 1, 0, 0)

        [Header(Layer colour)]
        _LayerColorA    ("Layer colour A", Color)           = (0.16, 0.90, 0.70, 1)
        _LayerColorB    ("Layer colour B", Color)           = (0.62, 0.42, 0.98, 1)
        _LayerColorC    ("Layer colour C", Color)           = (0.98, 0.62, 0.22, 1)
        _LayerMix       ("Layer colour amount", Range(0, 1)) = 0.8
        _NearLayerBoost ("Brighten the nearest layer", Range(0, 3)) = 0.8

        [Header(Iteration)]
        _IterMax        ("Iterations at full depth", Range(1, 24)) = 8
        _IterThin       ("Thin the deep iterations", Range(0, 1)) = 0.4
        _IterSparkle    ("Sparkle on the deep iterations", Range(0, 2)) = 1

        [Header(Colour)]
        _SparkColor     ("Spark colour", Color)             = (1, 0.92, 0.70, 1)
        _WireOpacity    ("Wire opacity", Range(0, 2))       = 0.45
        _Floor          ("Base glow", Range(0, 0.2))        = 0.006
        _ScanTint       ("WorldGridScan recolour", Range(0, 1)) = 0.5
        _Glow           ("Sparkle glow", Range(0, 8))       = 2.8

        [Header(Line)]
        _LinePixels     ("Line width in pixels", Range(0.5, 4)) = 1.05
        _MinCellPixels  ("Fade below cell pixels", Range(1, 24)) = 4
        _BaryWidth      ("Edge width (Bary mode)", Range(0.2, 6)) = 1.25

        [Header(UV mode)]
        _UVScale        ("UV scale (1/uRange, 1/vRange)", Vector) = (1, 1, 0, 0)
        _LatticeU       ("Lattice lines across", Range(2, 256)) = 24
        _LatticeV       ("Lattice lines along", Range(2, 256))  = 24
        _Flow           ("Flow along V", Range(-2, 2))      = 0

        [Header(Triplanar mode)]
        _WorldCellSize  ("World grid cell size", Range(0.02, 4)) = 0.22
        _PlanarSharpness ("Planar blend sharpness", Range(1, 16)) = 8

        [Header(Sparkle)]
        _Sparkle        ("Sparkle amount", Range(0, 4))     = 1
        _SparkleSparsity("Sparkle sparsity", Range(0, 0.99)) = 0.78
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
            #pragma shader_feature_local _GRIDSPACE_TRIPLANAR _GRIDSPACE_UV _GRIDSPACE_BARY
            #pragma shader_feature_local _LAYERFROM_WORLD _LAYERFROM_FIELD _LAYERFROM_ITERATION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #pragma multi_compile_local CURVEDWORLD_BEND_TYPE_CLASSICRUNNER_X_POSITIVE CURVEDWORLD_BEND_TYPE_LITTLEPLANET_Y CURVEDWORLD_BEND_TYPE_CYLINDRICALTOWER_X CURVEDWORLD_BEND_TYPE_CYLINDRICALROLLOFF_Z CURVEDWORLD_BEND_TYPE_TWISTEDSPIRAL_X_POSITIVE CURVEDWORLD_BEND_TYPE_TWISTEDSPIRAL_Z_POSITIVE
            #define CURVEDWORLD_BEND_ID_1
            #include "Assets/Amazing Assets/Curved World/Shaders/Core/CurvedWorldTransform.cginc"
            #include "Assets/PsychedelicLab/Shaders/WorldGridScan.hlsl"
            #include "Assets/_GeometryWork/ShaderSets/EscherWireCore.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _LayerColorA, _LayerColorB, _LayerColorC, _SparkColor;
                float4 _UVScale, _LayerAxis, _DislocAxis;
                float _GridSpace, _LayerFrom;
                float _LayerPeriod, _LayerCount, _LayerSharp, _Dislocation;
                float _LayerMix, _NearLayerBoost;
                float _IterMax, _IterThin, _IterSparkle;
                float _WireOpacity, _Floor, _ScanTint, _Glow;
                float _LinePixels, _MinCellPixels, _BaryWidth;
                float _LatticeU, _LatticeV, _Flow;
                float _WorldCellSize, _PlanarSharpness;
                float _Sparkle, _SparkleSparsity, _SparkleSnap, _SparkleSize, _SparkleSpeed;
            CBUFFER_END

            #define SPARK(g, sizeScale, seed) EscherSparkLayer(g, sizeScale, seed, \
                _SparkleSparsity, _SparkleSnap, _SparkleSize, _SparkleSpeed, _MinCellPixels)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 bary       : TEXCOORD1;
                float4 shapeData  : TEXCOORD2;   // (sideSign, fieldValue, iteration, w)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 gridPosition : TEXCOORD2;
                float3 bary         : TEXCOORD3;
                float2 layerData    : TEXCOORD4;   // (field value, iteration)
                float  fog          : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.gridPosition = TransformObjectToWorld(v.positionOS.xyz);
                CURVEDWORLD_TRANSFORM_VERTEX(v.positionOS);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                o.bary = v.bary;
                o.layerData = v.shapeData.yz;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            // How far along the layer stack this fragment is, as a continuous coordinate in units of
            // layers. Measured on the pre-bend position so the banding stays locked to the shape.
            float LayerCoordinate(float3 gridPosition, float2 layerData)
            {
            #if defined(_LAYERFROM_ITERATION)
                // Already an integer index from the mesh; nothing to measure.
                return layerData.y;

            #elif defined(_LAYERFROM_FIELD)
                // The implicit field's own value. This is the honest answer for a TPMS: the level set
                // a sheet belongs to is literally what distinguishes it from the next sheet over.
                return layerData.x / max(_LayerPeriod, .02);

            #else
                // Fallback with no mesh channel: project onto an axis. Usable on a TPMS because its
                // sheet stack is roughly axis-aligned, and it is the only thing available until the
                // builders write TEXCOORD2.
                float3 axis = normalize(_LayerAxis.xyz + float3(0, 1e-5, 0));
                float along = dot(gridPosition, axis);

                if (abs(_Dislocation) > 1e-4)
                {
                    // A screw dislocation advances the field by `_Dislocation` periods per full turn
                    // about the axis, so the bands have to climb with it. Flat bands would cut across
                    // the continuity the dislocation exists to create, which is exactly the seam that
                    // gives away a fake endless staircase.
                    float3 screw = normalize(_DislocAxis.xyz + float3(0, 1e-5, 0));
                    float3 radial = gridPosition - screw * dot(gridPosition, screw);
                    // Any two vectors orthogonal to the screw axis will do for measuring azimuth.
                    float3 e1 = normalize(abs(screw.y) < .9 ? cross(screw, float3(0, 1, 0))
                                                            : cross(screw, float3(1, 0, 0)));
                    float3 e2 = cross(screw, e1);
                    float azimuth = atan2(dot(radial, e2), dot(radial, e1));
                    along += _Dislocation * _LayerPeriod * (azimuth / ESCHER_TAU);
                }

                return along / max(_LayerPeriod, .02);
            #endif
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

                // --- layer banding ---------------------------------------------------------------
                float layerCoord = LayerCoordinate(i.gridPosition, i.layerData);

                // Wrap into `_LayerCount` distinct bands. Wrapping rather than clamping is correct
                // here: a periodic surface genuinely repeats, so band N+count should look like band N
                // and the stack reads as endless instead of running out.
                float wrapped = frac(layerCoord / max(_LayerCount, 1)) * max(_LayerCount, 1);
                float bandIndex = floor(wrapped);
                float withinBand = wrapped - bandIndex;

                // `_LayerSharp` 1 gives flat per-band colour (sheets read as discrete objects),
                // 0 gives a smooth gradient (sheets read as one continuous volume). Both are useful,
                // so it is a crossfade rather than a choice.
                float bandT = lerp(withinBand, 0, _LayerSharp);
                float stackT = (bandIndex + bandT) / max(_LayerCount, 1);

                // Three stops, so adjacent sheets differ and sheet N+2 is not a repeat of N. Two
                // colours alternate and the eye pairs the sheets up wrongly.
                float3 layerTint = stackT < .5
                    ? lerp(_LayerColorA.rgb, _LayerColorB.rgb, saturate(stackT * 2))
                    : lerp(_LayerColorB.rgb, _LayerColorC.rgb, saturate(stackT * 2 - 1));

                // --- iteration depth -------------------------------------------------------------
                float iterT = saturate(i.layerData.y / max(_IterMax, 1));
                // Deep iterations are the fine detail. Thinning them keeps a Mandelbox's deep folds
                // from saturating into a solid mass, which is the usual failure of fractal wires.
                float iterDim = lerp(1, 1 - iterT, _IterThin);
                // ...and moving the sparkle the other way puts the glitter on the fine structure,
                // which is where it reads as detail rather than as noise.
                float iterSpark = lerp(1, 1 + iterT * 2, _IterSparkle);

                // The nearest band gets a lift so there is a readable front surface in a stack that
                // would otherwise be uniformly bright all the way back.
                float nearBoost = 1 + _NearLayerBoost * (1 - withinBand) * _LayerSharp;

                float scan;
                WorldGridHighlighting_float(i.positionWS, _Time.y, scan);
                float3 base = lerp(_LayerColorA.rgb, layerTint, _LayerMix);
                float3 tint = lerp(base, _SparkColor.rgb, saturate(scan * _ScanTint));

                float3 colour = base * _Floor
                              + tint * wire * _WireOpacity * iterDim * nearBoost
                              + _SparkColor.rgb * spark * _Sparkle * _Glow * iterSpark;

                colour = MixFogColor(colour, half3(0, 0, 0), i.fog);
                return half4(colour, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
