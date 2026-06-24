using UnityEngine;

namespace SpineFxLab.Core
{
    /// <summary>
    /// SpineFx 셰이더의 인스턴스별 연출 수치 한 벌. (premultiplied 결과를 만드는 입력값)
    /// </summary>
    public struct FxState
    {
        public Color color;            // rgb = light color, a = global alpha
        public Color flashColor;       // a = max intensity
        public float flashAmount;
        public Color tintColor;
        public float tintAmount;
        public float grayscale;
        public float dissolveAmount;
        public float dissolveEdgeWidth;
        public Color dissolveEdgeColor;

        /// <summary>FX 가 전부 꺼진 기본 상태(원본 그대로).</summary>
        public static FxState Default => new FxState
        {
            color = Color.white,
            flashColor = Color.white,
            flashAmount = 0f,
            tintColor = Color.white,
            tintAmount = 0f,
            grayscale = 0f,
            dissolveAmount = 0f,
            dissolveEdgeWidth = 0.05f,
            dissolveEdgeColor = new Color(1f, 0.6f, 0.1f, 1f),
        };

        /// <summary>두 상태를 t 로 보간(트윈/페이드용).</summary>
        public static FxState Lerp(in FxState a, in FxState b, float t) => new FxState
        {
            color = Color.Lerp(a.color, b.color, t),
            flashColor = Color.Lerp(a.flashColor, b.flashColor, t),
            flashAmount = Mathf.Lerp(a.flashAmount, b.flashAmount, t),
            tintColor = Color.Lerp(a.tintColor, b.tintColor, t),
            tintAmount = Mathf.Lerp(a.tintAmount, b.tintAmount, t),
            grayscale = Mathf.Lerp(a.grayscale, b.grayscale, t),
            dissolveAmount = Mathf.Lerp(a.dissolveAmount, b.dissolveAmount, t),
            dissolveEdgeWidth = Mathf.Lerp(a.dissolveEdgeWidth, b.dissolveEdgeWidth, t),
            dissolveEdgeColor = Color.Lerp(a.dissolveEdgeColor, b.dissolveEdgeColor, t),
        };
    }

    /// <summary>
    /// MaterialPropertyBlock 프로퍼티 set 헬퍼. 셰이더 프로퍼티 ID를 캐싱하고
    /// FxState 를 MPB 에 기록한다. 머티리얼을 증식시키지 않고 인스턴스별 수치를 주입 ->
    /// 단일 머티리얼 공유 + GC 억제. 런타임에 자주 바뀌는 값은 키워드가 아닌 여기로.
    /// </summary>
    public sealed class MpbEffectBinder
    {
        static readonly int ColorId            = Shader.PropertyToID("_Color");
        static readonly int FlashColorId       = Shader.PropertyToID("_FlashColor");
        static readonly int FlashAmountId      = Shader.PropertyToID("_FlashAmount");
        static readonly int TintColorId        = Shader.PropertyToID("_TintColor");
        static readonly int TintAmountId       = Shader.PropertyToID("_TintAmount");
        static readonly int GrayscaleId        = Shader.PropertyToID("_Grayscale");
        static readonly int DissolveAmountId   = Shader.PropertyToID("_DissolveAmount");
        static readonly int DissolveEdgeWidthId = Shader.PropertyToID("_DissolveEdgeWidth");
        static readonly int DissolveEdgeColorId = Shader.PropertyToID("_DissolveEdgeColor");

        /// <summary>현재 블록 값을 보존한 채 FX 프로퍼티만 덮어쓴다.</summary>
        public void Write(MaterialPropertyBlock mpb, in FxState s)
        {
            mpb.SetColor(ColorId, s.color);
            mpb.SetColor(FlashColorId, s.flashColor);
            mpb.SetFloat(FlashAmountId, s.flashAmount);
            mpb.SetColor(TintColorId, s.tintColor);
            mpb.SetFloat(TintAmountId, s.tintAmount);
            mpb.SetFloat(GrayscaleId, s.grayscale);
            mpb.SetFloat(DissolveAmountId, s.dissolveAmount);
            mpb.SetFloat(DissolveEdgeWidthId, s.dissolveEdgeWidth);
            mpb.SetColor(DissolveEdgeColorId, s.dissolveEdgeColor);
        }
    }
}
