using UnityEngine;

namespace SpineFxLab.Data
{
    /// <summary>
    /// 연출 정의 SO 공통 베이스. FxPreset(다채널 스냅샷)과 StatusEffectDef(스택형 상태)가 공유하는
    /// 공통 타입과 오서링 메모를 둔다. 동작 모델(스냅샷 교체 vs 스택/수명)과 수치는 서로 달라
    /// 각 타입이 자기 필드로 가진다. 색의 HDR 여부/강도 표현이 달라 수치는 베이스로 끌어올리지 않는다.
    /// </summary>
    public abstract class FxDefinition : ScriptableObject
    {
        [Tooltip("기획/아트용 메모(선택).")]
        [TextArea(1, 2)] public string note;
    }
}
