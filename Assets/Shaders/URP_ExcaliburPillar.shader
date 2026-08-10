Shader "Custom/URP_ExcaliburPillar"
{
    Properties
    {
        [MainTexture] _MainTex ("Noise Texture 1", 2D) = "white" {}
        _DetailNoise ("Noise Texture 2", 2D) = "white" {}
        [HDR] _BeamColor ("HDR Pillar Color", Color) = (4, 0, 2, 1) // Deep Magenta/Red Eruption
        [HDR] _CoreColor ("HDR Core Color", Color) = (8, 8, 8, 1)   // Blazing White Core
        _PanSpeed1Y ("Primary Vertical Panning", Float) = -10.0
        _PanSpeed2Y ("Secondary Vertical Panning", Float) = -16.0
        _CoreThickness ("Core Sharpness", Range(1, 16)) = 6.0
        _NoiseDistortion ("Noise Distortion Force", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Plane"
        }

        Blend SrcAlpha One
        Cull Off
        ZWrite Off

        Pass
        {
            Name "ExcaliburMorganPillar"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_DetailNoise);
            SAMPLER(sampler_DetailNoise);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _DetailNoise_ST;
                float4 _BeamColor;
                float4 _CoreColor;
                float _PanSpeed1Y;
                float _PanSpeed2Y;
                float _CoreThickness;
                float _NoiseDistortion;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Dual Vertical UV Panning for upward shooting mana eruption
                float2 uv1 = input.uv;
                uv1.y += _Time.y * _PanSpeed1Y;

                float2 uv2 = input.uv * 1.5;
                uv2.y += _Time.y * _PanSpeed2Y;

                half4 noise1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv1);
                half4 noise2 = SAMPLE_TEXTURE2D(_DetailNoise, sampler_DetailNoise, uv2);

                float combinedNoise = saturate((noise1.r + noise2.r) * 0.65);

                // Horizontal Mask for Vertical Pillar (0 at center X=0.5, 1 at left/right edges)
                float distortedX = input.uv.x + (combinedNoise - 0.5) * _NoiseDistortion;
                float distFromCenter = abs(distortedX - 0.5) * 2.0;

                float edgeMask = saturate(1.0 - distFromCenter);
                float outerFlame = pow(edgeMask, 1.8);
                float innerCore = pow(edgeMask, _CoreThickness);

                // Taper & fade out near top of pillar
                float heightFade = saturate(1.0 - pow(input.uv.y, 2.5));

                half3 magentaBody = _BeamColor.rgb * combinedNoise * outerFlame;
                half3 whiteCore = _CoreColor.rgb * innerCore * (noise1.r * 1.4);

                half3 finalRGB = (magentaBody + whiteCore) * heightFade;
                float finalAlpha = saturate(outerFlame * combinedNoise * _BeamColor.a * heightFade) * input.color.a;

                return half4(finalRGB * input.color.rgb, finalAlpha);
            }
            ENDHLSL
        }
    }
}
