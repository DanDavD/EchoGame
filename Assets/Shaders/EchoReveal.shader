// EchoReveal - URP unlit shader.
// Everything is pitch black except inside any of the echo spheres published each frame by
// EchoShaderPublisher (one sphere per connected player, max 4):
//   _EchoSources[i] (xyz = world position, w = radius in m)
//   _EchoFalloffs   (x,y,z,w = soft edge width in m for sources 0..3)
// A source with radius 0 is inactive.
//
// The surface is procedural (no textures): _Pattern picks bricks (walls), slabs (floors), rock (props) or flesh
// (monster), projected in WORLD space so scaled cubes never stretch the pattern. Inside a sphere the surface is
// lit from the sphere's centre (surfaces facing the voice are brighter), the edge glows (HDR, so bloom picks it up),
// a pulse wave travels towards the edge and a rim light outlines the silhouettes.
Shader "EchoProtocol/EchoReveal"
{
    Properties
    {
        _BaseMap("Base Map (unused, kept for old materials)", 2D) = "white" {}
        _Tint("Tint", Color) = (0.6, 0.6, 0.6, 1)
        [HDR] _EdgeColor("Edge Color (glowing ring)", Color) = (0.2, 0.9, 1.0, 1)
        _TexScale("Pattern scale (1 = default size)", Float) = 1
        _Pattern("Pattern: 0 bricks, 1 slabs, 2 rock, 3 flesh", Float) = 0
        [HDR] _Emission("Emission (only seen inside an echo)", Color) = (0, 0, 0, 1)
        _EmissionInDark("Emission visible in the dark (0..1)", Range(0, 1)) = 0
        _PulseSpeed("Emission pulse speed", Float) = 0
        _PortalActive("Portal energy (0 dormant, 1 open)", Range(0, 1)) = 0
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
                half4 _Emission;
                float _TexScale;
                float _Pattern;
                float _EmissionInDark;
                float _PulseSpeed;
                float _PortalActive;
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
                float3 positionOS : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            // ---- Procedural surface patterns ------------------------------------------------------------------

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    v += a * ValueNoise(p);
                    p = p * 2.03 + 17.1;
                    a *= 0.5;
                }
                return v;
            }

            // Thin dark lines where the noise crosses 0.5: cracks.
            float Cracks(float2 p)
            {
                return 1.0 - smoothstep(0.0, 0.011, abs(Fbm(p) - 0.5));
            }

            // Running-bond brick wall, bricks 1.0 m x 0.5 m.
            float3 BrickPattern(float2 uv)
            {
                float row = floor(uv.y / 0.5);
                float shift = abs(fmod(row, 2.0)) * 0.5;
                float bx = uv.x + shift;
                float by = uv.y / 0.5;
                float2 id = float2(floor(bx), floor(by));
                float2 f = float2(frac(bx), frac(by));

                float mortar = saturate(step(f.x, 0.035) + step(f.y, 0.075));
                float tone = 0.5 + 0.5 * Hash21(id);
                float wear = Fbm(uv * 3.5);
                float chip = smoothstep(0.0, 0.14, min(min(f.x, 1.0 - f.x), min(f.y, 1.0 - f.y) * 2.0));
                float3 hue = lerp(float3(1.0, 0.94, 0.88), float3(0.88, 0.94, 1.0), Hash21(id + 7.7));

                float3 brick = hue * tone * (0.55 + 0.7 * wear) * (0.7 + 0.3 * chip);
                brick *= 1.0 - 0.4 * Cracks(uv * 1.3 + id * 0.17);
                return lerp(brick, float3(0.04, 0.04, 0.045), mortar);
            }

            // Big irregular floor slabs, about 1.6 m across.
            float3 SlabPattern(float2 uv)
            {
                float2 g = uv / 1.6;
                float2 id = floor(g);
                float2 f = frac(g);
                float edge = min(min(f.x, 1.0 - f.x), min(f.y, 1.0 - f.y));
                float grout = 1.0 - smoothstep(0.012, 0.045, edge);

                float tone = 0.45 + 0.55 * Hash21(id);
                float dirt = Fbm(uv * 2.6);
                float3 slab = float3(1.0, 0.98, 0.95) * tone * (0.5 + 0.8 * dirt);
                slab *= 1.0 - 0.45 * Cracks(uv * 1.1 + id * 0.31);
                return lerp(slab, float3(0.025, 0.025, 0.03), grout);
            }

            // Rough layered rock for pillars, altar and props.
            float3 RockPattern(float2 uv)
            {
                float n = Fbm(uv * 1.6);
                float strata = sin(uv.y * 5.5 + n * 7.0) * 0.5 + 0.5;
                float v = 0.38 + 0.45 * n + 0.2 * strata;
                float3 rock = float3(0.95, 1.0, 0.97) * v;
                rock *= 1.0 - 0.35 * Cracks(uv * 2.1);
                return rock;
            }

            // Wet dark flesh with bright veins (the monster).
            float3 FleshPattern(float2 uv)
            {
                float warp = Fbm(uv * 2.0);
                float n = Fbm(uv * 3.2 + warp * 2.2);
                float veins = 1.0 - smoothstep(0.0, 0.07, abs(n - 0.5));
                float3 flesh = float3(0.45, 0.1, 0.12) * (0.55 + 0.8 * n);
                return flesh + float3(0.9, 0.18, 0.2) * veins * 0.7;
            }

            // The escape portal: a slab of dark stone that, once open, shows swirling energy. Uses object-space
            // coordinates (unit cube, -0.5..0.5) so the swirl is centred on the gateway whatever its world position.
            float3 PortalEnergy(float3 posOS)
            {
                float2 p = posOS.xy * float2(1.0, 1.2);
                float r = length(p);
                float a = atan2(p.y, p.x);
                float t = _Time.y;
                float warp = Fbm(p * 5.0 + t * 0.35);
                float swirl = sin(a * 3.0 + r * 14.0 - t * 2.6 + warp * 5.0) * 0.5 + 0.5;
                float core = 1.0 - smoothstep(0.0, 0.5, r);
                float frame = smoothstep(0.33, 0.5, max(abs(posOS.x), abs(posOS.y)));
                float e = saturate(swirl * 0.8 + core * 0.8 - frame * 0.5);
                return _EdgeColor.rgb * (0.3 + 1.1 * e);
            }

            float3 SurfacePattern(float2 uv)
            {
                uv /= max(_TexScale, 0.01);
                if (_Pattern < 0.5) return BrickPattern(uv);
                if (_Pattern < 1.5) return SlabPattern(uv);
                if (_Pattern < 2.5) return RockPattern(uv);
                if (_Pattern < 3.5) return FleshPattern(uv);
                return RockPattern(uv) * 0.4; // dormant portal stone
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);

                // Triplanar-lite: pick the projection plane of the dominant axis of the surface normal.
                float3 n = abs(N);
                float2 uv = (n.y > n.x && n.y > n.z) ? IN.positionWS.xz
                          : (n.x > n.z ? IN.positionWS.zy : IN.positionWS.xy);
                float3 albedo = SurfacePattern(uv) * _Tint.rgb;

                // An open portal is a beacon: its energy shows in the dark, not only inside an echo.
                float3 portal = _Pattern > 3.5 ? PortalEnergy(IN.positionOS) * _PortalActive : float3(0.0, 0.0, 0.0);

                // 1. Permanent Sanctuary / Debug Full Illumination override
                if (_FullIllumination > 0.5)
                {
                    half3 golden = half3(1.0, 0.90, 0.65);
                    float shade = 0.65 + 0.35 * saturate(dot(N, normalize(float3(0.35, 0.85, 0.25))));
                    return half4(albedo * 1.8 * golden * shade + _Emission.rgb + portal, 1.0);
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
                float wave = 0.0;
                float bestWeight = 0.0;
                float3 bestSource = float3(0.0, 0.0, 0.0);
                float bestAtten = 0.0;

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
                    ring = max(ring, pow(m * (1.0 - m) * 4.0, 3.0));
                    mask = max(mask, m);

                    // A pulse that travels towards the edge, strongest in the outer part of the sphere.
                    float travelling = sin((dist - _Time.y * 5.0) * 1.1) * 0.5 + 0.5;
                    float outer = smoothstep(radius * 0.35, radius, dist) * m;
                    wave = max(wave, pow(travelling, 10.0) * outer);

                    // The brightest source lights the surface (voice-centred lighting).
                    float atten = 1.0 - saturate(dist / max(radius, 0.001));
                    float weight = m * (0.4 + 0.6 * atten);
                    if (weight > bestWeight)
                    {
                        bestWeight = weight;
                        bestSource = sources[i].xyz;
                        bestAtten = atten;
                    }
                }

                // Voice-centred shading: faces turned towards the source are brighter, distance dims the light.
                float3 L = normalize(bestSource - IN.positionWS + float3(0.0, 0.001, 0.0));
                float facing = saturate(dot(N, L));
                float shade = (0.35 + 0.65 * facing) * (0.5 + 0.5 * bestAtten);

                float rim = pow(1.0 - saturate(dot(N, V)), 2.6);

                float3 color = albedo * shade * mask * 1.15;
                color += _EdgeColor.rgb * ring * 0.6;
                color += _EdgeColor.rgb * wave * 0.12;
                color += _EdgeColor.rgb * rim * mask * 0.07;

                float pulse = _PulseSpeed > 0.0 ? 0.75 + 0.25 * sin(_Time.y * _PulseSpeed) : 1.0;
                color += _Emission.rgb * pulse * max(mask, _EmissionInDark);
                color += portal;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
