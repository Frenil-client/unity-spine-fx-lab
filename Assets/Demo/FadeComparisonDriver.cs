using System.Collections;
using UnityEngine;
using SpineFxLab.Core;

namespace SpineFxLab.Demo
{
    /// <summary>
    /// 페이드 비교 시연. 두 스켈레톤을 같이 페이드시켜 naive(부위별 알파 -> 고스팅) 와
    /// FadeGroup(통합 투명 -> 클린) 을 좌우로 나란히 보여준다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FadeComparisonDriver : MonoBehaviour
    {
        [Tooltip("단순 글로벌 알파 - 부위 겹침이 비친다(고스팅).")]
        [SerializeField] SpineFxController _naive;
        [Tooltip("FadeGroup - RT 불투명 렌더 후 단일 알파 합성(클린).")]
        [SerializeField] SpineFxController _group;

        [SerializeField] float _fadeDuration = 1.5f;
        [SerializeField] float _hold = 0.8f;
        [SerializeField] float _startDelay = 0.5f;
        [Tooltip("반복 횟수. 0이면 무한.")]
        [SerializeField] int _loops = 0;
        [Tooltip("끝나면 Play 모드 자동 종료(에디터). GIF 캡처용.")]
        [SerializeField] bool _exitPlayWhenDone = false;
        [SerializeField] bool _showLabels = true;

        GUIStyle _style;

        void OnEnable() => StartCoroutine(Run());

        IEnumerator Run()
        {
            if (_startDelay > 0f) yield return new WaitForSeconds(_startDelay);

            int done = 0;
            while (_loops <= 0 || done < _loops)
            {
                yield return Fade(1f, 0f);                  // 페이드 아웃
                yield return new WaitForSeconds(_hold);
                yield return Fade(0f, 1f);                  // 복귀
                yield return new WaitForSeconds(_hold);
                done++;
            }

            if (_exitPlayWhenDone)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }
        }

        IEnumerator Fade(float from, float to)
        {
            float t = 0f;
            while (t < _fadeDuration)
            {
                t += Time.deltaTime;
                Apply(Mathf.Lerp(from, to, Mathf.Clamp01(t / _fadeDuration)));
                yield return null;
            }
            Apply(to);
        }

        void Apply(float alpha)
        {
            if (_naive != null) _naive.Fade(alpha, useGroup: false);   // 고스팅
            if (_group != null) _group.Fade(alpha, useGroup: true);    // 클린
        }

        void OnGUI()
        {
            if (!_showLabels) return;
            if (_style == null)
                _style = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.UpperCenter };
            _style.normal.textColor = Color.white;

            float w = Screen.width;
            GUI.Label(new Rect(0f, 12f, w * 0.5f, 28f), "naive  (per-part alpha -> ghosting)", _style);
            GUI.Label(new Rect(w * 0.5f, 12f, w * 0.5f, 28f), "FadeGroup  (unified -> clean)", _style);
        }
    }
}
