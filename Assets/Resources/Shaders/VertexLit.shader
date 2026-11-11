// Main material for all procedurally built geometry.
//   vertex color rgb : albedo
//   vertex color a   : 1 = normal surface, 0 = emissive at night (windows, lamps)
//   uv2.x / uv2.y    : smoothness / metallic
//   uv2.z            : surface detail layer + 1 (0 = plain), see SurfaceKind in Materials.cs
Shader "Crosswind/VertexLit"
{
    Properties
    {
        _MainTex ("Detail (grayscale)", 2D) = "white" {}
        _DetailScale ("Detail Scale", Float) = 0
        _DetailStrength ("Detail Strength", Range(0,1)) = 0
        _KitSurface ("Ignore uv2 surface (kit models)", Float) = 0
    }

    // ------------------------------------------------------------------ URP (Universal Render Pipeline)
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float _DetailScale;
            float _DetailStrength;
            float _KitSurface;  // 1 = the mesh has no surface channel (free kit models): matte, no detail
        CBUFFER_END
        float _NightGlow;
        float4 _GlowColor;
        float _SurfaceDetailOn; // global, 1 once the detail array is bound
        TEXTURE2D_ARRAY(_SurfaceDetail); SAMPLER(sampler_SurfaceDetail);
        // Per layer: meters covered by one texture tile, and how strongly the detail shows.
        static const float kDetailTile[14] = { 1.6, 2.4, 2.0, 2.2, 2.0, 2.4, 3.0, 2.0, 2.0, 1.6, 1.4, 2.0, 1.5, 3.0 };
        static const float kDetailStrength[14] = { 0.85, 0.7, 0.75, 0.75, 0.6, 0.75, 0.45, 0.35, 0.55, 0.8, 0.5, 0.6, 0.9, 0.7 };
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float3 uv1 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 sm : TEXCOORD2;
                float fogCoord : TEXCOORD3;
                float3 positionOS : TEXCOORD4;
                float3 normalOS : TEXCOORD5;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.sm = _KitSurface > 0.5 ? float3(0.12, 0, 0) : v.uv1;
                o.positionOS = v.positionOS.xyz;
                o.normalOS = v.normalOS;
                o.fogCoord = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 albedo = i.color.rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    albedo = SRGBToLinear(albedo); // vertex colors are authored in sRGB
                #endif
                if (_DetailScale > 0)
                {
                    float d = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.positionWS.xz * _DetailScale).r;
                    albedo *= lerp(1, d * 1.6 + 0.2, _DetailStrength);
                }
                float glow = 1 - i.color.a;
                float3 normalWS = normalize(i.normalWS);
                float occlusion = 1;

                // Surface detail: one fetch, projected along the face's dominant object-space axis
                // (the procedural geometry is made of boxes, so faces are mostly axis-aligned).
                int layer = (int)(i.sm.z + 0.5) - 1;
                float3 dpdx = ddx(i.positionOS), dpdy = ddy(i.positionOS); // outside the branch: gradients must be uniform
                UNITY_BRANCH if (layer >= 0 && _SurfaceDetailOn > 0.5)
                {
                    float3 an = abs(i.normalOS);
                    float2 uv, gx, gy; float3 tOS, bOS;
                    if (an.y >= an.x && an.y >= an.z) { uv = i.positionOS.xz; gx = dpdx.xz; gy = dpdy.xz; tOS = float3(1, 0, 0); bOS = float3(0, 0, 1); }
                    else if (an.x >= an.z) { uv = i.positionOS.zy; gx = dpdx.zy; gy = dpdy.zy; tOS = float3(0, 0, 1); bOS = float3(0, 1, 0); }
                    else { uv = i.positionOS.xy; gx = dpdx.xy; gy = dpdy.xy; tOS = float3(1, 0, 0); bOS = float3(0, 1, 0); }
                    float tile = kDetailTile[layer];
                    float4 d = SAMPLE_TEXTURE2D_ARRAY_GRAD(_SurfaceDetail, sampler_SurfaceDetail, uv / tile, layer, gx / tile, gy / tile);
                    float k = kDetailStrength[layer];
                    albedo *= lerp(1, d.r * 2, k);
                    occlusion = lerp(1, d.b, k);
                    float2 nxy = (float2(d.a, d.g) * 2 - 1) * k;
                    float3 tWS = TransformObjectToWorldDir(tOS), bWS = TransformObjectToWorldDir(bOS);
                    normalWS = normalize(normalWS + nxy.x * tWS + nxy.y * bWS);
                }

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.fogCoord = i.fogCoord;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo * (1 - glow * 0.6);
                s.metallic = i.sm.y;
                s.smoothness = i.sm.x;
                s.occlusion = occlusion;
                s.alpha = 1;
                s.emission = glow * _NightGlow * _GlowColor.rgb * (0.6 + albedo);

                half4 c = UniversalFragmentPBR(inputData, s);
                c.rgb = MixFog(c.rgb, inputData.fogCoord);
                return c;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 vertShadow(A a) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(a);
                float3 ws = TransformObjectToWorld(a.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(a.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 ld = normalize(_LightPosition - ws);
                #else
                    float3 ld = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(ws, n, ld));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return cs;
            }
            half4 fragShadow() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing
            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 vertDepth(A a) : SV_POSITION { UNITY_SETUP_INSTANCE_ID(a); return TransformObjectToHClip(a.positionOS.xyz); }
            half fragDepth() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vertDN
            #pragma fragment fragDN
            #pragma multi_compile_instancing
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            V vertDN(A a)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(a);
                o.positionCS = TransformObjectToHClip(a.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(a.normalOS);
                return o;
            }
            half4 fragDN(V i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0.0); }
            ENDHLSL
        }
    }

    // ------------------------------------------------------------------ Built-in pipeline fallback
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0
        #pragma multi_compile_instancing

        sampler2D _MainTex;
        float _DetailScale;
        float _DetailStrength;
        float _NightGlow;      // global, set by DayNightCycle
        float4 _GlowColor;     // global

        struct Input
        {
            float4 vcol;
            float2 sm;
            float3 worldPos;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.vcol = v.color;
            o.sm = v.texcoord1.xy;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 albedo = IN.vcol.rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
                albedo = GammaToLinearSpace(albedo); // vertex colors are authored in sRGB
            #endif
            if (_DetailScale > 0)
            {
                float d = tex2D(_MainTex, IN.worldPos.xz * _DetailScale).r;
                albedo *= lerp(1, d * 1.6 + 0.2, _DetailStrength);
            }
            float glow = (1 - IN.vcol.a);
            o.Albedo = albedo * (1 - glow * 0.6);
            o.Smoothness = IN.sm.x;
            o.Metallic = IN.sm.y;
            o.Emission = glow * _NightGlow * _GlowColor.rgb * (0.6 + albedo);
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
