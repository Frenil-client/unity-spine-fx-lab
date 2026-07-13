using UnityEngine;

namespace SpineFxLab.Effects
{
    /// <summary>
    /// RT 외곽선. 스켈레톤을 RT 한 장으로 합친(SkeletonRtEffect) 뒤 그 전체 실루엣 바깥에만 외곽선을 그린다.
    /// per-part 알파 아웃라인과 달리 부위 경계(seam)에 선이 끼지 않는다. On/Off 토글.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkeletonOutlineRt : SkeletonRtEffect
    {
        [SerializeField] Color _outlineColor = Color.white;
        [SerializeField, Range(1, 16)] int _outlineWidthPixels = 6;

        static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidthPixels");
        static readonly int RtTexelId = Shader.PropertyToID("_RtTexelSize");

        protected override string QuadShaderName => "SpineFxLab/SpineOutlineRtComposite";
        protected override int ScreenPaddingPixels => _outlineWidthPixels + 2;   // 외곽선 여백

        public bool IsOn => IsActive;
        public void SetOutline(bool on) => SetEffectActive(on);
        public void SetColor(Color c) => _outlineColor = c;
        public void SetWidth(int px) => _outlineWidthPixels = Mathf.Clamp(px, 1, 16);

        protected override void OnBeforeAssign()
        {
            var m = QuadMaterial;
            if (m == null) return;
            m.SetColor(OutlineColorId, _outlineColor);
            m.SetFloat(OutlineWidthId, _outlineWidthPixels);
            var rt = CurrentRT;
            if (rt != null)
                m.SetVector(RtTexelId, new Vector4(1f / rt.width, 1f / rt.height, rt.width, rt.height));
        }
    }
}
