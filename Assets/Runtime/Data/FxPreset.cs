using UnityEngine;

namespace SpineFxLab.Data
{
    /// <summary>
    /// 디졸브/플래시/틴트/알파 연출 수치를 담는 데이터 주도 프리셋.
    /// 셰이더 키워드(연출 종류)는 공유 머티리얼에 고정하고, 이 프리셋은 MPB로 주입되는
    /// "수치(강도/색/임계값)"만 정의한다. SpineFxController.Play(FxPreset)가 소비한다.
    /// </summary>
    [CreateAssetMenu(menuName = "SpineFxLab/Fx Preset", fileName = "FxPreset", order = 0)]
    public sealed class FxPreset : FxDefinition
    {
        [Header("Global")]
        public Color color = Color.white;          // rgb = light color
        [Range(0f, 1f)] public float alpha = 1f;   // _Color.a (글로벌 페이드)

        [Header("Hit Flash (_FLASH_ON)")]
        [ColorUsage(true, true)] public Color flashColor = Color.white;
        [Range(0f, 1f)] public float flashAmount = 0f;

        [Header("Tint / Grayscale (_TINT_ON)")]
        public Color tintColor = Color.white;
        [Range(0f, 1f)] public float tintAmount = 0f;
        [Range(0f, 1f)] public float grayscale = 0f;

        [Header("Dissolve (_DISSOLVE_ON)")]
        [Range(0f, 1f)] public float dissolveAmount = 0f;
        [Range(0f, 0.3f)] public float dissolveEdgeWidth = 0.05f;
        [ColorUsage(true, true)] public Color dissolveEdgeColor = new Color(1f, 0.6f, 0.1f, 1f);
    }
}
