using UnityEngine;
using SpineFxLab.Data;
using SpineFxLab.Effects;

namespace SpineFxLab.Core
{
    /// <summary>
    /// 스켈레톤 1체의 이펙트 제어 진입점. MaterialPropertyBlock 한 벌을 들고,
    /// 게임 로직에는 트리거 API(Play / 개별 Set / Clear)만 노출한다.
    /// 셰이더/MPB 디테일은 내부에 숨기고 머티리얼은 증식시키지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpineFxController : MonoBehaviour
    {
        [Tooltip("비우면 Awake 에서 같은 GameObject 의 Renderer(Spine MeshRenderer)를 찾는다.")]
        [SerializeField] Renderer _renderer;
        [Tooltip("통합 투명용. 있으면 Fade(alpha, useGroup:true)가 이걸 통해 합성한다.")]
        [SerializeField] FadeGroup _fadeGroup;
        [Tooltip("상태이상용. 있으면 SetStatus 가 이걸로 위임한다.")]
        [SerializeField] StatusEffectPlayer _statusPlayer;

        readonly MpbEffectBinder _binder = new MpbEffectBinder();
        MaterialPropertyBlock _mpb;
        FxState _state = FxState.Default;

        public FxState State => _state;

        void Reset() => _renderer = GetComponent<Renderer>();

        void Awake() => Apply();   // 초기 상태 반영(지연 초기화 포함)

        // Awake 실행 순서에 의존하지 않도록, 외부 호출 시점에도 안전하게 초기화.
        void EnsureInitialized()
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            if (_renderer == null) _renderer = GetComponent<Renderer>();
            if (_fadeGroup == null) _fadeGroup = GetComponent<FadeGroup>();
            if (_statusPlayer == null) _statusPlayer = GetComponent<StatusEffectPlayer>();
        }

        /// <summary>프리셋의 수치를 한 번에 적용한다(즉시).</summary>
        public void Play(FxPreset preset)
        {
            if (preset == null) return;
            _state = ToState(preset);
            Apply();
        }

        /// <summary>프리셋을 가중치(0=원본, 1=프리셋)로 블렌드 적용. 트윈/페이드인아웃에 사용.</summary>
        public void Play(FxPreset preset, float weight)
        {
            if (preset == null) return;
            _state = FxState.Lerp(FxState.Default, ToState(preset), Mathf.Clamp01(weight));
            Apply();
        }

        // ---- 개별 트리거 (데모/애니메이션에서 수치를 구동) ----

        public void SetFlash(float amount)
        {
            _state.flashAmount = amount;
            Apply();
        }

        public void SetFlash(Color color, float amount)
        {
            _state.flashColor = color;
            _state.flashAmount = amount;
            Apply();
        }

        public void SetTint(Color color, float amount, float grayscale = 0f)
        {
            _state.tintColor = color;
            _state.tintAmount = amount;
            _state.grayscale = grayscale;
            Apply();
        }

        public void SetDissolve(float amount)
        {
            _state.dissolveAmount = amount;
            Apply();
        }

        public void SetAlpha(float alpha)
        {
            var c = _state.color;
            c.a = alpha;
            _state.color = c;
            Apply();
        }

        /// <summary>
        /// 페이드. useGroup=true 면 FadeGroup 통합 투명(부위 비침 없음),
        /// false 면 단순 글로벌 알파(naive - 비교용).
        /// </summary>
        public void Fade(float alpha, bool useGroup)
        {
            EnsureInitialized();
            if (useGroup && _fadeGroup != null)
            {
                SetAlpha(1f);                  // RT 에는 불투명(alpha=1)으로 렌더
                _fadeGroup.SetFade(alpha);     // 합성 단계에서 단일 알파
            }
            else
            {
                if (_fadeGroup != null) _fadeGroup.SetFade(1f);  // 그룹 비활성
                SetAlpha(alpha);               // naive 단순 알파(부위 비침)
            }
        }

        /// <summary>상태이상 적용/해제. StatusEffectPlayer 에 위임(중첩/만료 관리).</summary>
        public void SetStatus(StatusEffectDef def, bool on)
        {
            EnsureInitialized();
            if (_statusPlayer != null) _statusPlayer.SetStatus(def, on);
        }

        /// <summary>모든 FX 를 끄고 원본 상태로 되돌린다.</summary>
        public void Clear()
        {
            _state = FxState.Default;
            Apply();
        }

        /// <summary>현재 상태를 MPB 로 써서 렌더러에 반영한다.</summary>
        void Apply()
        {
            EnsureInitialized();
            if (_renderer == null) return;
            _renderer.GetPropertyBlock(_mpb);   // 기존 블록 값 보존
            _binder.Write(_mpb, _state);
            _renderer.SetPropertyBlock(_mpb);
        }

        static FxState ToState(FxPreset p)
        {
            var color = p.color;
            color.a = p.alpha;
            return new FxState
            {
                color = color,
                flashColor = p.flashColor,
                flashAmount = p.flashAmount,
                tintColor = p.tintColor,
                tintAmount = p.tintAmount,
                grayscale = p.grayscale,
                dissolveAmount = p.dissolveAmount,
                dissolveEdgeWidth = p.dissolveEdgeWidth,
                dissolveEdgeColor = p.dissolveEdgeColor,
            };
        }
    }
}
