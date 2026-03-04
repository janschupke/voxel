Shader "Voxel/WheatWind"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.81, 0.78, 0.03, 1)
        _WindAmplitude ("Wind Amplitude", Range(0, 0.5)) = 0.15
        _WindFrequency ("Wind Frequency", Range(0.5, 4)) = 1.5
        _WindSpeed ("Wind Speed", Range(0.5, 3)) = 1.2
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _WindAmplitude;
            float _WindFrequency;
            float _WindSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 positionOS = IN.positionOS.xyz;
                float time = _Time.y * _WindSpeed;

                // Height factor: base (y=0) stays fixed, taller parts sway more
                float heightFactor = max(0, positionOS.y);

                // Sine wind: sway in X and Z for natural bending
                float windX = sin(time + positionOS.x * _WindFrequency) * _WindAmplitude * heightFactor;
                float windZ = sin(time * 0.7 + positionOS.z * _WindFrequency) * _WindAmplitude * heightFactor * 0.6;

                positionOS.x += windX;
                positionOS.z += windZ;

                float3 positionWS = TransformObjectToWorld(positionOS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionCS = TransformWorldToHClip(positionWS);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(IN.normalWS, mainLight.direction));
                half3 lighting = mainLight.color * (NdotL * mainLight.shadowAttenuation + 0.3);
                return half4(_BaseColor.rgb * lighting, 1);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
