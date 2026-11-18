Shader "_Project/HologramHighlightShader"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        [HDR] _HighlightColor ("Highlight Color", Color) = (1.0, 0.35, 0.0, 1.0)
        [HDR] _CoreColor ("Hot Core Color", Color) = (1.0, 0.9, 0.6, 1.0)
        _CoreThreshold ("Core Threshold", Range(0.0, 1.0)) = 0.65
        _Alpha ("Alpha", Range(0.0, 1.0)) = 0.85
        _PulseSpeed ("Pulse Speed", Float) = 4.0
        _PulseMin ("Pulse Min Brightness", Range(0.1, 1.0)) = 0.65
        _ScanlineTiling ("Scanline Frequency", Float) = 80.0
        _ScanlineSpeed ("Scanline Speed", Float) = 4.0
        _ScanlineStrength ("Scanline Strength", Range(0.0, 1.0)) = 0.25
        _GlitchSpeed ("Glitch Speed", Float) = 8.0
        _GlitchIntensity ("Glitch Intensity", Range(0.0, 0.5)) = 0.03
        _GlitchFrequency ("Glitch Frequency", Range(0.0, 1.0)) = 0.85
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+1" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        Pass
        {
            Name "HologramHighlightPass"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _HighlightColor;
                float4 _CoreColor;
                float _CoreThreshold;
                float _Alpha;
                float _PulseSpeed;
                float _PulseMin;
                float _ScanlineTiling;
                float _ScanlineSpeed;
                float _ScanlineStrength;
                float _GlitchSpeed;
                float _GlitchIntensity;
                float _GlitchFrequency;
            CBUFFER_END

            float Hash(float n)
            {
                return frac(sin(n) * 43758.5453123);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 posOS = input.positionOS.xyz;

                float timeStep = floor(_Time.y * _GlitchSpeed);
                float glitchTrigger = step(_GlitchFrequency, Hash(timeStep));
                float slice = floor(posOS.y * 12.0 + timeStep);
                float sliceNoise = Hash(slice) * 2.0 - 1.0;
                float sliceTrigger = step(0.65, Hash(slice * 1.7));

                posOS.x += sliceNoise * _GlitchIntensity * glitchTrigger * sliceTrigger;
                posOS.z += (Hash(slice + 3.1) * 2.0 - 1.0) * (_GlitchIntensity * 0.5) * glitchTrigger * sliceTrigger;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(posOS);

                output.positionHCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float mask = texColor.a * dot(texColor.rgb, float3(0.299, 0.587, 0.114));

                float pulse = lerp(_PulseMin, 1.0, sin(_Time.y * _PulseSpeed) * 0.5 + 0.5);

                float scanline = sin((input.positionWS.y * _ScanlineTiling) - (_Time.y * _ScanlineSpeed)) * 0.5 + 0.5;
                scanline = lerp(1.0 - _ScanlineStrength, 1.0, scanline);

                float coreFactor = smoothstep(_CoreThreshold, 1.0, mask);
                float3 finalRGB = lerp(_HighlightColor.rgb, _CoreColor.rgb, coreFactor) * pulse * scanline;

                float finalAlpha = saturate(mask * _Alpha * pulse * scanline);

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }
}
