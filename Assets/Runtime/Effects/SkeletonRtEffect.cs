using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Spine.Unity;

namespace SpineFxLab.Effects
{
    /// <summary>
    /// RT 캡처 공통 베이스. 스켈레톤 메시를 스크린 bounds 에 맞춰 오프스크린 RT 에 불투명하게 1회 렌더하고,
    /// forceRenderingOff 로 원본을 숨긴 뒤 같은 자리에 쿼드로 합성한다. 2차 카메라 없이 CommandBuffer 로 처리.
    /// 파츠가 RT 안에서 하나로 합쳐지므로, 단일 알파 페이드나 전체 실루엣 외곽선처럼 캐릭터를
    /// 한 덩어리로 다루는 효과의 공통 토대다.
    /// </summary>
    [RequireComponent(typeof(SkeletonRenderer))]
    public abstract class SkeletonRtEffect : MonoBehaviour
    {
        [SerializeField] protected Camera _targetCamera;
        [SerializeField] protected int _maxRenderTextureSize = 1024;
        [Tooltip("RT 에 렌더할 셰이더 패스. URP 는 0.")]
        [SerializeField] protected int[] _shaderPasses = new int[1] { 0 };

        protected SkeletonRenderer _skeletonRenderer;
        protected MeshRenderer _meshRenderer;
        protected MeshFilter _meshFilter;

        GameObject _quad;
        MeshRenderer _quadRenderer;
        MeshFilter _quadFilter;
        Mesh _quadMesh;
        Material _quadMaterial;
        RenderTexture _renderTexture;
        CommandBuffer _commandBuffer;
        MaterialPropertyBlock _propertyBlock;
        readonly List<Material> _materials = new List<Material>();
        bool _active;

        Vector2Int _screenSize, _usedSize, _allocatedSize;
        Vector2 _downScale = Vector2.one;
        Vector3 _wnd0, _wnd1, _wnd2, _wnd3;
        Vector2 _uv0, _uv1, _uv2, _uv3;

        public bool IsActive => _active;
        protected Material QuadMaterial => _quadMaterial;
        protected RenderTexture CurrentRT => _renderTexture;

        // ---- 서브클래스 훅 ----
        protected abstract Shader QuadShader { get; }
        protected virtual int ScreenPaddingPixels => 0;   // 외곽선 등 바깥 여백(px)
        protected virtual void OnBeforeAssign() { }        // 캡처 직전 쿼드 머티리얼 파라미터 갱신

        protected virtual void Awake()
        {
            _skeletonRenderer = GetComponent<SkeletonRenderer>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshFilter = GetComponent<MeshFilter>();
            if (_targetCamera == null) _targetCamera = Camera.main;
            _commandBuffer = new CommandBuffer { name = GetType().Name + " RT" };
            _propertyBlock = new MaterialPropertyBlock();
            CreateQuad();
            _quad.SetActive(false);
        }

        protected virtual void OnDestroy()
        {
            if (_renderTexture) RenderTexture.ReleaseTemporary(_renderTexture);
            _commandBuffer?.Release();
            if (_quadMaterial != null) Destroy(_quadMaterial);
        }

        /// <summary>효과 활성/비활성. 활성 시 RT 캡처+쿼드, 비활성 시 원본 일반 렌더 복귀(RT 해제).</summary>
        protected void SetEffectActive(bool on)
        {
            if (on)
            {
                if (!_active)
                {
                    if (_targetCamera == null) _targetCamera = Camera.main;
                    _skeletonRenderer.OnMeshAndMaterialsUpdated += RenderOntoQuad;
                    _meshRenderer.forceRenderingOff = true;
                    _quad.SetActive(true);
                    _active = true;
                }
                RenderOntoQuad(_skeletonRenderer);
            }
            else if (_active)
            {
                _skeletonRenderer.OnMeshAndMaterialsUpdated -= RenderOntoQuad;
                _meshRenderer.forceRenderingOff = false;
                _quad.SetActive(false);
                if (_renderTexture)
                {
                    RenderTexture.ReleaseTemporary(_renderTexture);
                    _renderTexture = null;
                    _allocatedSize = Vector2Int.zero;
                }
                _active = false;
            }
        }

        void CreateQuad()
        {
            _quad = new GameObject(name + " " + GetType().Name, typeof(MeshRenderer), typeof(MeshFilter));
            _quad.transform.SetParent(transform.parent, false);
            _quad.layer = _meshRenderer.gameObject.layer;
            _quadRenderer = _quad.GetComponent<MeshRenderer>();
            _quadFilter = _quad.GetComponent<MeshFilter>();
            _quadRenderer.sortingOrder = _meshRenderer.sortingOrder;
            _quadRenderer.sortingLayerID = _meshRenderer.sortingLayerID;

            _quadMesh = new Mesh { name = "RtEffect Quad" };
            _quadMesh.MarkDynamic();
            _quadMesh.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;

            _quadMaterial = new Material(QuadShader) { hideFlags = HideFlags.DontSave };
            _quadRenderer.material = _quadMaterial;
        }

        void RenderOntoQuad(ISkeletonRenderer skeletonRenderer)
        {
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
            var mesh = _meshFilter.sharedMesh;
            if (mesh == null) return;
            Vector3 size = mesh.bounds.size;
            if (size.x == 0f || size.y == 0f) { _quadFilter.mesh = null; return; }

            PrepareForMesh(mesh);
            RenderToRenderTexture(mesh);
            OnBeforeAssign();
            AssignAtQuad();
        }

        void PrepareForMesh(Mesh mesh)
        {
            Bounds b = mesh.bounds;
            Vector3 lc0 = b.min, lc3 = b.max;
            Vector3 lc1 = new Vector3(lc0.x, lc3.y, lc0.z);
            Vector3 lc2 = new Vector3(lc3.x, lc0.y, lc3.z);

            Vector3 s0 = _targetCamera.WorldToScreenPoint(transform.TransformPoint(lc0));
            Vector3 s1 = _targetCamera.WorldToScreenPoint(transform.TransformPoint(lc1));
            Vector3 s2 = _targetCamera.WorldToScreenPoint(transform.TransformPoint(lc2));
            Vector3 s3 = _targetCamera.WorldToScreenPoint(transform.TransformPoint(lc3));

            // 외곽선 등 바깥 여백: 스크린 AABB 를 padding(px) 만큼 확장 -> RT/쿼드에 여유.
            float pad = ScreenPaddingPixels;
            if (pad > 0f)
            {
                s0 += new Vector3(-pad, -pad, 0);
                s1 += new Vector3(-pad, +pad, 0);
                s2 += new Vector3(+pad, -pad, 0);
                s3 += new Vector3(+pad, +pad, 0);
            }

            // 원근 왜곡 방지: 평균 깊이 평면에 투영
            float avgZ = (s0.z + s1.z + s2.z + s3.z) * 0.25f;
            s0.z = s1.z = s2.z = s3.z = avgZ;
            _wnd0 = _targetCamera.ScreenToWorldPoint(s0);
            _wnd1 = _targetCamera.ScreenToWorldPoint(s1);
            _wnd2 = _targetCamera.ScreenToWorldPoint(s2);
            _wnd3 = _targetCamera.ScreenToWorldPoint(s3);

            PrepareTextureMapping(out Vector3 ssMin, out Vector3 ssMax, s0, s1, s2, s3);
            PrepareCommandBuffer(ssMin, ssMax);
        }

        void PrepareTextureMapping(out Vector3 ssMin, out Vector3 ssMax,
            Vector3 c0, Vector3 c1, Vector3 c2, Vector3 c3)
        {
            ssMin = Vector3.Min(c0, Vector3.Min(c1, Vector3.Min(c2, c3)));
            ssMax = Vector3.Max(c0, Vector3.Max(c1, Vector3.Max(c2, c3)));
            ssMin.x = Mathf.Floor(ssMin.x); ssMin.y = Mathf.Floor(ssMin.y);
            ssMax.x = Mathf.Ceil(ssMax.x);  ssMax.y = Mathf.Ceil(ssMax.y);

            _uv0 = InverseLerp(ssMin, ssMax, c0);
            _uv1 = InverseLerp(ssMin, ssMax, c1);
            _uv2 = InverseLerp(ssMin, ssMax, c2);
            _uv3 = InverseLerp(ssMin, ssMax, c3);

            _screenSize = new Vector2Int(Mathf.Abs((int)ssMax.x - (int)ssMin.x),
                                         Mathf.Abs((int)ssMax.y - (int)ssMin.y));
            _usedSize = new Vector2Int(Mathf.Min(_maxRenderTextureSize, _screenSize.x),
                                       Mathf.Min(_maxRenderTextureSize, _screenSize.y));
            _downScale = new Vector2(
                _screenSize.x == 0 ? 1f : (float)_usedSize.x / _screenSize.x,
                _screenSize.y == 0 ? 1f : (float)_usedSize.y / _screenSize.y);

            PrepareRenderTexture();
        }

        void PrepareRenderTexture()
        {
            var texSize = new Vector2Int(Mathf.NextPowerOfTwo(Mathf.Max(1, _usedSize.x)),
                                         Mathf.NextPowerOfTwo(Mathf.Max(1, _usedSize.y)));
            if (texSize != _allocatedSize)
            {
                if (_renderTexture) RenderTexture.ReleaseTemporary(_renderTexture);
                _renderTexture = RenderTexture.GetTemporary(texSize.x, texSize.y);
                _renderTexture.filterMode = FilterMode.Bilinear;
                _allocatedSize = texSize;
            }
        }

        void PrepareCommandBuffer(Vector3 ssMin, Vector3 ssMax)
        {
            _commandBuffer.Clear();
            _commandBuffer.SetRenderTarget(_renderTexture);
            _commandBuffer.ClearRenderTarget(true, true, Color.clear);
            _commandBuffer.SetViewMatrix(_targetCamera.worldToCameraMatrix);
            _commandBuffer.SetProjectionMatrix(CalculateProjectionMatrix(ssMin, ssMax, _targetCamera.pixelRect.size));
            var viewport = new Vector2(ssMax.x - ssMin.x, ssMax.y - ssMin.y);
            _commandBuffer.SetViewport(new Rect(Vector2.zero, viewport * _downScale));
        }

        Matrix4x4 CalculateProjectionMatrix(Vector3 ssMin, Vector3 ssMax, Vector2 fullSize)
        {
            if (_targetCamera.orthographic)
            {
                Vector2 camSize = new Vector2(_targetCamera.orthographicSize * 2f * _targetCamera.aspect,
                                              _targetCamera.orthographicSize * 2f);
                Vector2 min = new Vector2(ssMin.x, ssMin.y) / fullSize;
                Vector2 max = new Vector2(ssMax.x, ssMax.y) / fullSize;
                Vector2 off = new Vector2(-0.5f, -0.5f);
                min = (min + off) * camSize;
                max = (max + off) * camSize;
                return Matrix4x4.Ortho(min.x, max.x, min.y, max.y, float.MinValue, float.MaxValue);
            }
            else
            {
                FrustumPlanes fp = _targetCamera.projectionMatrix.decomposeProjection;
                Vector2 planes = new Vector2(fp.right - fp.left, fp.top - fp.bottom);
                Vector2 min = new Vector2(ssMin.x, ssMin.y) / fullSize * planes;
                Vector2 max = new Vector2(ssMax.x, ssMax.y) / fullSize * planes;
                fp.right = fp.left + max.x;
                fp.top = fp.bottom + max.y;
                fp.left += min.x;
                fp.bottom += min.y;
                return Matrix4x4.Frustum(fp);
            }
        }

        void RenderToRenderTexture(Mesh mesh)
        {
            _meshRenderer.GetPropertyBlock(_propertyBlock);
            _meshRenderer.GetSharedMaterials(_materials);
            for (int i = 0; i < _materials.Count; i++)
                foreach (int pass in _shaderPasses)
                    _commandBuffer.DrawMesh(mesh, transform.localToWorldMatrix,
                        _materials[i], _meshRenderer.subMeshStartIndex + i, pass, _propertyBlock);
            Graphics.ExecuteCommandBuffer(_commandBuffer);
        }

        void AssignAtQuad()
        {
            Transform qt = _quad.transform;
            qt.position = transform.position;
            qt.rotation = transform.rotation;
            qt.localScale = transform.localScale;

            var verts = new Vector3[4]
            {
                qt.InverseTransformPoint(_wnd0),
                qt.InverseTransformPoint(_wnd1),
                qt.InverseTransformPoint(_wnd2),
                qt.InverseTransformPoint(_wnd3),
            };
            _quadMesh.vertices = verts;
            _quadMesh.triangles = new int[6] { 0, 1, 2, 2, 1, 3 };
            _quadMesh.normals = new Vector3[4] { -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward };

            float maxU = (float)_usedSize.x / _allocatedSize.x;
            float maxV = (float)_usedSize.y / _allocatedSize.y;
            if (_downScale.x < 1f || _downScale.y < 1f)
            {
                maxU = _downScale.x * _screenSize.x / _allocatedSize.x;
                maxV = _downScale.y * _screenSize.y / _allocatedSize.y;
            }
            _quadMesh.uv = new Vector2[4]
            {
                new Vector2(_uv0.x * maxU, _uv0.y * maxV),
                new Vector2(_uv1.x * maxU, _uv1.y * maxV),
                new Vector2(_uv2.x * maxU, _uv2.y * maxV),
                new Vector2(_uv3.x * maxU, _uv3.y * maxV),
            };

            _quadFilter.mesh = _quadMesh;
            _quadMaterial.mainTexture = _renderTexture;
        }

        static Vector2 InverseLerp(Vector3 a, Vector3 b, Vector3 v)
        {
            return new Vector2(
                Mathf.Approximately(b.x, a.x) ? 0f : (v.x - a.x) / (b.x - a.x),
                Mathf.Approximately(b.y, a.y) ? 0f : (v.y - a.y) / (b.y - a.y));
        }
    }
}
