Shader "_Project/HologramShader"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        [HDR] _HoloColor ("Hologram Color", Color) = (0.0, 0.65, 1.0, 1.0)
        [HDR] _RimColor ("Rim Color", Color) = (0.2, 0.9, 1.0, 1.0)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 2.5
        _Alpha ("Base Alpha", Range(0.0, 1.0)) = 0.45
        _ScanlineTiling ("Scanline Frequency", Float) = 60.0
        _ScanlineSpeed ("Scanline Speed", Float) = 3.0
        _ScanlineStrength ("Scanline Strength", Range(0.0, 1.0)) = 0.35
        _GlitchSpeed ("Glitch Speed", Float) = 8.0
        _GlitchIntensity ("Glitch Intensity", Range(0.0, 0.5)) = 0.03
        _GlitchFrequency ("Glitch Frequency", Range(0.0, 1.0)) = 0.85
        _FlickerSpeed ("Flicker Speed", Float) = 25.0
        _FlickerAmount ("Flicker Amount", Range(0.0, 0.5)) = 0.15
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
            Name "HologramPass"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _HoloColor;
                float4 _RimColor;
                float _RimPower;
                float _Alpha;
                float _ScanlineTiling;
                float _ScanlineSpeed;
                float _ScanlineStrength;
                float _GlitchSpeed;
                float _GlitchIntensity;
                float _GlitchFrequency;
                float _FlickerSpeed;
                float _FlickerAmount;
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
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionHCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                float glitchTime = floor(_Time.y * (_GlitchSpeed * 1.5));
                float uvGlitchTrigger = step(_GlitchFrequency, Hash(glitchTime + 11.4));
                float band = floor(input.positionWS.y * 25.0 + glitchTime);
                uv.x += (Hash(band) * 2.0 - 1.0) * (_GlitchIntensity * 0.5) * uvGlitchTrigger;

                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                float luminance = dot(texColor.rgb, float3(0.299, 0.587, 0.114));

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                float ndotv = saturate(dot(normalWS, viewDirWS));
                float fresnel = pow(1.0 - ndotv, _RimPower);

                float scanline = sin((input.positionWS.y * _ScanlineTiling) - (_Time.y * _ScanlineSpeed)) * 0.5 + 0.5;
                scanline = lerp(1.0 - _ScanlineStrength, 1.0, scanline);

                float flicker = 1.0 - (_FlickerAmount * Hash(floor(_Time.y * _FlickerSpeed)));

                float3 baseHolo = _HoloColor.rgb * (luminance * 1.35 + 0.15);
                float3 finalRGB = (baseHolo + _RimColor.rgb * fresnel) * scanline * flicker;

                float finalAlpha = saturate((_Alpha * (luminance * 0.6 + 0.4) + fresnel * 0.6) * scanline * flicker);

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }
}