using UnityEngine;

namespace SpineFxLab.Data
{
    /// <summary>상태이상 종류 - SpineFx 셰이더의 _STATUS_* 키워드/프로퍼티에 1:1 대응.</summary>
    public enum StatusType { Electric = 0, Freeze = 1, Poison = 2, Shield = 3 }

    /// <summary>
    /// 상태이상 1종의 데이터 정의 - 종류, 색, 강도, 지속/페이드. StatusEffectPlayer 가 소비해
    /// MPB(_ElectricAmount/_ElectricColor 등)로 주입한다. 셰이더 키워드는 종류, 수치는 MPB.
    /// </summary>
    [CreateAssetMenu(menuName = "SpineFxLab/Status Effect Def", fileName = "StatusEffectDef", order = 1)]
    public sealed class StatusEffectDef : FxDefinition
    {
        public StatusType type = StatusType.Electric;
        [ColorUsage(true, true)] public Color color = new Color(0.6f, 0.9f, 1.4f, 1f);
        [Range(0f, 1f)] public float intensity = 1f;
        [Tooltip("0이면 수동 해제(SetStatus false)까지 지속.")]
        public float duration = 0f;
        [Tooltip("켜질 때 페이드인 / 꺼질 때 페이드아웃 시간(초).")]
        public float fade = 0.25f;
    }
}
