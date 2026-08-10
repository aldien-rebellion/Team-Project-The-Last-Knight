Shader "Custom/URP_ExcaliburBeam"
{
    Properties
    {
        [MainTexture] _MainTex ("Noise Texture 1", 2D) = "white" {}
        _DetailNoise ("Noise Texture 2", 2D) = "white" {}
        
        [HDR] _AuraColor ("HDR Aura Color", Color) = (0.3, 0.0, 0.5, 1)
        [HDR] _BeamColor ("HDR Beam Color", Color) = (3, 0, 1.5, 1) // High intensity Magenta/Red
        [HDR] _NoiseStreakColor ("HDR Noise Streak Color", Color) = (2.5, 0.0, 0.2, 1)
        [HDR] _CoreColor ("HDR Core Color", Color) = (5, 5, 5, 1)   // Piercing White
        
        _PanSpeed1 ("Primary Panning Speed", Float) = -8.0
        _PanSpeed2 ("Secondary Panning Speed", Float) = -12.0
        _PanSpeed3 ("Tertiary Panning Speed", Float) = -6.0
        
        _CoreThickness ("Core Sharpness", Range(1, 16)) = 8.0
        _AuraWidth ("Aura Width", Range(0.3, 3.0)) = 1.2
        _NoiseDistortion ("Noise Distortion Force", Range(0, 1)) = 0.3
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
            Name "ExcaliburMorganBeam"
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
                float4 _AuraColor;
                float4 _BeamColor;
                float4 _NoiseStreakColor;
                float4 _CoreColor;
                float _PanSpeed1;
                float _PanSpeed2;
                float _PanSpeed3;
                float _CoreThickness;
                float _AuraWidth;
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
                // Dual UV Panning Engine for chaotic liquid plasma energy
                float2 uv1 = input.uv;
                uv1.x += _Time.y * _PanSpeed1;

                float2 uv2 = input.uv * 1.5;
                uv2.x += _Time.y * _PanSpeed2;
                
                // Tertiary UV for Crimson Streaks
                float2 uv3 = input.uv * 1.2;
                uv3.x += _Time.y * _PanSpeed3;

                // Sample primary, secondary, and tertiary turbulent noise
                half4 noise1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv1);
                half4 noise2 = SAMPLE_TEXTURE2D(_DetailNoise, sampler_DetailNoise, uv2);
                half4 noise3 = SAMPLE_TEXTURE2D(_DetailNoise, sampler_DetailNoise, uv3);

                float combinedNoise = saturate((noise1.r + noise2.r) * 0.6);
                
                // High contrast streak mask
                float streakMask = smoothstep(0.4, 0.7, noise3.r);

                // Vertical Beam Mask with Noise Distortion (pulsating plasma beam edges)
                float distortedY = input.uv.y + (combinedNoise - 0.5) * _NoiseDistortion;
                float distFromCenter = abs(distortedY - 0.5) * 2.0; // 0 at centerline, 1 at edges

                // Base edge mask for the main beam body
                float edgeMask = saturate(1.0 - distFromCenter);
                
                // Outer Aura Mask
                float auraMask = saturate(1.0 - (distFromCenter / _AuraWidth));
                float outerAura = pow(auraMask, 0.8) * combinedNoise; // Add some noise to the aura fade

                float outerFlame = pow(edgeMask, 1.8);
                float innerCore = pow(edgeMask, _CoreThickness);

                // High contrast dark fantasy color layers:
                // Black/Dark Red outer halo -> Dark Magenta -> Crimson Streaks -> Piercing White Core
                half3 auraColor = _AuraColor.rgb * outerAura;
                half3 magentaBody = _BeamColor.rgb * combinedNoise * outerFlame;
                half3 streaks = _NoiseStreakColor.rgb * streakMask * outerFlame;
                half3 whiteCore = _CoreColor.rgb * innerCore * (noise1.r * 1.2);

                half3 finalRGB = auraColor + magentaBody + streaks + whiteCore;
                
                // Combine alphas for the additive blend
                float finalAlpha = saturate(outerAura * _AuraColor.a + outerFlame * combinedNoise * _BeamColor.a) * input.color.a;

                return half4(finalRGB * input.color.rgb, finalAlpha);
            }
            ENDHLSL
        }
    }
}
