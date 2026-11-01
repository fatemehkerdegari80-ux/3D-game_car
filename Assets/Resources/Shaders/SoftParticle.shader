Shader "NeonHighway/SoftParticle"
{
    Properties
    {
        _Tint ("Particle tint", Color) = (1,1,1,1)
        _Brightness ("Soft brightness", Float) = 1.35
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "Soft particles URP"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Brightness;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                o.color=v.color;
                o.uv=v.uv;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float2 uv=i.uv*2.0-1.0;
                float mask=saturate(1.0-dot(uv,uv));
                mask=mask*mask*(3.0-2.0*mask);
                return half4(i.color.rgb*_Tint.rgb*_Brightness,mask*i.color.a*_Tint.a*.85);
            }
            ENDHLSL
        }
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Tint;
            float _Brightness;
            struct Attributes { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 pos : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.pos=UnityObjectToClipPos(v.vertex);
                o.color=v.color;
                o.uv=v.uv;
                return o;
            }
            fixed4 frag(Varyings i):SV_Target
            {
                float2 uv=i.uv*2.0-1.0;
                float mask=saturate(1.0-dot(uv,uv));
                mask=mask*mask*(3.0-2.0*mask);
                return fixed4(i.color.rgb*_Tint.rgb*_Brightness,mask*i.color.a*_Tint.a*.85);
            }
            ENDCG
        }
    }
    FallBack Off
}
