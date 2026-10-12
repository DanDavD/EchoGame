// EchoReveal - URP unlit shader.
// Everything is pitch black except inside any of the echo spheres published each frame by
// EchoShaderPublisher (one sphere per connected player, max 4):
//   _EchoSources[i] (xyz = world position, w = radius in m)
//   _EchoFalloffs   (x,y,z,w = soft edge width in m for sources 0..3)
// A source with radius 0 is inactive. Textures are projected in WORLD space (triplanar-lite),
// so scaled cubes never stretch the texture.
Shader "EchoProtocol/EchoReveal"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _Tint("Tint", Color) = (0.6, 0.6, 0.6, 1)
        [HDR] _EdgeColor("Edge Color (glowing ring)", Color) = (0.2, 0.9, 1.0, 1)
        _TexScale("Texture Scale (repeats per metre)", Float) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "DisableBatching" = "True" }

        Pass
        {
            Name "EchoForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define ECHO_MAX_SOURCES 4

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Tint;
                half4 _EdgeColor;
                float _TexScale;
            CBUFFER_END

            // Dedicated constant buffer for dynamic global echolocation parameters.
            CBUFFER_START(EchoGlobals)
                float4 _EchoSources[ECHO_MAX_SOURCES]; // xyz = origin, w = radius
                float4 _EchoFalloffs;                  // soft edge width per source
                float4 _EchoSource0;
                float4 _EchoSource1;
                float4 _EchoSource2;
                float4 _EchoSource3;
                float4 _EchoOrigin;
                float _EchoRadius;
                float _EchoFalloff;
                float _FullIllumination;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Triplanar world-space projected texture
                float3 n = abs(normalize(IN.normalWS));
                float2 uv = (n.y > n.x && n.y > n.z) ? IN.positionWS.xz
                          : (n.x > n.z ? IN.positionWS.zy : IN.positionWS.xy);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv * _TexScale);

                // 1. Permanent Sanctuary / Debug Full Illumination override
                if (_FullIllumination > 0.5)
                {
                    half3 golden = half3(1.0, 0.90, 0.65);
                    half3 litColor = tex.rgb * _Tint.rgb * 1.6 * golden;
                    return half4(litColor, 1.0);
                }

                // 2. Gather active sources with robust fallbacks
                float4 sources[ECHO_MAX_SOURCES];
                sources[0] = _EchoSource0.w > 0.001 ? _EchoSource0 : (_EchoRadius > 0.001 ? float4(_EchoOrigin.xyz, _EchoRadius) : _EchoSources[0]);
                sources[1] = _EchoSource1.w > 0.001 ? _EchoSource1 : _EchoSources[1];
                sources[2] = _EchoSource2.w > 0.001 ? _EchoSource2 : _EchoSources[2];
                sources[3] = _EchoSource3.w > 0.001 ? _EchoSource3 : _EchoSources[3];

                float falloffs[ECHO_MAX_SOURCES];
                falloffs[0] = _EchoFalloff > 0.001 ? _EchoFalloff : (_EchoFalloffs.x > 0.001 ? _EchoFalloffs.x : 2.0);
                falloffs[1] = _EchoFalloffs.y > 0.001 ? _EchoFalloffs.y : 2.0;
                falloffs[2] = _EchoFalloffs.z > 0.001 ? _EchoFalloffs.z : 2.0;
                falloffs[3] = _EchoFalloffs.w > 0.001 ? _EchoFalloffs.w : 2.0;

                float mask = 0.0;
                float ring = 0.0;

                [unroll]
                for (int i = 0; i < ECHO_MAX_SOURCES; i++)
                {
                    float radius = sources[i].w;
                    if (radius <= 0.001) continue;

                    float falloff = falloffs[i];
                    float dist = distance(IN.positionWS, sources[i].xyz);
                    float edgeStart = max(radius - falloff, 0.0);
                    float edgeEnd = radius + 0.01;
                    float m = 1.0 - smoothstep(edgeStart, edgeEnd, dist);

                    // Glowing boundary ring
                    ring = max(ring, m * (1.0 - m) * 4.0);
                    mask = max(mask, m);
                }

                half3 color = tex.rgb * _Tint.rgb * mask + _EdgeColor.rgb * ring;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
