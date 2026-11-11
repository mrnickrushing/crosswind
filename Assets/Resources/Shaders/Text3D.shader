// Lettering in the world (shop signs): the font's texture, tinted, hidden behind anything in front of it.
// (Unity's own text shader draws through walls.)
Shader "Crosswind/Text3D"
{
    Properties { _MainTex ("Font Texture", 2D) = "white" {} _Color ("Text Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Lighting Off Cull Off ZWrite Off ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct appdata { float4 vertex : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST; half4 _Color;
            CBUFFER_END
            v2f vert(appdata v) { v2f o; o.pos = TransformObjectToHClip(v.vertex.xyz); o.color = v.color * _Color; o.uv = TRANSFORM_TEX(v.uv, _MainTex); return o; }
            half4 frag(v2f i) : SV_Target { half4 c = i.color; c.a *= SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a; return c; }
            ENDHLSL
        }
    }
}
