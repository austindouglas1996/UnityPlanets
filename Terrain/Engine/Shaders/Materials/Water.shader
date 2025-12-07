Shader "Custom/URP/TerrainWater"
{
    Properties
    {
        _WaterHeight("Water Height", Float) = 0.0
        _FoamColor("Foam Color", Vector) = (1, 1, 1, 1)
        _FoamThickness("Foam Thickness", Float) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue"="Transparent+10" 
            "RenderType"="Transparent"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            // URP includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float _WaterHeight;
            float4 _FoamColor;
            float _FoamThickness;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_Position;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS   = TransformObjectToWorldNormal(v.normalOS);
                o.positionWS  = posWS;
                o.normalWS    = nWS;
                o.positionHCS = TransformWorldToHClip(posWS);
                return o;
            }

half4 frag (Varyings i) : SV_Target
{
// Base water color
float3 deepColor = float3(0.10, 0.30, 0.70);      // deep blue
float3 shallowColor = float3(0.20, 0.55, 0.90);   // shallower light blue

// Distance from water surface
float depth = _WaterHeight - i.positionWS.y;

// ----------------------
// 1. Depth-based tint
// ----------------------
float depthFactor = saturate(depth * 0.2); 
float3 waterColor = lerp(shallowColor, deepColor, depthFactor);

// ----------------------
// 2. Foam near shoreline
// ----------------------
float foamEdge = saturate(1.0 - (depth / _FoamThickness));
float3 foam = _FoamColor.rgb * pow(foamEdge, 3.0); // cute falloff

// ----------------------
// 3. Fresnel sparkle
// ----------------------
float3 N = normalize(i.normalWS);
float3 V = normalize(_WorldSpaceCameraPos.xyz - i.positionWS);
float fresnel = pow(1.0 - saturate(dot(N, V)), 3.0);
float3 fresnelColor = float3(0.75, 0.90, 1.00) * fresnel * 0.3;

// ----------------------
// Combine everything
// ----------------------
float3 finalColor = waterColor + foam + fresnelColor;

return float4(finalColor, 1.0);

}

            ENDHLSL
        }
    }

    FallBack Off
}
