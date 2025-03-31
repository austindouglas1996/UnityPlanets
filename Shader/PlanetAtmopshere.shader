Shader "Custom/AtmosphereGlow"
{
    Properties
    {
        _Color ("Glow Color", Color) = (0.4, 0.7, 1, 1)
        _Intensity ("Glow Intensity", Range(0, 5)) = 1
        _Power ("Edge Sharpness", Range(0.1, 10)) = 3
        _Alpha ("Alpha", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Front
            Lighting Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Intensity;
            float _Power;
            float _Alpha;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = _WorldSpaceCameraPos - worldPos;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float fresnel = pow(1.0 - saturate(dot(normalize(i.worldNormal), normalize(i.viewDir))), _Power);
                float glow = fresnel * _Intensity;
                return fixed4(_Color.rgb * glow, glow * _Alpha);
            }
            ENDCG
        }
    }
}
