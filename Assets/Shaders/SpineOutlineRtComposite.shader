// SpineOutlineRtComposite - SkeletonOutlineRt(RT) 가 합쳐진 RT 실루엣에 "바깥 외곽선 한 줄"을 그린다.
// RT 는 캐릭터가 한 장으로 합쳐진 premultiplied 결과 -> 부위 내부 seam 없음. 비어있는 픽셀이
// _OutlineWidthPixels 반경 안에 채워진 이웃을 가지면 외곽선. (per-part 셰이더로는 불가능한 전체 윤곽)
Shader "SpineFxLab/SpineOutlineRtComposite"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Render Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineWidthPixels ("Outline Width (RT px)", Float) = 6
        _RtTexelSize ("RT Texel Size", Vector) = (0.001,0.001,1024,1024)

        [HideInInspector] _StencilRef ("Stencil Reference", Float) = 1.0
        [HideInInspector][Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Float) = 8
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Stencil { Ref [_StencilRef] Comp [_StencilComp] Pass Keep }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
        CBUFFER_START(UnityPerMaterial)
            half4 _OutlineColor;
            half  _OutlineWidthPixels;
            float4 _RtTexelSize;   // (1/w, 1/h, w, h)
        CBUFFER_END

        struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
        struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

        Varyings CompositeVert(Attributes v)
        {
            Varyings o = (Varyings)0;
            o.positionCS = TransformObjectToHClip(v.positionOS);
            o.uv = v.uv;
            return o;
        }

        half4 CompositeFrag(Varyings i) : SV_Target
        {
            half4 rt = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);  // premultiplied 캐릭터
            half a = rt.a;

            // 외곽 반경에서 16방향 이웃 알파 최대 - 비었지만 이웃이 채워졌으면 외곽선 밴드.
            float2 r = _RtTexelSize.xy * _OutlineWidthPixels;
            half maxN = 0.0h;
            [unroll] for (int k = 0; k < 16; k++)
            {
                float ang = 6.2831853h * k / 16.0h;
                float2 d = float2(cos(ang), sin(ang));
                maxN = max(maxN, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + d * r).a);
            }

            half inside = step(0.04h, a);                 // 캐릭터 내부
            half outline = (1.0h - inside) * step(0.04h, maxN);  // 밖 + 이웃 채움 = 외곽선

            half3 outlinePremult = _OutlineColor.rgb * _OutlineColor.a;
            half3 rgb = rt.rgb + outlinePremult * outline;          // 캐릭터 + 외곽선
            half outA = max(a, _OutlineColor.a * outline);
            return half4(rgb, outA);
        }
        ENDHLSL

        Pass { Name "Universal2D" Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex CompositeVert
            #pragma fragment CompositeFrag
            ENDHLSL
        }
        Pass { Name "Forward" Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex CompositeVert
            #pragma fragment CompositeFrag
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}
