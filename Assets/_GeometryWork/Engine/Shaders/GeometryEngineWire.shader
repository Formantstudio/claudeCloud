// Standalone URP barycentric wire for the geometry engine. No Curved World, no studio includes:
// only the URP core library, so it compiles in any URP project.
//
// Expects the WireMeshBuilder layout: barycentric coordinates in UV1 (components lifted to [1,2]
// along hidden quad diagonals) and a normalised parameter UV in UV0.
//
//  - Constant screen-space width with a one-pixel anti-aliased edge (fwidth), fading by coverage
//    below one pixel instead of breaking up.
//  - _ShowDiagonals: frac() undoes the +1 diagonal lift, so the shared diagonal draws again
//    without rebuilding the mesh.
//  - Depth fade near the camera and into the distance.
//  - Dual-tone gradient along U or V of the surface parameter.
//  - Emissive lattice pulse: a travelling glow wave along U or V, driven by time and by
//    _PulsePhase, which a script can set from a beat clock.
//
// Opaque with Cull Off, so self-intersecting immersions resolve in the depth buffer.
Shader "GeometryEngine/Wire"
{
    Properties
    {
        [Header(Wire)]
        _FillColor      ("Fill", Color) = (0.004, 0.008, 0.014, 1)
        _WireColor      ("Wire colour A", Color) = (0.06, 0.55, 0.65, 1)
        _WireColorB     ("Wire colour B", Color) = (0.95, 0.55, 0.15, 1)
        [Enum(U,0,V,1)] _GradientAxis ("Gradient along", Float) = 1
        _GradientMix    ("Gradient amount", Range(0, 1)) = 1
        _WireWidth      ("Width (pixels)", Range(0.25, 8)) = 1.25
        _WireOpacity    ("Wire opacity", Range(0, 1)) = 1
        [Toggle] _ShowDiagonals ("Show quad diagonals", Float) = 0

        [Header(Depth fade)]
        _NearFade       ("Near fade start, end", Vector) = (0.05, 0.6, 0, 0)
        _FarFade        ("Far fade start, end", Vector) = (40, 120, 0, 0)

        [Header(Lattice pulse)]
        _PulseColor     ("Pulse colour", Color) = (1, 0.85, 0.5, 1)
        _PulseIntensity ("Pulse intensity", Range(0, 8)) = 1.5
        [Enum(U,0,V,1)] _PulseAxis ("Pulse along", Float) = 1
        _PulseFrequency ("Waves across the surface", Range(0, 32)) = 3
        _PulseSpeed     ("Waves per second", Range(-4, 4)) = 0.25
        _PulseSharpness ("Sharpness", Range(1, 64)) = 12
        _PulsePhase     ("Phase (script driven)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        // One material layout shared by both passes keeps the shader SRP Batcher compatible.
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _FillColor, _WireColor, _WireColorB, _PulseColor;
            float4 _NearFade, _FarFade;
            float _GradientAxis, _GradientMix, _WireWidth, _WireOpacity, _ShowDiagonals;
            float _PulseIntensity, _PulseAxis, _PulseFrequency, _PulseSpeed, _PulseSharpness, _PulsePhase;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Wire"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 bary       : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 bary       : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                o.bary = v.bary;
                return o;
            }

            // 1 on the wire, 0 off it, with a one-pixel anti-aliased edge at constant screen width.
            float WireMask(float3 bary)
            {
                // frac() maps a lifted diagonal component back to its true barycentric value.
                bary = _ShowDiagonals > 0.5 ? frac(bary) : bary;
                float3 pixels = bary / max(fwidth(bary), 1e-6);
                float distancePx = min(pixels.x, min(pixels.y, pixels.z));
                float halfWidth = max(_WireWidth, 1.0) * 0.5;
                float mask = 1.0 - saturate(distancePx - halfWidth + 0.5);
                // Below one pixel, keep the line one pixel wide and dim it by its coverage.
                return mask * saturate(_WireWidth);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float wire = WireMask(i.bary) * _WireOpacity;

                float distanceToCamera = length(i.positionWS - _WorldSpaceCameraPos);
                float fade = smoothstep(_NearFade.x, max(_NearFade.y, _NearFade.x + 1e-4), distanceToCamera)
                           * (1.0 - smoothstep(_FarFade.x, max(_FarFade.y, _FarFade.x + 1e-4), distanceToCamera));
                wire *= fade;

                float gradientCoord = _GradientAxis < 0.5 ? i.uv.x : i.uv.y;
                half3 wireColor = lerp(_WireColor.rgb, _WireColorB.rgb, saturate(gradientCoord) * _GradientMix);

                float pulseCoord = _PulseAxis < 0.5 ? i.uv.x : i.uv.y;
                float wave = cos(TWO_PI * (pulseCoord * _PulseFrequency - _Time.y * _PulseSpeed - _PulsePhase));
                float pulse = pow(saturate(wave), _PulseSharpness) * _PulseIntensity;

                half3 color = _FillColor.rgb + wire * (wireColor + pulse * _PulseColor.rgb);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // Depth so the wire works with depth-dependent post effects and the depth-priming path.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            Cull Off
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float4 vert(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
