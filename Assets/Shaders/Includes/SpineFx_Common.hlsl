#ifndef SPINEFX_COMMON_INCLUDED
#define SPINEFX_COMMON_INCLUDED

// ----------------------------------------------------------------------------
// SpineFx.shader 가 쓰는 공용 베이스. 텍스처/정점 입출력과 vert, PMA 베이스 샘플링,
// 알파 샘플/알파 경계 헬퍼를 모아 둔다. 효과별 로직(디졸브/상태이상/아웃라인)과 CBUFFER 는
// SpineFx.shader 에 둔다.
//
// PMA / Linear: 소스는 straight alpha 이고 셰이더에서 premultiply 한다. 결과가 premultiplied
// 라서 블렌드는 One/OneMinusSrcAlpha 이며, 각 효과도 알파를 함께 곱해 premultiplied 를 유지한다.
// ----------------------------------------------------------------------------

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);

struct Attributes
{
    float3 positionOS : POSITION;
    half4  color      : COLOR;
    float2 uv         : TEXCOORD0;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    half4  color      : COLOR;
    float2 uv         : TEXCOORD0;
};

Varyings SpineVert(Attributes v)
{
    Varyings o = (Varyings)0;
    o.positionCS = TransformObjectToHClip(v.positionOS);
    o.uv    = v.uv;          // Spine 메시는 아틀라스 UV (스케일/오프셋 없음)
    o.color = v.color;
    return o;
}

half SampleAlpha(float2 uv) { return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a; }

// 이미 샘플한 tex 로 premultiply + vertex color. (아웃라인이 raw tex.a 를 쓰므로 한 번만 샘플.)
half4 PremultiplyBase(half4 tex, half4 vertexColor)
{
    half4 c;
#if defined(_STRAIGHT_ALPHA_INPUT)
    c.rgb = tex.rgb * vertexColor.rgb * tex.a;
#else
    c.rgb = tex.rgb * vertexColor.rgb;
#endif
    c.a = tex.a * vertexColor.a;
    return c;
}

// 알파 경계(2D 림/실루엣 가장자리). texel = _MainTex_TexelSize.xy.
half AlphaEdge(float2 uv, float2 texel, half scale)
{
    float2 t = texel * scale;
    half ax = abs(SampleAlpha(uv + float2(t.x, 0)) - SampleAlpha(uv - float2(t.x, 0)));
    half ay = abs(SampleAlpha(uv + float2(0, t.y)) - SampleAlpha(uv - float2(0, t.y)));
    return saturate((ax + ay) * 1.5);
}

#endif // SPINEFX_COMMON_INCLUDED
