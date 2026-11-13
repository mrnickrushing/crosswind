// The tutorial's dimmer: darkens the whole screen except a soft-edged rounded hole over the thing to tap, with a
// glowing rim round the hole. The hole is given in the image's own local units (the canvas units of the overlay).
Shader "Crosswind/Spotlight"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Dim", Color) = (0.02, 0.04, 0.08, 0.66)
        _Hole ("Hole (centre xy, half size zw)", Vector) = (0, 0, 0, 0)
        _Radius ("Corner radius", Float) = 26
        _Soft ("Edge softness", Float) = 22
        _RimColor ("Rim", Color) = (1, 0.8, 0.3, 1)
        _RimWidth ("Rim width", Float) = 7
        _RimAlpha ("Rim strength", Float) = 0.9
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float2 local : TEXCOORD0; };

            fixed4 _Color, _RimColor;
            float4 _Hole;
            float _Radius, _Soft, _RimWidth, _RimAlpha;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.local = v.vertex.xy;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                if (_Hole.z <= 0) return i.color;
                // Signed distance to the rounded rectangle (negative inside).
                float r = min(_Radius, min(_Hole.z, _Hole.w));
                float2 d = abs(i.local - _Hole.xy) - (_Hole.zw - r);
                float dist = length(max(d, 0)) + min(max(d.x, d.y), 0) - r;
                float dim = smoothstep(0, _Soft, dist) * i.color.a;
                float rim = exp(-pow((dist - _RimWidth * 0.5) / _RimWidth, 2)) * _RimAlpha;
                float a = max(dim, rim);
                float3 rgb = lerp(i.color.rgb, _RimColor.rgb, rim / max(a, 1e-4));
                return fixed4(rgb, a);
            }
            ENDCG
        }
    }
}
