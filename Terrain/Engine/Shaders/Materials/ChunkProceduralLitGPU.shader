Shader "Custom/ChunkProceduralLitGPU"
{
    Properties
    {
        [Header(Style)]
        _BaseColor("Global Tint", Color) = (1, 1, 1, 1)
        _Overlay("Overlay Debug Mode", Int) = 0
        [Toggle(_TERRAIN_TRIPLANAR)] _UseTriplanar("Use Triplanar Layers (off = vertex colors)", Float) = 0
        _LightIntensity("Light Intensity", Float) = 1.0
        // Vector (not Color) so the over-bright material values aren't gamma-converted
        // — preserves the original stylized look exactly.
        _LightColor("Light Color", Vector) = (1, 1, 1, 1)
        _ShadowColor("Shadow Color", Vector) = (0.55, 0.55, 0.55, 1)
        _ShadeMin("Shade Band Min", Range(0, 1)) = 0.35
        _ShadeMax("Shade Band Max", Range(0, 1)) = 0.65
        _AmbientStrength("Ambient Strength", Range(0, 2)) = 0.35
        _SpecularStrength("Specular Strength", Range(0, 2)) = 0.0
        _Smoothness("Default Smoothness", Range(0, 1)) = 0.1

        [Header(Triplanar Placement)]
        _TriplanarSharpness("Triplanar Sharpness", Range(1, 8)) = 4
        _SandHeight("Sand Max Height", Float) = 3
        _SandBlend("Sand Blend", Float) = 3
        _SnowHeight("Snow Min Height", Float) = 45
        _SnowBlend("Snow Blend", Float) = 12
        _DirtSlopeStart("Dirt Slope Start (0 flat..1 vertical)", Range(0, 1)) = 0.25
        _DirtSlopeBlend("Dirt Slope Blend", Range(0.001, 1)) = 0.2
        _RockSlopeStart("Rock Slope Start (0 flat..1 vertical)", Range(0, 1)) = 0.55
        _RockSlopeBlend("Rock Slope Blend", Range(0.001, 1)) = 0.2

        [Header(Grass Layer)]
        _GrassColor("Grass Color", Color) = (0.34, 0.55, 0.26, 1)
        [ToggleUI] _GrassUseTex("Grass Use Texture", Float) = 0
        [NoScaleOffset] _GrassAlbedo("Grass Albedo", 2D) = "white" {}
        _GrassScale("Grass Tex Scale", Float) = 0.15
        _GrassTexStrength("Grass Texture Influence", Range(0, 1)) = 1
        [ToggleUI] _GrassUseNormal("Grass Use Normal", Float) = 0
        [Normal] _GrassNormal("Grass Normal", 2D) = "bump" {}
        _GrassNormalStrength("Grass Normal Strength", Float) = 1
        _GrassSmoothness("Grass Smoothness", Range(0, 1)) = 0.08
        _GrassMetallic("Grass Metallic", Range(0, 1)) = 0

        [Header(Dirt Layer)]
        _DirtColor("Dirt Color", Color) = (0.45, 0.34, 0.22, 1)
        [ToggleUI] _DirtUseTex("Dirt Use Texture", Float) = 0
        [NoScaleOffset] _DirtAlbedo("Dirt Albedo", 2D) = "white" {}
        _DirtScale("Dirt Tex Scale", Float) = 0.15
        _DirtTexStrength("Dirt Texture Influence", Range(0, 1)) = 1
        [ToggleUI] _DirtUseNormal("Dirt Use Normal", Float) = 0
        [Normal] _DirtNormal("Dirt Normal", 2D) = "bump" {}
        _DirtNormalStrength("Dirt Normal Strength", Float) = 1
        _DirtSmoothness("Dirt Smoothness", Range(0, 1)) = 0.05
        _DirtMetallic("Dirt Metallic", Range(0, 1)) = 0

        [Header(Rock Layer)]
        _RockColor("Rock Color", Color) = (0.42, 0.42, 0.44, 1)
        [ToggleUI] _RockUseTex("Rock Use Texture", Float) = 0
        [NoScaleOffset] _RockAlbedo("Rock Albedo", 2D) = "white" {}
        _RockScale("Rock Tex Scale", Float) = 0.08
        _RockTexStrength("Rock Texture Influence", Range(0, 1)) = 1
        [ToggleUI] _RockUseNormal("Rock Use Normal", Float) = 0
        [Normal] _RockNormal("Rock Normal", 2D) = "bump" {}
        _RockNormalStrength("Rock Normal Strength", Float) = 1
        _RockSmoothness("Rock Smoothness", Range(0, 1)) = 0.10
        _RockMetallic("Rock Metallic", Range(0, 1)) = 0

        [Header(Sand Layer)]
        _SandColor("Sand Color", Color) = (0.80, 0.74, 0.53, 1)
        [ToggleUI] _SandUseTex("Sand Use Texture", Float) = 0
        [NoScaleOffset] _SandAlbedo("Sand Albedo", 2D) = "white" {}
        _SandScale("Sand Tex Scale", Float) = 0.20
        _SandTexStrength("Sand Texture Influence", Range(0, 1)) = 1
        [ToggleUI] _SandUseNormal("Sand Use Normal", Float) = 0
        [Normal] _SandNormal("Sand Normal", 2D) = "bump" {}
        _SandNormalStrength("Sand Normal Strength", Float) = 1
        _SandSmoothness("Sand Smoothness", Range(0, 1)) = 0.06
        _SandMetallic("Sand Metallic", Range(0, 1)) = 0

        [Header(Snow Layer)]
        _SnowColor("Snow Color", Color) = (0.92, 0.94, 0.98, 1)
        [ToggleUI] _SnowUseTex("Snow Use Texture", Float) = 0
        [NoScaleOffset] _SnowAlbedo("Snow Albedo", 2D) = "white" {}
        _SnowScale("Snow Tex Scale", Float) = 0.10
        _SnowTexStrength("Snow Texture Influence", Range(0, 1)) = 1
        [ToggleUI] _SnowUseNormal("Snow Use Normal", Float) = 0
        [NoScaleOffset] [Normal] _SnowNormal("Snow Normal", 2D) = "bump" {}
        _SnowNormalStrength("Snow Normal Strength", Float) = 1
        _SnowSmoothness("Snow Smoothness", Range(0, 1)) = 0.25
        _SnowMetallic("Snow Metallic", Range(0, 1)) = 0
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

            // ── URP lighting / shadow / fog keywords ─────────────────────────
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog

            // Local material toggle: solid vertex colors vs triplanar texture layers.
            #pragma shader_feature_local _TERRAIN_TRIPLANAR

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "../ChunkFunctions.hlsl"
            #include "../ChunkColoring.hlsl"
            #include "ChunkTriplanar.hlsl"

            StructuredBuffer<TriangleData> _TriangleBuffer;
            StructuredBuffer<ChunkDetailData> _TriangleDetailsBuffer;

            // Layer textures live outside the cbuffer. One shared linear-repeat
            // sampler for all of them keeps sampler-slot usage low.
            TEXTURE2D(_GrassAlbedo); TEXTURE2D(_GrassNormal);
            TEXTURE2D(_DirtAlbedo);  TEXTURE2D(_DirtNormal);
            TEXTURE2D(_RockAlbedo);  TEXTURE2D(_RockNormal);
            TEXTURE2D(_SandAlbedo);  TEXTURE2D(_SandNormal);
            TEXTURE2D(_SnowAlbedo);  TEXTURE2D(_SnowNormal);
            //SAMPLER(sampler_LinearRepeat);

            // All non-texture properties must live in UnityPerMaterial for the
            // SRP batcher to treat this material consistently.
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                int    _Overlay;
                float  _UseTriplanar;
                float  _LightIntensity;
                float4 _LightColor;
                float4 _ShadowColor;
                float  _ShadeMin;
                float  _ShadeMax;
                float  _AmbientStrength;
                float  _SpecularStrength;
                float  _Smoothness;

                float  _TriplanarSharpness;
                float  _SandHeight;
                float  _SandBlend;
                float  _SnowHeight;
                float  _SnowBlend;
                float  _DirtSlopeStart;
                float  _DirtSlopeBlend;
                float  _RockSlopeStart;
                float  _RockSlopeBlend;

                float4 _GrassColor; float _GrassUseTex; float _GrassScale; float _GrassTexStrength;
                float  _GrassUseNormal; float _GrassNormalStrength; float _GrassSmoothness; float _GrassMetallic;

                float4 _DirtColor;  float _DirtUseTex;  float _DirtScale;  float _DirtTexStrength;
                float  _DirtUseNormal;  float _DirtNormalStrength;  float _DirtSmoothness;  float _DirtMetallic;

                float4 _RockColor;  float _RockUseTex;  float _RockScale;  float _RockTexStrength;
                float  _RockUseNormal;  float _RockNormalStrength;  float _RockSmoothness;  float _RockMetallic;

                float4 _SandColor;  float _SandUseTex;  float _SandScale;  float _SandTexStrength;
                float  _SandUseNormal;  float _SandNormalStrength;  float _SandSmoothness;  float _SandMetallic;

                float4 _SnowColor;  float _SnowUseTex;  float _SnowScale;  float _SnowTexStrength;
                float  _SnowUseNormal;  float _SnowNormalStrength;  float _SnowSmoothness;  float _SnowMetallic;
            CBUFFER_END

            struct Attributes { uint vertexID : SV_VertexID; };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  fogCoord   : TEXCOORD2;   // FIX: fog factor from clip-space Z, vertex stage
                float4 color      : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                uint triIndex = IN.vertexID / 3;
                uint subIndex = IN.vertexID % 3;

                TriangleData tri     = _TriangleBuffer[triIndex];
                ChunkDetailData data = _TriangleDetailsBuffer[triIndex];

                float3 pos    = (subIndex == 0) ? tri.a : (subIndex == 1) ? tri.b : tri.c;
                float3 normal = (subIndex == 0) ? tri.NormalA : (subIndex == 1) ? tri.NormalB : tri.NormalC;

                OUT.positionCS = TransformWorldToHClip(pos);
                OUT.positionWS = pos;                 // already world-space from marching cubes
                OUT.normalWS   = normal;              // already world-space
                OUT.color      = GetVertexColor(tri, data, subIndex, _Overlay);

                // FIX: URP fog uses the CLIP-SPACE Z evaluated in the vertex stage,
                // then interpolated. The old code fed positionCS.w in the fragment,
                // which is view depth, not the clip Z ComputeFogFactor expects.
                OUT.fogCoord   = ComputeFogFactor(OUT.positionCS.z);

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
                float3 positionWS = IN.positionWS;
                float3 geomN      = normalize(IN.normalWS);

                // ── Surface: albedo + per-pixel normal + smoothness/metallic ──
                float3 albedo;
                float3 normalWS   = geomN;
                float  smoothness = _Smoothness;
                float  metallic   = 0.0;

            #if defined(_TERRAIN_TRIPLANAR)
                // World-space triplanar layers, blended by height and slope.
                float3 triW     = TriplanarWeights(geomN, _TriplanarSharpness);
                float  h        = positionWS.y;
                float  flatness = saturate(geomN.y);   // 1 flat, 0 vertical
                float  slope    = 1.0 - flatness;

                // Readable placement weights. Painted in order below; rock last so
                // steep faces win at any height. Successive lerp keeps them normalized.
                float sandW = (1.0 - smoothstep(_SandHeight, _SandHeight + _SandBlend, h)) * flatness;
                float snowW = smoothstep(_SnowHeight - _SnowBlend, _SnowHeight, h);
                float dirtW = smoothstep(_DirtSlopeStart, _DirtSlopeStart + _DirtSlopeBlend, slope);
                float rockW = smoothstep(_RockSlopeStart, _RockSlopeStart + _RockSlopeBlend, slope);

                albedo = float3(0, 0, 0);
                smoothness = 0.0;

                // Grass = base ground (always sampled). Then dirt→sand→snow→rock.
                AddTerrainLayer(albedo, normalWS, smoothness, metallic, 1.0,
                    _GrassAlbedo, _GrassNormal, sampler_LinearRepeat,
                    _GrassColor.rgb, _GrassScale, _GrassTexStrength, _GrassUseTex,
                    _GrassUseNormal, _GrassNormalStrength, _GrassSmoothness, _GrassMetallic,
                    positionWS, geomN, triW);

                AddTerrainLayer(albedo, normalWS, smoothness, metallic, dirtW,
                    _DirtAlbedo, _DirtNormal, sampler_LinearRepeat,
                    _DirtColor.rgb, _DirtScale, _DirtTexStrength, _DirtUseTex,
                    _DirtUseNormal, _DirtNormalStrength, _DirtSmoothness, _DirtMetallic,
                    positionWS, geomN, triW);

                AddTerrainLayer(albedo, normalWS, smoothness, metallic, sandW,
                    _SandAlbedo, _SandNormal, sampler_LinearRepeat,
                    _SandColor.rgb, _SandScale, _SandTexStrength, _SandUseTex,
                    _SandUseNormal, _SandNormalStrength, _SandSmoothness, _SandMetallic,
                    positionWS, geomN, triW);

                AddTerrainLayer(albedo, normalWS, smoothness, metallic, snowW,
                    _SnowAlbedo, _SnowNormal, sampler_LinearRepeat,
                    _SnowColor.rgb, _SnowScale, _SnowTexStrength, _SnowUseTex,
                    _SnowUseNormal, _SnowNormalStrength, _SnowSmoothness, _SnowMetallic,
                    positionWS, geomN, triW);

                AddTerrainLayer(albedo, normalWS, smoothness, metallic, rockW,
                    _RockAlbedo, _RockNormal, sampler_LinearRepeat,
                    _RockColor.rgb, _RockScale, _RockTexStrength, _RockUseTex,
                    _RockUseNormal, _RockNormalStrength, _RockSmoothness, _RockMetallic,
                    positionWS, geomN, triW);

                albedo *= _BaseColor.rgb;   // material colors are already linear
            #else
                // Vertex-color mode: preserves the original baked-biome look.
                albedo = FromSRGB(IN.color.rgb) * FromSRGB(_BaseColor.rgb);
            #endif

                // ── Main light (stylized band; keeps the original look) ───────
                float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
                Light mainLight    = GetMainLight(shadowCoord);

                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float shade = smoothstep(_ShadeMin, _ShadeMax, NdotL) * mainLight.shadowAttenuation;

                // Deliberately uses the material light/shadow colors (not the scene
                // light color) to preserve the stylized flat look.
                float3 lightCol  = _LightColor.rgb * _LightIntensity;
                float3 shadowCol = _ShadowColor.rgb * (1.0 - saturate(_LightIntensity) * 0.2);
                float3 litColor  = lerp(shadowCol, lightCol, shade);

                float3 color = albedo * litColor;

                // ── Additional lights (Forward + Forward+) ────────────────────
            #if defined(_ADDITIONAL_LIGHTS)
                uint addCount = GetAdditionalLightsCount();
                #if USE_FORWARD_PLUS
                    InputData inputData = (InputData)0;
                    inputData.positionWS = positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                #endif
                LIGHT_LOOP_BEGIN(addCount)
                    Light light = GetAdditionalLight(lightIndex, positionWS);
                    float atten = light.distanceAttenuation * light.shadowAttenuation;
                    color += albedo * light.color * (saturate(dot(normalWS, light.direction)) * atten);
                LIGHT_LOOP_END
            #endif

                // ── Ambient (SH / light probes), fills shadowed areas ─────────
                float3 ambient = SampleSH(normalWS) * albedo;
                color += ambient * (1.0 - shade) * _AmbientStrength;

                // ── Optional stylized specular from blended smoothness ────────
                UNITY_BRANCH
                if (_SpecularStrength > 0.0)
                {
                    float3 V = normalize(GetWorldSpaceViewDir(positionWS));
                    float3 H = normalize(mainLight.direction + V);
                    float  spec = pow(saturate(dot(normalWS, H)), lerp(8.0, 128.0, smoothness));
                    color += lightCol * (spec * _SpecularStrength * shade);
                }

                // ── Fog (correct URP stage: interpolated factor + MixFog) ─────
                color = MixFog(saturate(color), IN.fogCoord);

                return float4(color, 1.0);
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
