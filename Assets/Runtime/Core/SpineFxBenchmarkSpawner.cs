using System.Collections.Generic;
using UnityEngine;
using Spine.Unity;

namespace SpineFxLab.Core
{
    /// <summary>
    /// 벤치마크용 런타임 스폰너. SkeletonDataAsset 로 N개 인스턴스를 그리드 배치하고 애니메이션 재생.
    /// 런타임에 카운트 전환(버튼/자동 스텝) + 레버 토글:
    ///  - Manager: 오프스크린 컬링
    ///  - Pooling: 인스턴스 재사용으로 카운트 전환 시 Instantiate/Destroy 스파이크 제거
    ///  - MPB Tint: 인스턴스별 색을 MaterialPropertyBlock 으로 - 단일 머티리얼 공유 유지
    /// </summary>
    public sealed class SpineFxBenchmarkSpawner : MonoBehaviour
    {
        [SerializeField] SkeletonDataAsset _skeletonData;
        [Tooltip("버튼/자동 스텝으로 순회할 인스턴스 수 목록. 큰 수는 그리드가 화면을 넘쳐 컬링 효과가 드러남.")]
        [SerializeField] int[] _countSequence = { 10, 30, 100, 300 };
        [SerializeField] int _columns = 10;
        [SerializeField] Vector2 _spacing = new Vector2(2.2f, 3.4f);
        [SerializeField] string _animation = "idle";

        [Header("Auto Step (recording)")]
        [Tooltip("켜면 배열을 stepHold 초마다 자동 전환 - 한 번의 녹화로 진행.")]
        [SerializeField] bool _autoStep = false;
        [SerializeField] float _stepHold = 3f;
        [SerializeField] bool _loopSequence = true;
        [Tooltip("자동 스텝이 끝나면(loop off) Play 모드 자동 종료 - GIF 캡처용.")]
        [SerializeField] bool _exitPlayWhenDone = true;

        [Header("Performance Levers")]
        [Tooltip("오프스크린 컬링 = managed. 끄면 naive.")]
        [SerializeField] bool _useManager = false;
        [Tooltip("풀링 - 카운트 전환 시 재사용으로 Instantiate/Destroy 스파이크 제거.")]
        [SerializeField] bool _usePooling = true;
        [Tooltip("인스턴스별 색을 MPB 로 주입 - 단일 머티리얼 공유 유지.")]
        [SerializeField] bool _mpbTint = false;

        [SerializeField] bool _showStats = true;

        readonly List<GameObject> _pool = new List<GameObject>();   // 생성된 전체(활성+비활성)
        int _active;
        SpineFxManager _manager;
        MaterialPropertyBlock _mpb;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        int _seqIndex;
        float _stepTimer;
        float _fpsSmoothed;

        public int Count => _active;

        void Start()
        {
            if (_countSequence != null && _countSequence.Length > 0)
                SpawnIndex(0);
        }

        void OnDestroy() => DestroyPool();

        public void SpawnIndex(int idx)
        {
            if (_countSequence == null || _countSequence.Length == 0) return;
            _seqIndex = Mathf.Clamp(idx, 0, _countSequence.Length - 1);
            _stepTimer = 0f;
            Spawn(_countSequence[_seqIndex]);
        }

        /// <summary>count 개를 활성화. 풀링이면 재사용, 아니면 매번 파괴 후 재생성.</summary>
        public void Spawn(int count)
        {
            if (_skeletonData == null)
            {
                Debug.LogWarning("[SpineFxBenchmark] SkeletonDataAsset 가 비어 있습니다.");
                return;
            }

            if (!_usePooling) DestroyPool();   // 풀링 OFF: 전환 스파이크(파괴->재생성)

            EnsurePool(count);
            for (int i = 0; i < _pool.Count; i++)
            {
                var go = _pool[i];
                if (go == null) continue;
                bool active = i < count;
                if (go.activeSelf != active) go.SetActive(active);
            }
            _active = count;

            ApplyTint();
            ApplyManaged();
        }

        void EnsurePool(int count)
        {
            int cols = Mathf.Max(1, _columns);
            while (_pool.Count < count)
            {
                int i = _pool.Count;
                var comps = SkeletonAnimation.NewSkeletonAnimationGameObject(_skeletonData);
                var sa = comps.skeletonAnimation;
                var go = sa.gameObject;
                go.name = $"Bench_{i:D3}";
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(
                    (i % cols - (cols - 1) * 0.5f) * _spacing.x,
                    -(i / cols) * _spacing.y,
                    0f);

                // 지정 애니가 없으면 첫 애니로 폴백. loop=true 를 직접 넘기는 AnimationState API.
                var data = sa.Skeleton != null ? sa.Skeleton.Data : null;
                if (data != null && sa.AnimationState != null)
                {
                    string anim = _animation;
                    if (string.IsNullOrEmpty(anim) || data.FindAnimation(anim) == null)
                        anim = data.Animations.Count > 0 ? data.Animations.Items[0].Name : null;
                    if (!string.IsNullOrEmpty(anim))
                    {
                        sa.loop = true;
                        sa.AnimationState.SetAnimation(0, anim, true);
                    }
                }

                // 고유 sortingOrder - 같은 z/order 동률로 인한 매 프레임 뒤집힘 방지.
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null) mr.sortingOrder = i;

                _pool.Add(go);
            }
        }

        void DestroyPool()
        {
            for (int i = 0; i < _pool.Count; i++)
                if (_pool[i] != null) Destroy(_pool[i]);
            _pool.Clear();
            _active = 0;
        }

        public void Clear() => DestroyPool();

        // 인스턴스별 색을 MPB 로 준다. Spine 이 sharedMaterials 를 매 프레임 재할당해도 MPB 는 건드리지
        // 않으므로, 머티리얼을 늘리지 않고(단일 머티리얼 공유) per-instance 색을 줄 수 있다.
        // (머티리얼 인스턴스를 꽂으면 Spine 재할당에 덮여 되돌아간다.)
        void ApplyTint()
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            for (int i = 0; i < _active; i++)
            {
                var mr = _pool[i] != null ? _pool[i].GetComponent<MeshRenderer>() : null;
                if (mr == null) continue;
                if (_mpbTint)
                {
                    mr.GetPropertyBlock(_mpb);
                    _mpb.SetColor(ColorId, Color.HSVToRGB((i * 0.13f) % 1f, 0.45f, 1f));
                    mr.SetPropertyBlock(_mpb);
                }
                else
                {
                    mr.SetPropertyBlock(null);
                }
            }
        }

        void EnsureManager()
        {
            if (_manager == null) _manager = GetComponent<SpineFxManager>();
            if (_manager == null) _manager = gameObject.AddComponent<SpineFxManager>();
        }

        void ApplyManaged()
        {
            EnsureManager();
            if (_useManager)
            {
                _manager.Clear();
                for (int i = 0; i < _active; i++)
                {
                    var sa = _pool[i] != null ? _pool[i].GetComponent<SkeletonAnimation>() : null;
                    if (sa != null) _manager.Register(sa);
                }
                _manager.enabled = true;
            }
            else
            {
                _manager.enabled = false;   // OnDisable -> EnableAll (naive 복귀)
            }
        }

        public void SetManaged(bool on)
        {
            _useManager = on;
            ApplyManaged();
        }

        public void SetMpbTint(bool on)
        {
            _mpbTint = on;
            ApplyTint();
        }

        static void ExitPlayMode()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Update()
        {
            float fps = 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-5f);
            _fpsSmoothed = Mathf.Lerp(_fpsSmoothed, fps, 0.1f);

            if (_autoStep && _countSequence != null && _countSequence.Length > 1)
            {
                // 첫 프레임/스폰 hitch 의 큰 deltaTime 이 스텝을 통째로 건너뛰지 않도록 클램프.
                _stepTimer += Mathf.Min(Time.unscaledDeltaTime, 0.2f);
                if (_stepTimer >= _stepHold)
                {
                    int next = _seqIndex + 1;
                    if (next >= _countSequence.Length)
                    {
                        if (!_loopSequence)
                        {
                            _autoStep = false;
                            if (_exitPlayWhenDone) ExitPlayMode();
                            return;
                        }
                        next = 0;
                    }
                    SpawnIndex(next);
                }
            }
        }

        void OnGUI()
        {
            if (!_showStats) return;

            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(8, 6, 520, 150), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(12, 10, 360, 22), $"Spine instances: {_active}  (pool {_pool.Count})");
            GUI.Label(new Rect(12, 30, 360, 22), $"FPS: {_fpsSmoothed:0.0}   ({Time.unscaledDeltaTime * 1000f:0.0} ms/frame)");

            if (_countSequence == null) return;

            // 카운트 전환 버튼
            float x = 12f;
            for (int i = 0; i < _countSequence.Length; i++)
            {
                GUI.color = i == _seqIndex ? Color.cyan : Color.white;
                if (GUI.Button(new Rect(x, 58, 56, 26), _countSequence[i].ToString()))
                    SpawnIndex(i);
                x += 60f;
            }
            GUI.color = _autoStep ? Color.cyan : Color.white;
            if (GUI.Button(new Rect(x + 8f, 58, 90, 26), _autoStep ? "Auto: ON" : "Auto: OFF"))
            {
                _autoStep = !_autoStep;
                _stepTimer = 0f;
            }
            GUI.color = Color.white;

            // Manager 토글 + 갱신 인스턴스 수(컬링 증거)
            GUI.color = _useManager ? Color.cyan : Color.white;
            if (GUI.Button(new Rect(12, 90, 170, 26), _useManager ? "Manager: ON (after)" : "Manager: OFF (naive)"))
                SetManaged(!_useManager);
            GUI.color = Color.white;
            if (_useManager && _manager != null)
                GUI.Label(new Rect(190, 92, 320, 22), $"Updating: {_manager.ActiveThisFrame} / {_manager.ManagedCount}  (culled)");

            // Pooling / MPB Tint 토글
            GUI.color = _usePooling ? Color.cyan : Color.white;
            if (GUI.Button(new Rect(12, 122, 150, 26), _usePooling ? "Pooling: ON" : "Pooling: OFF"))
                _usePooling = !_usePooling;
            GUI.color = _mpbTint ? Color.cyan : Color.white;
            if (GUI.Button(new Rect(170, 122, 150, 26), _mpbTint ? "MPB Tint: ON" : "MPB Tint: OFF"))
            {
                _mpbTint = !_mpbTint;
                ApplyTint();
            }
            GUI.color = Color.white;
        }
    }
}
