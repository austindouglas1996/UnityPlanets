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

            #include "../ChunkFunctions.hlsl"
            #include "../ChunkColoring.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            StructuredBuffer<TriangleData> _TriangleBuffer;
            StructuredBuffer<ChunkDetailData> _TriangleDetailsBuffer;

            CBUFFER_START(UnityPerMaterial)
                int Overlay;
            CBUFFER_END

            float4 _BaseColor;
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

                float3 pos    = (subIndex == 0) ? tri.a : (subIndex == 1) ? tri.b : tri.c;
                float3 normal = (subIndex == 0) ? tri.NormalA : (subIndex == 1) ? tri.NormalB : tri.NormalC;

                float3 up = abs(normal.y) < 0.999 ? float3(0,1,0) : float3(1,0,0);

                OUT.color      = GetVertexColor(tri, data, subIndex, Overlay);
                OUT.positionCS = TransformWorldToHClip(pos);
                OUT.positionWS = pos;
                OUT.normalWS   = normal;
                OUT.tangentWS  = float4(normalize(cross(up, normal)), 1.0);

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
                InputData inputData = (InputData)0;
                inputData.positionWS      = IN.positionWS;
                inputData.normalWS        = normalize(IN.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceViewDir(IN.positionWS);
                inputData.shadowCoord     = TransformWorldToShadowCoord(IN.positionWS);
                inputData.fogCoord        = ComputeFogFactor(IN.positionCS.z);
                inputData.vertexLighting  = float3(0,0,0);
                inputData.bakedGI         = SampleSH(inputData.normalWS);

                float3 baseColorLin   = FromSRGB(_BaseColor.rgb);
                float3 vertexColorLin = FromSRGB(IN.color.rgb);

                float3 finalColor = vertexColorLin * baseColorLin;

                float3 lightDir = normalize(_MainLightPosition.xyz);
                float  NdotL    = saturate(dot(inputData.normalWS, lightDir));
                float  shade    = smoothstep(0.35, 0.65, NdotL);

                float3 lightCol  = _LightColor.rgb * _LightIntensity;
                float3 shadowCol = lerp(_ShadowColor.rgb * 1.2, _ShadowColor.rgb, _LightIntensity);

                float3 litColor  = lerp(shadowCol, lightCol, shade);
                float3 litFinal  = finalColor * litColor;

                float3 fogged = MixFog(litFinal, inputData.fogCoord);

                return float4(fogged, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
