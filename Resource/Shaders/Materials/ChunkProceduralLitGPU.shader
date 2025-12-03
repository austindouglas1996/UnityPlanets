Shader "Custom/ChunkProceduralLitGPU"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Overlay("Overlay Mode", Int) = 0

        _CustomBaseMap("Base Texture", 2D) = "white" {}
        _UseBaseMap("Use Base Texture (0..1)", Range(0,1)) = 1
        _TextureTint("Texture Tint", Color) = (1,1,1,1)
        _TexScale("Texture Tiling (world units)", Float) = 1.0
        _TriplanarSharpness("Triplanar Sharpness", Range(0.5,8)) = 2.0

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
            "Queue"          = "Transparent+2"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            // Main directional light + cascades
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            // Additional lights (vertex or per-pixel) + their shadows
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            // Shadow filtering
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            // Mixed lighting modes (baked + realtime)
            #pragma multi_compile_fragment _ _MIXED_LIGHTING_SUBTRACTIVE

            // Fog
#           pragma multi_compile_fog

            // Instancing (if you want SRP batcher)
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include "../ChunkFunctions.hlsl"
            #include "../ChunkColoring.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            StructuredBuffer<TriangleData> _TriangleBuffer;
            StructuredBuffer<ChunkDetailData> _TriangleDetailsBuffer;

            int    Overlay;

            TEXTURE2D(_CustomBaseMap);
            SAMPLER(sampler_CustomBaseMap);
            float4 _BaseColor;
            float  _UseBaseMap;
            float4 _TextureTint;
            float  _TexScale;
            float  _TriplanarSharpness;

            float _LightIntensity;
            float4 _LightColor;
            float4 _ShadowColor;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 tangentWS  : TEXCOORD2;
                float4 color      : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                uint triIndex = IN.vertexID / 3;
                uint subIndex = IN.vertexID % 3;
                TriangleData tri = _TriangleBuffer[triIndex];
                ChunkDetailData data = _TriangleDetailsBuffer[triIndex];

                float3 pos = (subIndex == 0) ? tri.a : (subIndex == 1) ? tri.b : tri.c;
                // Do you like triangles and savings? 
                // you can use a simple normalize to make some simple normals.
                //normalize(cross(tri.b - tri.a, tri.c - tri.a));
                float3 normal = (subIndex == 0) ? tri.NormalA : (subIndex == 1) ? tri.NormalB : tri.NormalC;
                float3 up = abs(normal.y) < 0.999 ? float3(0,1,0) : float3(1,0,0);

                OUT.positionCS = TransformWorldToHClip(pos);
                OUT.positionWS = pos;
                OUT.normalWS   = normal;
                OUT.tangentWS = float4(normalize(cross(up, normal)), 1.0);
                OUT.color      = GetVertexColor(tri,data,subIndex,Overlay);

                return OUT;
            }

            float4 SampleBaseMapTriplanar(float3 wsPos, float3 wsNormal)
            {
                float3 n = normalize(wsNormal);
                float3 an = pow(abs(n), _TriplanarSharpness);
                float sum = an.x + an.y + an.z + 1e-5;
                float3 w = an / sum;

                float s = max(_TexScale, 1e-4);
                float2 uvX = wsPos.zy / s;
                float2 uvY = wsPos.xz / s;
                float2 uvZ = wsPos.xy / s;

                float4 tx = SAMPLE_TEXTURE2D(_CustomBaseMap, sampler_CustomBaseMap, uvX);
                float4 ty = SAMPLE_TEXTURE2D(_CustomBaseMap, sampler_CustomBaseMap, uvY);
                float4 tz = SAMPLE_TEXTURE2D(_CustomBaseMap, sampler_CustomBaseMap, uvZ);

                float4 c = tx * w.x + ty * w.y + tz * w.z;
                c.rgb *= _TextureTint.rgb;
                return float4(c.rgb, 1);
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
    // --------------------------------------
    // Populate InputData (your original code)
    // --------------------------------------
    InputData inputData = (InputData)0;
    inputData.positionWS      = IN.positionWS;
    inputData.normalWS        = normalize(-IN.normalWS);
    inputData.viewDirectionWS = GetWorldSpaceViewDir(IN.positionWS);
    inputData.shadowCoord     = TransformWorldToShadowCoord(IN.positionWS);
    inputData.fogCoord        = ComputeFogFactor(IN.positionCS.z);
    inputData.vertexLighting  = float3(0,0,0);
    inputData.bakedGI         = SampleSH(inputData.normalWS);

    // --------------------------------------
    // Your existing albedo logic
    // --------------------------------------
    float3 texColor     = SampleBaseMapTriplanar(IN.positionWS, inputData.normalWS).rgb;
    float3 baseColorLin = FromSRGB(_BaseColor.rgb);
    float3 baseOrTex    = lerp(baseColorLin, texColor, saturate(_UseBaseMap));

    float3 vertexColorLin = FromSRGB(IN.color.rgb);
    float3 finalColor     = lerp(baseOrTex, vertexColorLin, 1.0);

    // --------------------------------------
    // >>> Toon Lighting Section <<<
    // --------------------------------------

    // 1. Light direction (URP stores main light here)
    float3 lightDir = normalize(_MainLightPosition.xyz);

    // 2. Lambert term
    float NdotL = saturate(dot(inputData.normalWS, lightDir));

    // 3. Soft toon ramp band
    float shade = smoothstep(0.35, 0.65, NdotL);

    // 4. Time-of-day / brightness control
    float3 lightCol  = _LightColor * _LightIntensity;
    float3 shadowCol = lerp(_ShadowColor * 0.2, _ShadowColor, _LightIntensity);

    // 5. Toon blend
    float3 litColor = lerp(shadowCol, lightCol, shade);

    // 6. Apply lighting to albedo
    float3 litFinal = finalColor * litColor;

    // --------------------------------------
    // Fog (your original fog)
    // --------------------------------------
    float3 fogged = MixFog(litFinal, inputData.fogCoord);

    // --------------------------------------
    // Return final color
    // --------------------------------------
    return float4(fogged, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
