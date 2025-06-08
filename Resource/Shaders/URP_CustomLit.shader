Shader "Custom/URP_CustomLit"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 0, 0, 1)
    }

    SubShader
    {
        Tags 
        {
            "RenderPipeline"="UniversalRenderPipeline"
            "RenderType"="Opaque"
            "Queue"="Transparent+2"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            ZWrite On
            Blend One Zero

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

            #undef _SURFACE_TYPE_TRANSPARENT

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 color      : COLOR;
            };

            float4 _BaseColor;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.color = IN.color;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // Setup lighting input
                InputData inputData;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = normalize(IN.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceViewDir(IN.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                inputData.fogCoord = ComputeFogFactor(IN.positionCS.z);
                inputData.vertexLighting = float3(0, 0, 0);
                inputData.bakedGI = SampleSH(inputData.normalWS); // Important for shadows!

                float3 finalColor = IN.color.rgb;

                // Setup surface data
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = finalColor;
                surfaceData.alpha = 1.0;
                surfaceData.metallic = 0.0;
                surfaceData.smoothness = 0.5;
                surfaceData.occlusion = 1.0;
                surfaceData.emission = 0.0;
                surfaceData.normalTS = float3(0, 0, 1);

                float4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }

            ENDHLSL
        }
    }
}
