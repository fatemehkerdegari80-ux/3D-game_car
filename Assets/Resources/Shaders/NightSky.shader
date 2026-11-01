Shader "NeonHighway/NightSky"
{
    // Fully procedural night skybox: horizon haze that matches the scene fog, a magenta
    // afterglow band, drifting clouds lit from below, and twinkling stars.
    // No texture needed, and because it lives in Resources it is always included in builds
    // (Shader.Find("Skybox/Panoramic") is NOT guaranteed to exist in a player build).
    Properties
    {
        _FogTint ("Horizon haze (match fog)", Color) = (0.085,0.048,0.15,1)
        _Zenith  ("Zenith",            Color) = (0.006,0.009,0.034,1)
        _Mid     ("Upper sky",         Color) = (0.030,0.026,0.105,1)
        _Glow    ("Horizon afterglow", Color) = (0.58,0.16,0.40,1)
        _Warm    ("Low warm band",     Color) = (0.95,0.40,0.28,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _FogTint;
            float4 _Zenith;
            float4 _Mid;
            float4 _Glow;
            float4 _Warm;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float noise3(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = hash13(i);
                float n100 = hash13(i + float3(1, 0, 0));
                float n010 = hash13(i + float3(0, 1, 0));
                float n110 = hash13(i + float3(1, 1, 0));
                float n001 = hash13(i + float3(0, 0, 1));
                float n101 = hash13(i + float3(1, 0, 1));
                float n011 = hash13(i + float3(0, 1, 1));
                float n111 = hash13(i + float3(1, 1, 1));
                return lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
                            lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
            }

            float fbm(float3 p)
            {
                float a = 0.5;
                float s = 0.0;
                for (int k = 0; k < 3; k++)
                {
                    s += a * noise3(p);
                    p *= 2.03;
                    a *= 0.5;
                }
                return s;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;

                // Vertical gradient: haze -> afterglow -> violet -> deep indigo.
                float3 col = _FogTint.rgb;
                col = lerp(col, _Glow.rgb, smoothstep(0.0, 0.05, y));
                float band = (y - 0.032) / 0.034;
                col += _Warm.rgb * exp(-band * band) * 0.50;
                col = lerp(col, _Mid.rgb, smoothstep(0.06, 0.40, y));
                col = lerp(col, _Zenith.rgb, smoothstep(0.34, 1.0, y));

                // Slow-moving clouds that catch the horizon glow.
                float cl = fbm(d * float3(3.2, 8.5, 3.2) + float3(0.0, 0.0, _Time.x * 0.35));
                float cmask = smoothstep(0.015, 0.14, y) * (1.0 - smoothstep(0.16, 0.42, y));
                float c = smoothstep(0.46, 0.80, cl) * cmask;
                col = lerp(col, col * 0.45 + _Glow.rgb * 0.60, c * 0.70);

                // Stars: one random point per 3D cell on a shell around the camera.
                float3 sp = d * 90.0;
                float3 cell = floor(sp);
                float3 fr = frac(sp) - 0.5;
                float h = hash13(cell);
                float3 off = (float3(hash13(cell + 7.1), hash13(cell + 13.7), hash13(cell + 3.3)) - 0.5) * 0.5;
                float star = step(0.984, h) * smoothstep(0.22, 0.0, length(fr - off));
                star *= 0.65 + 0.35 * sin(_Time.y * (1.5 + h * 6.0) + h * 60.0);
                star *= smoothstep(0.03, 0.30, y) * (1.0 - c);
                float3 starCol = lerp(float3(0.75, 0.85, 1.0), float3(1.0, 0.88, 0.75), frac(h * 91.7));
                col += starCol * star * 1.8;

                return float4(col, 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
