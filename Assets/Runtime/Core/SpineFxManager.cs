using System.Collections.Generic;
using UnityEngine;
using Spine.Unity;

namespace SpineFxLab.Core
{
    /// <summary>
    /// 다중 인스턴스 갱신 총괄. 등록된 스켈레톤의 SkeletonRenderer.UpdateMode 를 가시성/거리로 토글한다.
    /// 화면 밖이면 UpdateMode.Nothing 으로 두어 애니메이션/월드 트랜스폼/physics/MeshGenerator 를 모두 멈춘다
    /// (렌더는 어차피 프러스텀 컬링됨). naive(전부 FullUpdate) 대비 절약의 대부분이 여기서 나온다.
    /// 주의: SkeletonAnimation.enabled=false 로는 부족하다. 메시 생성은 SkeletonRenderer 쪽에서 UpdateMode 로
    /// 게이팅되므로, mesh deform 비용을 빼려면 UpdateMode 자체를 바꿔야 한다.
    /// </summary>
    public sealed class SpineFxManager : MonoBehaviour
    {
        [SerializeField] Camera _camera;
        [Tooltip("화면 밖 인스턴스의 갱신을 스킵 (가장 큰 절약).")]
        [SerializeField] bool _cullOffscreen = true;
        [Tooltip("원거리 인스턴스는 N프레임마다만 갱신 (perspective 에서 유효; 2D 는 거리 동일이라 거의 무효).")]
        [SerializeField] bool _throttleByDistance = false;
        [SerializeField] float _throttleDistance = 25f;
        [SerializeField, Min(1)] int _throttleInterval = 3;

        readonly List<SkeletonRenderer> _skeletons = new List<SkeletonRenderer>();
        readonly List<Renderer> _renderers = new List<Renderer>();
        readonly Plane[] _planes = new Plane[6];
        int _activeThisFrame;

        public int ManagedCount => _skeletons.Count;
        public int ActiveThisFrame => _activeThisFrame;

        void Awake()
        {
            if (_camera == null) _camera = Camera.main;
        }

        public void Register(SkeletonAnimation anim)
        {
            if (anim == null) return;
            var sr = anim.GetComponent<SkeletonRenderer>();
            if (sr == null) return;
            if (_skeletons.Contains(sr)) return;   // 중복 등록 시 리스트가 부풀어 모드 토글이 2회 수행됨
            // Spine 내장 컬링(Unity 가시성 기반)도 맞춰둠 - 매니저 LateUpdate 와 같은 방향으로 동작.
            sr.updateWhenInvisible = UpdateMode.Nothing;
            _skeletons.Add(sr);
            _renderers.Add(anim.GetComponent<Renderer>());
        }

        public void Clear()
        {
            _skeletons.Clear();
            _renderers.Clear();
            _activeThisFrame = 0;
        }

        /// <summary>전부 FullUpdate 로 복귀(매니저 비활성 = naive). 내장 컬링도 끔.</summary>
        public void EnableAll()
        {
            for (int i = 0; i < _skeletons.Count; i++)
            {
                var sr = _skeletons[i];
                if (sr == null) continue;
                sr.updateWhenInvisible = UpdateMode.FullUpdate;
                sr.UpdateMode = UpdateMode.FullUpdate;
            }
        }

        void OnDisable() => EnableAll();   // 매니저 꺼지면 naive 복귀

        void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            GeometryUtility.CalculateFrustumPlanes(_camera, _planes);
            Vector3 camPos = _camera.transform.position;
            int frame = Time.frameCount;
            _activeThisFrame = 0;

            for (int i = 0; i < _skeletons.Count; i++)
            {
                var sr = _skeletons[i];
                if (sr == null) continue;
                var r = _renderers[i];

                bool full = !_cullOffscreen || (r != null && GeometryUtility.TestPlanesAABB(_planes, r.bounds));

                if (full && _throttleByDistance && r != null)
                {
                    float d = Vector3.Distance(camPos, r.bounds.center);
                    if (d > _throttleDistance)
                        full = ((frame + i) % _throttleInterval) == 0;
                }

                var mode = full ? UpdateMode.FullUpdate : UpdateMode.Nothing;
                if (sr.UpdateMode != mode) sr.UpdateMode = mode;   // UpdateMode 가 메시 생성까지 게이팅
                if (full) _activeThisFrame++;
            }
        }
    }
}
