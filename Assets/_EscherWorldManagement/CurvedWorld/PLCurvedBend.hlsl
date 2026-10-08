#ifndef PL_CURVED_BEND_INCLUDED
#define PL_CURVED_BEND_INCLUDED

// THE SHARED CURVED WORLD BEND, for this project's own shaders.
//
// Why this exists. CurvedWorldBridge bends the world by swapping URP Lit/Unlit materials
// (AddSharedBend). Anything NOT drawn by one of those swapped materials travels correctly but renders
// UNBENT and visually detaches from the room. That hit three systems:
//
//   1. HypercubeTunnel      — drawn entirely in NCubeTunnel.shader
//   2. VFX Graph particles  — positioned inside the graph, not by a transform
//   3. ScannerPortalDoor    — a custom two-pass shader
//
// One include fixes all of them, so the bend cascades instead of being re-solved per effect.
//
// HOW TO USE IT — two lines in the shader, then one macro in the vertex function:
//
//   // 1. the keyword set (this MUST be in the .shader, pragmas do not reliably work from an include)
//   #pragma multi_compile_local CURVEDWORLD_BEND_TYPE_CLASSICRUNNER_X_POSITIVE CURVEDWORLD_BEND_TYPE_LITTLEPLANET_Y CURVEDWORLD_BEND_TYPE_CYLINDRICALTOWER_X CURVEDWORLD_BEND_TYPE_CYLINDRICALROLLOFF_Z CURVEDWORLD_BEND_TYPE_TWISTEDSPIRAL_X_POSITIVE CURVEDWORLD_BEND_TYPE_TWISTEDSPIRAL_Z_POSITIVE
//   // 2. this include
//   #include "Assets/_EscherWorldManagement/CurvedWorld/PLCurvedBend.hlsl"
//
//   ... and in the vertex stage, BEFORE going to world space:
//   PL_BEND_VERTEX(v.positionOS);
//
// The proven reference for all of this is _GeometryWork/Core/CurvedChamber.shader, which has been
// bending correctly in this project for a while. This file only wraps that pattern and adds the scale.
//
// WHY THE PATH IS LITERAL. Curved World ships its bend maths as .cginc files included by absolute
// project path, so `Assets/Amazing Assets/Curved World/` must never move. That is a standing project
// constraint, not a choice made here.

// Which of Curved World's bend slots to read. The bridge drives slot 1 through its CurvedWorldController,
// so ID 1 is "the world's bend". A shader can #define PL_BEND_ID before including this to read another
// slot, which is how a second, independent bend would be added later (it needs its own controller).
#ifndef PL_BEND_ID
    #define PL_BEND_ID 1
#endif

#if PL_BEND_ID == 1
    #define CURVEDWORLD_BEND_ID_1
#elif PL_BEND_ID == 2
    #define CURVEDWORLD_BEND_ID_2
#elif PL_BEND_ID == 3
    #define CURVEDWORLD_BEND_ID_3
#elif PL_BEND_ID == 4
    #define CURVEDWORLD_BEND_ID_4
#else
    #define CURVEDWORLD_BEND_ID_1
#endif

#include "Assets/Amazing Assets/Curved World/Shaders/Core/CurvedWorldTransform.cginc"

// PER-LAYER SCALE. Declare `float _PLBendScale;` in the shader's UnityPerMaterial block and the bend
// becomes proportionate instead of all-or-nothing:
//
//   1 = bend exactly like the room        (the default, so an unset material behaves correctly)
//   0 = dead straight, ignores the bend   (for anything that must stay rigid in a bent world)
//   between = partial, so a layer can sit at a fraction of the world's curve
//
// This is a lerp between the unbent and bent object-space position, which is safe for any bend type:
// it never extrapolates and never changes the bend's shape, only how far along it a vertex travels.
// A material without the property reads 1 from the fallback below, so adding the bend to a shader
// never silently flattens anything.
#ifndef PL_BEND_SCALE_DECLARED
    // A shader that does not declare _PLBendScale still compiles and bends fully.
    #define PL_BEND_SCALE_VALUE 1.0
#else
    #define PL_BEND_SCALE_VALUE _PLBendScale
#endif

#if defined(CURVEDWORLD_IS_INSTALLED)

    // Bend a vertex, scaled. `v` is an object-space position (float3 or float4); call it before
    // TransformObjectToWorld / TransformObjectToHClip.
    #define PL_BEND_VERTEX(v) \
        { \
            float3 _plUnbent = (v).xyz; \
            CURVEDWORLD_TRANSFORM_VERTEX(v); \
            (v).xyz = lerp(_plUnbent, (v).xyz, saturate(PL_BEND_SCALE_VALUE)); \
        }

    // Bend a vertex and its normal/tangent together, for anything that is lit. Same scaling rule.
    #define PL_BEND_VERTEX_AND_NORMAL(v, n, t) \
        { \
            float3 _plUnbent = (v).xyz; \
            float3 _plUnbentN = (n).xyz; \
            CURVEDWORLD_TRANSFORM_VERTEX_AND_NORMAL(v, n, t); \
            (v).xyz = lerp(_plUnbent, (v).xyz, saturate(PL_BEND_SCALE_VALUE)); \
            (n).xyz = normalize(lerp(_plUnbentN, (n).xyz, saturate(PL_BEND_SCALE_VALUE))); \
        }

#else
    // Curved World not present: the macros vanish rather than breaking the shader.
    #define PL_BEND_VERTEX(v)
    #define PL_BEND_VERTEX_AND_NORMAL(v, n, t)
#endif

#endif // PL_CURVED_BEND_INCLUDED
