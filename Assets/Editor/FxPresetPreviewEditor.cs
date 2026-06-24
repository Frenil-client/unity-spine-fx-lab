using UnityEditor;
using UnityEngine;
using SpineFxLab.Core;
using SpineFxLab.Data;

namespace SpineFxLab.Editor
{
    /// <summary>
    /// T-1 FxPreset 오서링/프리뷰. 인스펙터에서 프리셋을 편집하면 플레이 모드 없이도 씬의 대상
    /// SpineFxController 에 즉시 반영된다. 런타임 API(Play/Clear)를 그대로 호출하고, 값은 MPB 로
    /// 들어가므로 에디트 모드 렌더에도 보인다. 비개발자(기획/아트)가 수치를 보며 조정하는 용도.
    /// </summary>
    [CustomEditor(typeof(FxPreset))]
    public sealed class FxPresetPreviewEditor : UnityEditor.Editor
    {
        static readonly (string keyword, string prop)[] NumericKeywords =
        {
            ("_FLASH_ON", "_FlashEnabled"),
            ("_TINT_ON", "_TintEnabled"),
            ("_DISSOLVE_ON", "_DissolveEnabled"),
        };

        SpineFxController _target;
        bool _live = true;

        void OnEnable()
        {
            if (_target == null) _target = FindController();
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool changed = EditorGUI.EndChangeCheck();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("라이브 프리뷰 (플레이 불필요)", EditorStyles.boldLabel);
            _target = (SpineFxController)EditorGUILayout.ObjectField("대상 Controller", _target, typeof(SpineFxController), true);
            _live = EditorGUILayout.Toggle("라이브", _live);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("지금 적용")) Apply();
                if (GUILayout.Button("키워드 켜기")) EnableKeywords();
                if (GUILayout.Button("원본 복귀")) ClearTarget();
            }

            if (_target == null)
                EditorGUILayout.HelpBox("씬에 SpineFxController 가 있어야 프리뷰됩니다.", MessageType.Info);
            else
                EditorGUILayout.HelpBox("프리뷰가 안 보이면 [키워드 켜기]로 대상 머티리얼의 _FLASH_ON/_TINT_ON/_DISSOLVE_ON 을 켜세요.", MessageType.None);

            if (changed && _live) Apply();
        }

        void Apply()
        {
            if (_target == null) return;
            _target.Play((FxPreset)target);   // 런타임 API 그대로 (에디트 모드 MPB 반영)
            SceneView.RepaintAll();
        }

        void ClearTarget()
        {
            if (_target == null) return;
            _target.Clear();
            SceneView.RepaintAll();
        }

        void EnableKeywords()
        {
            if (_target == null) return;
            var r = _target.GetComponent<Renderer>();
            var mat = r != null ? r.sharedMaterial : null;
            if (mat == null) return;
            foreach (var k in NumericKeywords)
            {
                mat.EnableKeyword(k.keyword);
                if (mat.HasProperty(k.prop)) mat.SetFloat(k.prop, 1f);
            }
            EditorUtility.SetDirty(mat);
            Apply();
        }

        static SpineFxController FindController()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<SpineFxController>();
#else
            return Object.FindObjectOfType<SpineFxController>();
#endif
        }
    }
}
