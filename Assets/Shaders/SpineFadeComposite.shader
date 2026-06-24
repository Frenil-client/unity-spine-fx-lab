// SpineFadeComposite - FadeGroup이 RT(불투명 통합 렌더)를 단일 알파로 합성하는 쿼드 셰이더.
// 스켈레톤을 오프스크린 RT에 한 덩어리로 그린 결과를 _Color.a 하나로 페이드 -> 부위 겹침/비침 제거.
// URP 2D(Universal2D) + Forward 두 패스.
Shader "SpineFxLab/SpineFadeComposite"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Render Texture", 2D) = "white" {}
        _Color ("Color (a = Fade)", Color) = (1,1,1,1)

        [HideInInspector] _StencilRef ("Stencil Reference", Float) = 1.0
        [HideInInspector][Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Float) = 8
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Stencil { Ref [_StencilRef] Comp [_StencilComp] Pass Keep }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
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
            half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);  // premultiplied RT
            half4 col = _Color;
            col.rgb *= col.a;                 // 단일 알파를 premultiplied 로
            return tex * col;
        }
        ENDHLSL

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex CompositeVert
            #pragma fragment CompositeFrag
            ENDHLSL
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex CompositeVert
            #pragma fragment CompositeFrag
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}
