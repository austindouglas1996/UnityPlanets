Shader "Custom/URP_CustomLitGPU"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _UseVertexColor("Use Vertex Color", Float) = 1

        // New texture controls
        _CustomBaseMap("Base Texture", 2D) = "white" {}
        _UseBaseMap("Use Base Texture (0..1)", Range(0,1)) = 1
        _TextureTint("Texture Tint", Color) = (1,1,1,1)
        _TexScale("Texture Tiling (world units)", Float) = 1.0
        _TriplanarSharpness("Triplanar Sharpness", Range(0.5,8)) = 2.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalRenderPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Transparent+2"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back
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

            #include "../ChunkFunctions.hlsl"
            #include "../ChunkColoring.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

            StructuredBuffer<ChunkTriangleData> _TriangleBuffer;

            float4 _BaseColor;
            float _UseVertexColor;
            int LODHeatMap;

            // === New texture uniforms ===
            TEXTURE2D(_CustomBaseMap);
            SAMPLER(sampler_CustomBaseMap);
            float  _UseBaseMap;
            float4 _TextureTint;
            float  _TexScale;
            float  _TriplanarSharpness;

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

                ChunkTriangleData tri = _TriangleBuffer[triIndex];

                float3 pos = subIndex == 0 ? tri.a :
                             subIndex == 1 ? tri.b :
                                             tri.c;

                OUT.positionWS = pos;

                // Face normal per triangle (flat shading)
                OUT.normalWS = normalize(cross(tri.b - tri.a, tri.c - tri.a));
                OUT.color = LODHeatMap == 1 ? GetLodColor(tri.LodKey) : float4(GetTerrainColor(OUT.normalWS, OUT.positionWS),1);
                OUT.positionCS = TransformWorldToHClip(pos);
                return OUT;
            }

            // Triplanar sampling in world space (no UVs required)
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


// Utility: convert sRGB to Linear only if project is in Linear color space
inline float3 FromSRGB(float3 c)
{
#if defined(UNITY_COLORSPACE_GAMMA)
    return c;                         // already gamma, do nothing
#else
    return SRGBToLinear(c);           // convert to linear
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
    inputData.vertexLighting  = float3(0, 0, 0);
    inputData.bakedGI         = SampleSH(inputData.normalWS);

    // Base: color or triplanar texture
    float3 texColor     = SampleBaseMapTriplanar(IN.positionWS, inputData.normalWS).rgb; // Unity handles sRGB
    float3 baseColorLin = FromSRGB(_BaseColor.rgb);
    float3 baseOrTex    = lerp(baseColorLin, texColor, saturate(_UseBaseMap));

    // Optional vertex-color override
    float3 vertexColorLin = FromSRGB(IN.color.rgb);
    float3 finalColor     = lerp(baseOrTex, vertexColorLin, saturate(_UseVertexColor));

    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo      = finalColor;
    surfaceData.alpha       = 1.0;
    surfaceData.metallic    = 0.0;
    surfaceData.smoothness  = 0.1;
    surfaceData.occlusion   = 1.0;
    surfaceData.emission    = 0.0;
    surfaceData.normalTS    = float3(0, 0, 1);

    float4 color = UniversalFragmentPBR(inputData, surfaceData);
    color.rgb    = MixFog(color.rgb, inputData.fogCoord);
    return color;
}


            ENDHLSL
        }
    }
}