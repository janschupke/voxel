Shader "Voxel/Water"
{
    Properties
    {
        _WaterColor ("Water Color", Color) = (0.2, 0.5, 0.9, 0.7)
        _WaveAmplitude ("Wave Amplitude", Range(0, 1)) = 0.4
        _WaveFrequency ("Wave Frequency", Range(0.01, 1)) = 0.1
        _WaveSpeed ("Wave Speed", Range(0.1, 3)) = 1
        _RefractionStrength ("Refraction Strength", Range(0, 0.1)) = 0.04
        [Toggle] _RefractionEnabled ("Refraction Enabled", Float) = 1
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _WaterColor;
            float _WaveAmplitude;
            float _WaveFrequency;
            float _WaveSpeed;
            float _RefractionStrength;
            float _RefractionEnabled;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float time = _Time.y;

                float wave = sin(positionWS.x * _WaveFrequency + time * _WaveSpeed)
                    * sin(positionWS.z * _WaveFrequency + time * _WaveSpeed);
                positionWS.y += wave * _WaveAmplitude;

                OUT.positionWS = positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = GetCameraPositionWS() - positionWS;
                OUT.positionCS = TransformWorldToHClip(positionWS);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 color = _WaterColor;

                if (_RefractionEnabled > 0.5)
                {
                    float2 screenUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                    float3 viewDir = normalize(IN.viewDirWS);
                    float2 distortionBasis = viewDir.xz;
                    float viewBasisLen = length(distortionBasis);
                    if (viewBasisLen < 0.01)
                    {
                        float t = _Time.y;
                        distortionBasis = float2(sin(t * 2.0), cos(t * 2.0));
                    }
                    else
                    {
                        distortionBasis /= viewBasisLen;
                    }
                    float2 distortedUV = screenUV + distortionBasis * _RefractionStrength;
                    distortedUV = saturate(distortedUV);
                    float3 sceneColor = SampleSceneColor(distortedUV);
                    color = half4(lerp(sceneColor, _WaterColor.rgb, 1.0 - _WaterColor.a), _WaterColor.a);
                }

                return color;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
