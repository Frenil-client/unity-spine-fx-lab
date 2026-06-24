using System;
using System.Collections;
using UnityEngine;
using Spine.Unity;
using SpineFxLab.Core;
using SpineFxLab.Data;
using SpineFxLab.Effects;

namespace SpineFxLab.Demo
{
    /// <summary>
    /// 통합 FX 쇼케이스. 한 스켈레톤(단일 SpineFx 머티리얼)이 오버레이 연출을 차례로 순회한다:
    /// flash/tint/dissolve(데이터 주도), 감전->빙결->중독->실드, 아웃라인->실루엣->RT 아웃라인,
    /// 마지막에 빙결+RT 아웃라인을 동시에(통합 머티리얼의 이점).
    /// 수치 효과는 FxPreset SO 를 Play(preset) 로, 상태이상은 StatusEffectDef 로 구동(전부 데이터 주도),
    /// 아웃라인/실루엣은 키워드/컴포넌트 토글. 게임 로직은 Play/SetStatus 수준만 호출한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpineFxShowcaseDriver : MonoBehaviour
    {
        [SerializeField] SpineFxController _controller;
        [SerializeField] SkeletonRenderer _skeletonRenderer;
        [SerializeField] SkeletonOutlineRt _outlineRt;
        [SerializeField] Material _fxMaterial;        // SpineFx (수치 키워드 ON, outline/silhouette OFF)
        [SerializeField] Material _originalMaterial;  // 아틀라스 원본(오버라이드 키)
        [SerializeField] StatusEffectDef[] _statusDefs; // 감전/빙결/중독/실드
        [SerializeField] FxPreset[] _fxPresets;         // flash/tint/dissolve 데이터 주도(수치는 SO)
        [SerializeField] Color _outlineColor = new Color(1f, 0.95f, 0.4f, 1f);
        [SerializeField] Color _silhouetteColor = new Color(0.1f, 0.1f, 0.12f, 1f);
        [SerializeField] float _hold = 1.2f;
        [SerializeField] float _ramp = 0.35f;
        [SerializeField] float _gap = 0.3f;
        [SerializeField] float _startDelay = 0.5f;
        [SerializeField] int _loops = 0;
        [SerializeField] bool _exitPlayWhenDone = false;
        [SerializeField] bool _showLabel = true;

        Material _runtimeMat;
        string _label = "";
        GUIStyle _style;

        static readonly int SilhouetteColorId = Shader.PropertyToID("_SilhouetteColor");
        static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

        void OnEnable()
        {
            if (_controller == null) _controller = GetComponent<SpineFxController>();
            if (_skeletonRenderer == null) _skeletonRenderer = GetComponent<SkeletonRenderer>();
            if (_outlineRt == null) _outlineRt = GetComponent<SkeletonOutlineRt>();
            if (_fxMaterial != null && _originalMaterial != null)
            {
                _runtimeMat = new Material(_fxMaterial);
                _runtimeMat.SetColor(OutlineColorId, _outlineColor);
                _runtimeMat.SetColor(SilhouetteColorId, _silhouetteColor);
                _skeletonRenderer.CustomMaterialOverride[_originalMaterial] = _runtimeMat;
            }
            if (_outlineRt != null) _outlineRt.SetColor(_outlineColor);
            StartCoroutine(Run());
        }

        void OnDisable() { if (_runtimeMat != null) Destroy(_runtimeMat); }

        IEnumerator Run()
        {
            if (_controller == null) yield break;
            if (_startDelay > 0f) yield return new WaitForSeconds(_startDelay);
            var gap = new WaitForSeconds(_gap);

            int done = 0;
            while (_loops <= 0 || done < _loops)
            {
                // ---- 데이터 주도 FX: flash/tint/dissolve (수치는 FxPreset SO 에서, 호출은 Play 만) ----
                if (_fxPresets != null)
                {
                    foreach (var preset in _fxPresets)
                    {
                        if (preset == null) continue;
                        _label = "preset: " + preset.name;
                        yield return Lerp(_ramp, w => _controller.Play(preset, w));        // 페이드 인
                        yield return new WaitForSeconds(_hold);
                        yield return Lerp(_ramp, w => _controller.Play(preset, 1f - w));   // 페이드 아웃
                        _controller.Clear();
                        yield return gap;
                    }
                }

                // ---- 상태이상 ----
                for (int i = 0; i < _statusDefs.Length; i++)
                {
                    var def = _statusDefs[i];
                    if (def == null) continue;
                    _label = "status - " + def.type;
                    _controller.SetStatus(def, true);
                    yield return new WaitForSeconds(_hold);
                    _controller.SetStatus(def, false);
                    yield return gap;
                }

                // ---- 아웃라인 ----
                _label = "outline (per-part)";
                Keyword("_OUTLINE_ON", "_OutlineEnabled", true);
                yield return new WaitForSeconds(_hold);
                Keyword("_OUTLINE_ON", "_OutlineEnabled", false);
                yield return gap;

                _label = "silhouette";
                Keyword("_SILHOUETTE_ON", "_SilhouetteEnabled", true);
                yield return new WaitForSeconds(_hold);
                Keyword("_SILHOUETTE_ON", "_SilhouetteEnabled", false);
                yield return gap;

                _label = "RT outline (whole silhouette)";
                if (_outlineRt != null) _outlineRt.SetOutline(true);
                yield return new WaitForSeconds(_hold);
                if (_outlineRt != null) _outlineRt.SetOutline(false);
                yield return gap;

                // ---- 조합: 빙결 + RT 아웃라인 동시 (단일 머티리얼 통합의 이점) ----
                _label = "combo - freeze + RT outline";
                var freeze = FindDef(StatusType.Freeze);
                if (freeze != null) _controller.SetStatus(freeze, true);
                if (_outlineRt != null) _outlineRt.SetOutline(true);
                yield return new WaitForSeconds(_hold * 1.5f);
                if (freeze != null) _controller.SetStatus(freeze, false);
                if (_outlineRt != null) _outlineRt.SetOutline(false);
                yield return gap;

                done++;
            }

            if (_exitPlayWhenDone)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }
        }

        void Keyword(string kw, string floatProp, bool on)
        {
            if (_runtimeMat == null) return;
            if (on) _runtimeMat.EnableKeyword(kw); else _runtimeMat.DisableKeyword(kw);
            if (_runtimeMat.HasProperty(floatProp)) _runtimeMat.SetFloat(floatProp, on ? 1f : 0f);
        }

        StatusEffectDef FindDef(StatusType type)
        {
            foreach (var d in _statusDefs) if (d != null && d.type == type) return d;
            return null;
        }

        static IEnumerator Lerp(float dur, Action<float> apply)
        {
            if (dur <= 0f) { apply(1f); yield break; }
            float t = 0f;
            while (t < dur) { t += Time.deltaTime; apply(Mathf.Clamp01(t / dur)); yield return null; }
            apply(1f);
        }

        void OnGUI()
        {
            if (!_showLabel) return;
            if (_style == null)
                _style = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            _style.normal.textColor = Color.white;
            const float w = 400f, x = 12f, y = 12f;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(x - 4f, y, w, 34f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y + 3f, w - 8f, 28f), "SpineFx: " + _label, _style);
        }
    }
}
