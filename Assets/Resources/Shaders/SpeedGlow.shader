Shader "NeonHighway/SpeedGlow"
{
    Properties { _GlowColor ("HDR glow color", Color) = (0.1,0.9,1,1) }
    // URP / Unity 6: unlit additive geometry, speed controls HDR RGB and size.
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "Speed glow URP"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha One
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                o.color=v.color;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                return half4(_GlowColor.rgb, saturate(_GlowColor.a*i.color.a));
            }
            ENDHLSL
        }
    }
    // Built-in pipeline fallback without a URP dependency.
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha One
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            fixed4 _GlowColor;
            struct AppData { float4 vertex : POSITION; fixed4 color : COLOR; };
            struct Varyings { float4 pos : SV_POSITION; fixed4 color : COLOR; };
            Varyings Vert(AppData v)
            {
                Varyings o;
                o.pos=UnityObjectToClipPos(v.vertex);
                o.color=v.color;
                return o;
            }
            fixed4 Frag(Varyings i):SV_Target
            {
                return fixed4(_GlowColor.rgb, saturate(_GlowColor.a*i.color.a));
            }
            ENDCG
        }
    }
    FallBack Off
}
