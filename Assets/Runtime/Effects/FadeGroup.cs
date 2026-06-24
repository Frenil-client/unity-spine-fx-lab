using UnityEngine;

namespace SpineFxLab.Effects
{
    /// <summary>
    /// 통합 투명 페이드(고스팅 해결). 스켈레톤을 RT 에 한 번에 렌더(SkeletonRtEffect)한 뒤 그 결과를
    /// 단일 알파로 합성한다. 부위별 알파를 곱할 때 생기는 겹침/비침이 사라진다.
    /// RT 캡처는 베이스가 맡고, 여기선 페이드 알파만 얹는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FadeGroup : SkeletonRtEffect
    {
        Color _fadeColor = Color.white;   // a = 그룹 알파

        protected override Shader QuadShader => Shader.Find("SpineFxLab/SpineFadeComposite");

        /// <summary>그룹 알파 설정. 1 이상 -> 일반 렌더 복귀(RT 해제), 미만 -> 통합 페이드.</summary>
        public void SetFade(float alpha)
        {
            _fadeColor.a = Mathf.Clamp01(alpha);
            SetEffectActive(_fadeColor.a < 1f);
        }

        protected override void OnBeforeAssign()
        {
            if (QuadMaterial != null) QuadMaterial.color = _fadeColor;   // 단일 알파 합성
        }
    }
}
