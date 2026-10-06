Shader "Hidden/EchoProtocol/PS1PostProcess"
{
    Properties
    {
        _BlitTexture("Source", 2D) = "white" {}
        _ColorDepth("Color Depth", Float) = 32.0
        _GrainIntensity("Grain Intensity", Float) = 0.1
        _GlitchIntensity("Glitch Intensity", Float) = 0.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "PS1PostProcess"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            float _ColorDepth;
            float _GrainIntensity;
            float _GlitchIntensity;

            Varyings vert(Attributes input)
            {
                Varyings output;
                // Blitter.BlitTexture uses a procedural full-screen triangle (3 vertices)
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            // Simple hash function for noise
            float hash(float2 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * (p.x + p.y));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float time = _Time.y;
                half4 color = half4(0,0,0,1);

                // 1. VHS Screen Tearing / Glitch (Intensifies with _GlitchIntensity)
                if (_GlitchIntensity > 0.0)
                {
                    float tearY = frac(time * 0.2); // Slow vertical scroll
                    float tearBand = smoothstep(0.95, 1.0, 1.0 - abs(uv.y - tearY));
                    
                    // High frequency horizontal noise for the tear
                    float jitter = (hash(float2(uv.y * 100.0, time)) - 0.5) * 0.05 * _GlitchIntensity * tearBand;
                    uv.x += jitter;

                    // RGB Split glitch during tearing
                    float split = 0.02 * _GlitchIntensity * tearBand;
                    float r = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + float2(split, 0)).r;
                    float g = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv).g;
                    float b = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv - float2(split, 0)).b;
                    
                    half4 sourceColor = half4(r, g, b, 1.0);
                    half4 baseColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv);
                    color = lerp(baseColor, sourceColor, tearBand * _GlitchIntensity);
                }
                else
                {
                    color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv);
                }

                // 2. Color Depth Reduction (Banding/Posterization)
                // Multiply, floor, divide to reduce available color steps
                float depth = max(_ColorDepth, 1.0);
                color.rgb = floor(color.rgb * depth) / depth;

                // 3. Film Grain
                if (_GrainIntensity > 0.0)
                {
                    float noise = hash(uv * time * 100.0);
                    float grain = (noise - 0.5) * 2.0 * _GrainIntensity;
                    // Multiplicative grain so pitch black stays black
                    color.rgb += grain * color.rgb; 
                }

                return color;
            }
            ENDHLSL
        }
    }
}
