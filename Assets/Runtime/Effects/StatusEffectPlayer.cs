using UnityEngine;
using SpineFxLab.Data;

namespace SpineFxLab.Effects
{
    /// <summary>
    /// 상태이상 재생/중첩/만료 관리. 종류별 슬롯(감전/빙결/중독/실드)을 독립적으로 가지며,
    /// 여러 상태이상 동시 적용(중첩)을 지원한다. 강도는 페이드 인/아웃으로 부드럽게,
    /// duration 이 있으면 자동 만료. 머티리얼 증식 없이 MPB 로만 주입(단일 머티리얼 공유).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StatusEffectPlayer : MonoBehaviour
    {
        [SerializeField] Renderer _renderer;

        struct Slot
        {
            public Color color;
            public float target;    // 목표 강도
            public float current;   // 현재 강도(페이드)
            public float fade;      // 페이드 시간
            public bool timed;
            public float timeLeft;
        }

        readonly Slot[] _slots = new Slot[4];
        MaterialPropertyBlock _mpb;

        static readonly int[] ColorIds =
        {
            Shader.PropertyToID("_ElectricColor"),
            Shader.PropertyToID("_FreezeColor"),
            Shader.PropertyToID("_PoisonColor"),
            Shader.PropertyToID("_ShieldColor"),
        };
        static readonly int[] AmountIds =
        {
            Shader.PropertyToID("_ElectricAmount"),
            Shader.PropertyToID("_FreezeAmount"),
            Shader.PropertyToID("_PoisonAmount"),
            Shader.PropertyToID("_ShieldAmount"),
        };

        void Reset() => _renderer = GetComponent<Renderer>();

        void EnsureInit()
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            if (_renderer == null) _renderer = GetComponent<Renderer>();
        }

        /// <summary>상태이상 on/off. 같은 종류는 슬롯 하나를 갱신, 다른 종류는 중첩.</summary>
        public void SetStatus(StatusEffectDef def, bool on)
        {
            if (def == null) return;
            int i = (int)def.type;
            ref Slot s = ref _slots[i];
            s.color = def.color;
            s.fade = Mathf.Max(0f, def.fade);
            if (on)
            {
                s.target = Mathf.Clamp01(def.intensity);
                s.timed = def.duration > 0f;
                s.timeLeft = def.duration;
            }
            else
            {
                s.target = 0f;
                s.timed = false;
            }
            Apply();
        }

        /// <summary>모든 상태이상 즉시 해제.</summary>
        public void ClearAll()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].target = 0f;
                _slots[i].timed = false;
            }
            Apply();
        }

        void Update()
        {
            bool changed = false;
            float dt = Time.deltaTime;
            for (int i = 0; i < _slots.Length; i++)
            {
                ref Slot s = ref _slots[i];
                if (s.timed && s.timeLeft > 0f)
                {
                    s.timeLeft -= dt;
                    if (s.timeLeft <= 0f) { s.target = 0f; s.timed = false; }
                }
                float step = s.fade > 0f ? dt / s.fade : 1f;
                float next = Mathf.MoveTowards(s.current, s.target, step);
                if (!Mathf.Approximately(next, s.current)) { s.current = next; changed = true; }
            }
            if (changed) Apply();
        }

        void Apply()
        {
            EnsureInit();
            if (_renderer == null) return;
            _renderer.GetPropertyBlock(_mpb);
            for (int i = 0; i < _slots.Length; i++)
            {
                _mpb.SetColor(ColorIds[i], _slots[i].color);
                _mpb.SetFloat(AmountIds[i], _slots[i].current);
            }
            _renderer.SetPropertyBlock(_mpb);
        }
    }
}
