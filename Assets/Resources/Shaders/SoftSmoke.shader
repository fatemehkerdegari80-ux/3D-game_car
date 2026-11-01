Shader "NeonHighway/SoftSmoke"
{
    Properties { _Tint ("Smoke tint", Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Name "Feathered exhaust URP"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
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
                float d=length(uv);
                float mask=saturate((1.0-d)*1.22);
                mask=mask*mask*(3.0-2.0*mask);
                return half4(i.color.rgb*_Tint.rgb,mask*i.color.a*_Tint.a);
            }
            ENDHLSL
        }
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Tint;
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
                float d=length(uv);
                float mask=saturate((1.0-d)*1.22);
                mask=mask*mask*(3.0-2.0*mask);
                return fixed4(i.color.rgb*_Tint.rgb,mask*i.color.a*_Tint.a);
            }
            ENDCG
        }
    }
    FallBack Off
}
