Shader "Custom/URP_CustomLitGPU"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 0, 0, 1)
        _UseVertexColor("Use Vertex Color", Float) = 1
        _SizePerChunk("Size Per Chunk", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalRenderPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

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

            #include "Includes/Triangle.hlsl" // Make sure this includes the struct Triangle

            StructuredBuffer<Triangle> TriangleBuffer;
            float _SizePerChunk;
            float4 _BaseColor;
            float _UseVertexColor;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 color      : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                uint triIndex = IN.vertexID / 3;
                uint subIndex = IN.vertexID % 3;

                Triangle tri = TriangleBuffer[triIndex];

                float3 pos = subIndex == 0 ? tri.a :
                             subIndex == 1 ? tri.b :
                                             tri.c;

                float4 color = subIndex == 0 ? tri.colorA :
                               subIndex == 1 ? tri.colorB :
                                               tri.colorC;

                float3 worldPos = pos + tri.CoordPos * _SizePerChunk;

                OUT.positionWS = worldPos;
                OUT.normalWS = normalize(tri.normal); // Shared for all 3 verts
                OUT.color = color;
                OUT.positionCS = TransformWorldToHClip(worldPos);

                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                InputData inputData;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = normalize(IN.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceViewDir(IN.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                inputData.fogCoord = ComputeFogFactor(IN.positionCS.z);
                inputData.vertexLighting = float3(0, 0, 0);
                inputData.bakedGI = SampleSH(inputData.normalWS);

                float3 finalColor = lerp(_BaseColor.rgb, IN.color.rgb, _UseVertexColor);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = finalColor;
                surfaceData.alpha = 1.0;
                surfaceData.metallic = 0.0;
                surfaceData.smoothness = 0.5;
                surfaceData.occlusion = 1.0;
                surfaceData.emission = 0.0;
                surfaceData.normalTS = float3(0, 0, 1); // Not using normal maps

                float4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }

            ENDHLSL
        }
    }
}
