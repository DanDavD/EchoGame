// EchoReveal - URP unlit shader.
// Everything is pitch black except inside the sphere defined by the global values that
// EchoVisionController publishes every frame:
//   _EchoOrigin (xyz = world position), _EchoRadius (m), _EchoFalloff (soft edge width, m)
// Textures are projected in WORLD space (triplanar-lite), so scaled cubes never stretch the texture.
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
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "EchoForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Tint;
                half4 _EdgeColor;
                float _TexScale;
            CBUFFER_END

            // Globals set from C# (Shader.SetGlobal*). Must live OUTSIDE UnityPerMaterial.
            float4 _EchoOrigin;
            float _EchoRadius;
            float _EchoFalloff;

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
                // 1. Distance to the echo origin -> sphere mask (1 inside, soft fade to 0 at the edge).
                float dist = distance(IN.positionWS, _EchoOrigin.xyz);
                float edgeStart = max(_EchoRadius - _EchoFalloff, 0.0);
                float edgeEnd = _EchoRadius + 0.01; // +0.01 avoids smoothstep(0,0) when radius = 0
                float mask = 1.0 - smoothstep(edgeStart, edgeEnd, dist);

                // 2. Glowing ring that peaks at the sphere edge (bell curve of the mask).
                float ring = mask * (1.0 - mask) * 4.0;

                // 3. World-space projected texture (pick the axis the surface faces most).
                float3 n = abs(normalize(IN.normalWS));
                float2 uv = (n.y > n.x && n.y > n.z) ? IN.positionWS.xz
                          : (n.x > n.z ? IN.positionWS.zy : IN.positionWS.xy);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv * _TexScale);

                half3 color = tex.rgb * _Tint.rgb * mask + _EdgeColor.rgb * ring;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
