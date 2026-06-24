// SpineFx - Spine 2D 통합 FX 셰이더 (디졸브 / 플래시 / 틴트 / 상태이상4 / 아웃라인 / 실루엣).
// 스켈레톤 스프라이트에 거는 프래그먼트 오버레이 연출을 한 셰이더로 통합 - 한 머티리얼로 모든 연출을
// 동시에 걸 수 있다(예: 빙결 + 아웃라인). 베이스 샘플링/vert/AlphaEdge 는 SpineFx_Common.hlsl 공유.
// "연출 종류"는 shader_feature_local 키워드(안 쓰는 조합은 빌드 스트립), "수치"는 MaterialPropertyBlock.
// 렌더러 호환: Universal2D + Forward.
//
Shader "SpineFxLab/SpineFx"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Main Texture", 2D) = "black" {}
        [Toggle(_STRAIGHT_ALPHA_INPUT)] _StraightAlphaInput ("Straight Alpha Texture", Int) = 0
        _Color ("Light Color (a = Alpha)", Color) = (1,1,1,1)
        [NoScaleOffset] _DissolveNoise ("Noise (R) - dissolve/electric", 2D) = "gray" {}

        [Header(Hit Flash)][Space]
        [Toggle(_FLASH_ON)] _FlashEnabled ("Enable Flash", Float) = 0
        [HDR] _FlashColor ("Flash Color (a = Max)", Color) = (1,1,1,1)
        [Range(0,1)] _FlashAmount ("Flash Amount", Float) = 0

        [Header(Tint and Grayscale)][Space]
        [Toggle(_TINT_ON)] _TintEnabled ("Enable Tint", Float) = 0
        _TintColor ("Tint Color", Color) = (1,1,1,1)
        [Range(0,1)] _TintAmount ("Tint Amount", Float) = 0
        [Range(0,1)] _Grayscale ("Grayscale", Float) = 0

        [Header(Dissolve)][Space]
        [Toggle(_DISSOLVE_ON)] _DissolveEnabled ("Enable Dissolve", Float) = 0
        [Range(0,1)] _DissolveAmount ("Dissolve Amount", Float) = 0
        [Range(0,0.3)] _DissolveEdgeWidth ("Dissolve Edge Width", Float) = 0.05
        [HDR] _DissolveEdgeColor ("Dissolve Edge Color (a = Intensity)", Color) = (1,0.6,0.1,1)

        [Header(Status Electric)][Space]
        [Toggle(_STATUS_ELECTRIC)] _ElectricEnabled ("Enable Electric", Float) = 0
        [HDR] _ElectricColor ("Electric Color", Color) = (0.6,0.9,1.4,1)
        [Range(0,1)] _ElectricAmount ("Electric Amount", Float) = 0

        [Header(Status Freeze)][Space]
        [Toggle(_STATUS_FREEZE)] _FreezeEnabled ("Enable Freeze", Float) = 0
        [HDR] _FreezeColor ("Freeze Color", Color) = (0.6,0.85,1.1,1)
        [Range(0,1)] _FreezeAmount ("Freeze Amount", Float) = 0

        [Header(Status Poison)][Space]
        [Toggle(_STATUS_POISON)] _PoisonEnabled ("Enable Poison", Float) = 0
        [HDR] _PoisonColor ("Poison Color", Color) = (0.5,1.0,0.2,1)
        [Range(0,1)] _PoisonAmount ("Poison Amount", Float) = 0

        [Header(Status Shield)][Space]
        [Toggle(_STATUS_SHIELD)] _ShieldEnabled ("Enable Shield", Float) = 0
        [HDR] _ShieldColor ("Shield Color", Color) = (1.0,0.9,0.4,1)
        [Range(0,1)] _ShieldAmount ("Shield Amount", Float) = 0

        [Header(Outline)][Space]
        [Toggle(_OUTLINE_ON)] _OutlineEnabled ("Enable Outline", Float) = 0
        [HDR] _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        [Range(0,8)] _OutlineWidth ("Outline Width (texels)", Float) = 2
        [Range(0,1)] _OutlineThreshold ("Outline Threshold", Float) = 0.5

        [Header(Silhouette)][Space]
        [Toggle(_SILHOUETTE_ON)] _SilhouetteEnabled ("Enable Silhouette", Float) = 0
        [HDR] _SilhouetteColor ("Silhouette Color", Color) = (0,0,0,1)

        [HideInInspector] _StencilRef ("Stencil Reference", Float) = 1.0
        [HideInInspector][Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Float) = 8
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 100
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Stencil { Ref [_StencilRef] Comp [_StencilComp] Pass Keep }

        HLSLINCLUDE
        #include "Includes/SpineFx_Common.hlsl"

        TEXTURE2D(_DissolveNoise);  SAMPLER(sampler_DissolveNoise);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_TexelSize;
            half4  _Color;
            half4  _FlashColor;        half _FlashAmount;
            half4  _TintColor;         half _TintAmount;   half _Grayscale;
            half   _DissolveAmount;    half _DissolveEdgeWidth;  half4 _DissolveEdgeColor;
            half4  _ElectricColor;     half _ElectricAmount;
            half4  _FreezeColor;       half _FreezeAmount;
            half4  _PoisonColor;       half _PoisonAmount;
            half4  _ShieldColor;       half _ShieldAmount;
            half4  _OutlineColor;      half _OutlineWidth;   half _OutlineThreshold;
            half4  _SilhouetteColor;
        CBUFFER_END

        half3 ApplyTint(half3 rgb)
        {
            half luma = dot(rgb, half3(0.299, 0.587, 0.114));
            rgb = lerp(rgb, luma.xxx, saturate(_Grayscale));
            rgb = lerp(rgb, rgb * _TintColor.rgb, saturate(_TintAmount));
            return rgb;
        }

        half OutlineFactor(float2 uv, half centerA)
        {
            float2 o = _MainTex_TexelSize.xy * max(_OutlineWidth, 0.0001h);
            half m = 1.0h;
            m = min(m, SampleAlpha(uv + float2(o.x, 0)));
            m = min(m, SampleAlpha(uv - float2(o.x, 0)));
            m = min(m, SampleAlpha(uv + float2(0, o.y)));
            m = min(m, SampleAlpha(uv - float2(0, o.y)));
            m = min(m, SampleAlpha(uv + o));
            m = min(m, SampleAlpha(uv - o));
            m = min(m, SampleAlpha(uv + float2(o.x, -o.y)));
            m = min(m, SampleAlpha(uv + float2(-o.x, o.y)));
            return step(_OutlineThreshold, centerA) * (1.0h - step(_OutlineThreshold, m));
        }

        half4 SpineFxFrag(Varyings i) : SV_Target
        {
            half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
            half4 c = PremultiplyBase(tex, i.color);
            c.rgb *= _Color.rgb;
            c     *= _Color.a;

            half time = _Time.y;

        #if defined(_FLASH_ON)
            c.rgb = lerp(c.rgb, _FlashColor.rgb * c.a, saturate(_FlashAmount) * _FlashColor.a);
        #endif
        #if defined(_TINT_ON)
            c.rgb = ApplyTint(c.rgb);
        #endif
        #if defined(_STATUS_ELECTRIC)
            half flicker = 0.5h + 0.5h * sin(time * 30.0h);
            half spark = smoothstep(0.6h, 1.0h, SAMPLE_TEXTURE2D(_DissolveNoise, sampler_DissolveNoise, i.uv + float2(0, time)).r);
            half e = saturate(_ElectricAmount) * (0.4h + 0.6h * flicker);
            c.rgb = lerp(c.rgb, _ElectricColor.rgb * c.a, e * 0.5h);
            c.rgb += _ElectricColor.rgb * (spark * e * c.a);
        #endif
        #if defined(_STATUS_FREEZE)
            half edgeF = AlphaEdge(i.uv, _MainTex_TexelSize.xy, 1.5h);
            half f = saturate(_FreezeAmount);
            c.rgb = lerp(c.rgb, _FreezeColor.rgb * c.a, f * 0.55h);
            c.rgb += _FreezeColor.rgb * (edgeF * f * c.a);
        #endif
        #if defined(_STATUS_POISON)
            half pulse = 0.5h + 0.5h * sin(time * 3.0h);
            c.rgb = lerp(c.rgb, _PoisonColor.rgb * c.a, saturate(_PoisonAmount) * pulse);
        #endif
        #if defined(_STATUS_SHIELD)
            half edgeS = AlphaEdge(i.uv, _MainTex_TexelSize.xy, 1.5h);
            half s = saturate(_ShieldAmount) * (0.6h + 0.4h * sin(time * 4.0h));
            c.rgb += _ShieldColor.rgb * (edgeS * s * c.a);
        #endif
        #if defined(_SILHOUETTE_ON)
            c.rgb = _SilhouetteColor.rgb * c.a;          // 전체 단색(색 연출 위에 덮어씀)
        #endif
        #if defined(_DISSOLVE_ON)
            half coverage = c.a;
            half cutoff = SAMPLE_TEXTURE2D(_DissolveNoise, sampler_DissolveNoise, i.uv).r - _DissolveAmount;
            half body   = step(0.0, cutoff);
            half dedge  = (1.0 - saturate(cutoff / max(_DissolveEdgeWidth, 1e-4))) * step(1e-3, _DissolveAmount);
            c    *= body;
            c.rgb += _DissolveEdgeColor.rgb * (_DissolveEdgeColor.a * dedge * body * coverage);
        #endif
        #if defined(_OUTLINE_ON)
            half ol = OutlineFactor(i.uv, tex.a);
            c.rgb = lerp(c.rgb, _OutlineColor.rgb * c.a, ol * _OutlineColor.a);
        #endif

            return c;
        }
        ENDHLSL

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex SpineVert
            #pragma fragment SpineFxFrag
            #pragma shader_feature_local _ _STRAIGHT_ALPHA_INPUT
            #pragma shader_feature_local _FLASH_ON
            #pragma shader_feature_local _TINT_ON
            #pragma shader_feature_local _DISSOLVE_ON
            #pragma shader_feature_local _STATUS_ELECTRIC
            #pragma shader_feature_local _STATUS_FREEZE
            #pragma shader_feature_local _STATUS_POISON
            #pragma shader_feature_local _STATUS_SHIELD
            #pragma shader_feature_local _OUTLINE_ON
            #pragma shader_feature_local _SILHOUETTE_ON
            ENDHLSL
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex SpineVert
            #pragma fragment SpineFxFrag
            #pragma shader_feature_local _ _STRAIGHT_ALPHA_INPUT
            #pragma shader_feature_local _FLASH_ON
            #pragma shader_feature_local _TINT_ON
            #pragma shader_feature_local _DISSOLVE_ON
            #pragma shader_feature_local _STATUS_ELECTRIC
            #pragma shader_feature_local _STATUS_FREEZE
            #pragma shader_feature_local _STATUS_POISON
            #pragma shader_feature_local _STATUS_SHIELD
            #pragma shader_feature_local _OUTLINE_ON
            #pragma shader_feature_local _SILHOUETTE_ON
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}
