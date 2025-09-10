Shader "Custom/URP/PlanetAtmosphereRim"
{
    Properties
    {
        _AtmosphereColor ("Atmosphere Color", Color) = (1,1,1,1)
        _Intensity       ("Intensity", Range(0,5)) = 1.2
        _RimPower        ("Rim Power (sharpness)", Range(0.5,8)) = 3.0
        _NightDim        ("Night-side Dim", Range(0,1)) = 0.25
        _HorizonBoost    ("Horizon Boost", Range(0,2)) = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Front
        ZWrite Off
        Blend One One 

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            // URP includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float4 _AtmosphereColor;
            half   _Intensity;
            half   _RimPower;
            half   _NightDim;
            half   _HorizonBoost;

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
                float3 N = normalize(i.normalWS);
                float3 V = normalize(_WorldSpaceCameraPos.xyz - i.positionWS);

                float ndv  = saturate(dot(N, V));
                float fres = pow(1.0 - ndv, _RimPower) * _HorizonBoost;

                Light mainLight = GetMainLight();

                // In URP, mainLight.direction points *from* the surface toward the light source
                float NdotL = saturate(dot(N, -mainLight.direction));
                float dayFactor = lerp(_NightDim, 1.0, NdotL);
                float3 atm = _AtmosphereColor.rgb * fres * dayFactor * _Intensity;
                return half4(atm, saturate(max(max(atm.r, atm.g), atm.b)));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
