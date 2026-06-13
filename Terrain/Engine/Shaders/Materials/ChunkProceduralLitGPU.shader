Shader "Custom/ChunkProceduralLitGPU"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Overlay("Overlay Mode", Int) = 0

        _LightIntensity("Light Intensity", Float) = 1.0
        _LightColor("Light Color", Vector) = (1, 1, 1, 1)
        _ShadowColor("Shadow Color", Vector) = (0.55, 0.55, 0.55, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            // Required for shadow keywords to work correctly in URP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "../ChunkFunctions.hlsl"
            #include "../ChunkColoring.hlsl"

            StructuredBuffer<TriangleData> _TriangleBuffer;
            StructuredBuffer<ChunkDetailData> _TriangleDetailsBuffer;

            // Fixed: all material properties must live inside the cbuffer for
            // the SRP batcher to work. Previously _LightColor, _LightIntensity,
            // and _ShadowColor were declared outside, which breaks batching.
            CBUFFER_START(UnityPerMaterial)
                int   Overlay;
                float _LightIntensity;
                float4 _LightColor;
                float4 _ShadowColor;
                float4 _BaseColor;
            CBUFFER_END

            struct Attributes { uint vertexID : SV_VertexID; };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 color      : COLOR;
                // Note: tangentWS removed — it was computed but never used in
                // the fragment shader. Re-add if you introduce normal mapping.
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                uint triIndex = IN.vertexID / 3;
                uint subIndex = IN.vertexID % 3;

                TriangleData tri     = _TriangleBuffer[triIndex];
                ChunkDetailData data = _TriangleDetailsBuffer[triIndex];

                float3 pos    = (subIndex == 0) ? tri.a : (subIndex == 1) ? tri.b : tri.c;
                float3 normal = normalize(cross(tri.b - tri.a, tri.c - tri.a));

                OUT.color      = GetVertexColor(tri, data, subIndex, Overlay);
                OUT.positionCS = TransformWorldToHClip(pos);
                OUT.positionWS = pos;
                OUT.normalWS   = normal;

                return OUT;
            }

            inline float3 FromSRGB(float3 c)
            {
            #if defined(UNITY_COLORSPACE_GAMMA)
                return c;
            #else
                return SRGBToLinear(c);
            #endif
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // Recompute shadow coord in the fragment shader for correct
                // cascade selection in URP shadow cascades.
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);

                float3 normalWS = normalize(IN.normalWS);

                float3 baseColorLin   = FromSRGB(_BaseColor.rgb);
                float3 vertexColorLin = FromSRGB(IN.color.rgb);
                float3 finalColor     = vertexColorLin * baseColorLin;

                // ── Lighting ──────────────────────────────────────────────────
                float3 lightDir = normalize(_MainLightPosition.xyz);
                float  NdotL    = saturate(dot(normalWS, lightDir));

                // Fixed: shadow receiving. MainLightRealtimeShadow samples the
                // shadow map. Without this, terrain never receives self-shadows
                // or shadows cast by other objects.
                float shadowAtten = MainLightRealtimeShadow(shadowCoord);

                // NdotL and shadow attenuation both modulate shade so that
                // back-facing surfaces AND shadowed surfaces go dark.
                float shade = smoothstep(0.35, 0.65, NdotL) * shadowAtten;

                float3 lightCol = _LightColor.rgb * _LightIntensity;

                // Fixed: shadow color formula was inverted — it made shadows
                // brighter when _LightIntensity was low, and could exceed 1.0
                // (clipping in HDR). Replaced with a simple darkening that
                // behaves consistently across all intensity values.
                float3 shadowCol = _ShadowColor.rgb * (1.0 - saturate(_LightIntensity) * 0.2);

                float3 litColor = lerp(shadowCol, lightCol, shade);
                float3 litFinal = finalColor * litColor;

                // ── Ambient (baked GI) ────────────────────────────────────────
                // SampleSH was previously called and discarded. Now used to add
                // a subtle ambient fill in shadowed areas so they don't go fully
                // flat. Weighted by (1 - shade) so it only contributes where
                // direct light is absent.
                float3 ambient  = SampleSH(normalWS) * finalColor;
                litFinal       += ambient * (1.0 - shade) * 0.35;

                float fogFactor = ComputeFogFactor(IN.positionCS.w);
                float3 fogged = MixFog(saturate(litFinal), fogFactor);

                // Optional extra darkening with distance
                fogged *= lerp(0.45, 1.0, fogFactor);

                return float4(fogged, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "../ChunkFunctions.hlsl"

            StructuredBuffer<TriangleData> _TriangleBuffer;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings shadowVert(Attributes IN)
            {
                Varyings OUT;
                uint triIndex = IN.vertexID / 3;
                uint subIndex = IN.vertexID % 3;
                TriangleData tri = _TriangleBuffer[triIndex];
                float3 pos = (subIndex == 0) ? tri.a : (subIndex == 1) ? tri.b : tri.c;
                OUT.positionCS = TransformWorldToHClip(pos);
                return OUT;
            }

            half4 shadowFrag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    Fallback Off
}
