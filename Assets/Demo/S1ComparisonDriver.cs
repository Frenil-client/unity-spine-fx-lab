using System.Collections;
using UnityEngine;
using SpineFxLab.Core;

namespace SpineFxLab.Demo
{
    /// <summary>
    /// 성능 비교용 자동 시퀀스. 고정 카운트에서 단계를 순회해 한 번의 녹화로 레버 효과를 보여준다:
    ///   0) BEFORE - naive (컬링 off)
    ///   1) AFTER  - managed (오프스크린 컬링) -> FPS 회복
    ///   2) + MPB Tint (인스턴스별 색을 MPB 로, 단일 머티리얼 공유)
    /// 라벨과 스폰너의 FPS/Updating 오버레이를 함께 띄운다. OS 녹화로 캡처.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class S1ComparisonDriver : MonoBehaviour
    {
        [SerializeField] SpineFxBenchmarkSpawner _spawner;
        [Tooltip("비교에 쓸 인스턴스 수(화면을 넘쳐 다수 오프스크린이어야 컬링 효과가 보임).")]
        [SerializeField] int _count = 300;
        [Tooltip("각 단계 유지 시간(초).")]
        [SerializeField] float _hold = 4f;
        [SerializeField] float _startDelay = 1f;
        [Tooltip("전체 시퀀스 반복 횟수. 0이면 무한.")]
        [SerializeField] int _loops = 0;
        [Tooltip("끝나면 Play 모드 자동 종료(에디터). GIF 캡처용.")]
        [SerializeField] bool _exitPlayWhenDone = false;
        [SerializeField] bool _showLabel = true;

        const int PhaseCount = 3;
        int _phase;
        GUIStyle _style;

        void OnEnable()
        {
            if (_spawner == null) _spawner = GetComponent<SpineFxBenchmarkSpawner>();
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            if (_spawner == null) yield break;
            if (_startDelay > 0f) yield return new WaitForSeconds(_startDelay);
            _spawner.Spawn(_count);

            int done = 0;
            while (_loops <= 0 || done < _loops)
            {
                for (int p = 0; p < PhaseCount; p++)
                {
                    ApplyPhase(p);
                    yield return new WaitForSeconds(_hold);
                }
                done++;
            }

            ApplyPhase(0);
            if (_exitPlayWhenDone)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }
        }

        void ApplyPhase(int p)
        {
            _phase = p;
            switch (p)
            {
                case 0: _spawner.SetManaged(false); _spawner.SetMpbTint(false); break;  // naive
                case 1: _spawner.SetManaged(true);  _spawner.SetMpbTint(false); break;  // 컬링
                default: _spawner.SetManaged(true); _spawner.SetMpbTint(true);  break;  // 컬링 + MPB 틴트
            }
        }

        string PhaseLabel =>
            _phase == 0 ? "BEFORE - naive (cull off)" :
            _phase == 1 ? "AFTER - managed (offscreen cull)" :
                          "+ MPB Tint (per-instance color, 1 material)";

        Color PhaseColor =>
            _phase == 0 ? new Color(1f, 0.6f, 0.4f) :
            _phase == 1 ? new Color(0.4f, 1f, 0.6f) :
                          new Color(0.5f, 0.8f, 1f);

        void OnGUI()
        {
            if (!_showLabel) return;
            if (_style == null)
                _style = new GUIStyle(GUI.skin.label)
                { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            _style.normal.textColor = PhaseColor;

            const float w = 470f, x = 12f, y = 162f;   // 스탯 패널(8,6,520,150) 바로 아래
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(x - 4f, y, w, 36f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y + 4f, w - 8f, 28f), PhaseLabel, _style);
        }
    }
}
