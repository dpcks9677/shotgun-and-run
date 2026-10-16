using System.Collections.Generic;
using System.Linq;
using ShotgunAndRun.UI.Dialogue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PlayerComponent = ShotgunAndRun.Player.Player;

namespace ShotgunAndRun.Cutscene.Editor
{
    /// <summary>
    /// 컷씬 에셋 인스펙터. 열린 씬에 필요한 인물 · 마커 · 신호가 있는지 보여 주고, 빠진 마커를 만들어 줍니다.
    /// </summary>
    [CustomEditor(typeof(CutsceneAsset), true)]
    public sealed class CutsceneAssetEditor : UnityEditor.Editor
    {
        private const string CutsceneRootName = "[Cutscene]";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var asset = (CutsceneAsset)target;
            var requirements = asset.CollectRequirements();

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("씬 검사 (열린 씬 기준)", EditorStyles.boldLabel);

            foreach (string error in requirements.Errors)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }

            var director = Object.FindFirstObjectByType<CutsceneDirector>();
            DrawDirector(asset, director);
            DrawActors(requirements, director);
            DrawMarkers(asset, requirements);
            DrawSignals(requirements);

            if (Application.isPlaying && director != null)
            {
                EditorGUILayout.Space(8);
                using (new EditorGUI.DisabledScope(CutsceneDirector.IsPlaying))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("지금 재생 (위치 이동 & 재생)"))
                        {
                            director.TeleportPlayerToCutscene(asset);
                            director.Play(asset);
                        }

                        if (GUILayout.Button("위치로 이동", GUILayout.Width(80)))
                        {
                            director.TeleportPlayerToCutscene(asset);
                        }
                    }
                }
            }
        }

        private static void DrawDirector(CutsceneAsset asset, CutsceneDirector director)
        {
            if (director == null)
            {
                EditorGUILayout.HelpBox("씬에 CutsceneDirector가 없습니다. CutsceneDirector 프리팹을 씬에 놓으세요.", MessageType.Error);
                return;
            }

            if (director.Cutscenes.Contains(asset))
            {
                Row(true, $"Director에 등록됨 ({director.name})");
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                Row(false, "Director에 등록되지 않음");
                if (GUILayout.Button("등록", GUILayout.Width(60)))
                {
                    var serialized = new SerializedObject(director);
                    var list = serialized.FindProperty("_cutscenes");
                    list.arraySize++;
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = asset;
                    serialized.ApplyModifiedProperties();
                    EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
                }
            }
        }

        private static void DrawActors(CutsceneRequirements requirements, CutsceneDirector director)
        {
            if (requirements.Actors.Count == 0) return;

            EditorGUILayout.LabelField("등장인물", EditorStyles.miniBoldLabel);
            var actors = Object.FindObjectsByType<CutsceneActor>(FindObjectsSortMode.None);
            bool hasPlayer = Object.FindFirstObjectByType<PlayerComponent>() != null;
            CharacterProfileSO playerProfile = director != null ? director.PlayerProfile : null;

            foreach (var profile in requirements.Actors)
            {
                if (profile == playerProfile)
                {
                    Row(hasPlayer, $"{profile.name} (플레이어)");
                    continue;
                }

                var found = actors.Where(a => a.Profile == profile).ToArray();
                string note = found.Length > 1 ? $" — {found.Length}개 (하나만 있어야 함)" : "";
                ObjectRow(found.Length == 1, profile.name + note, found.FirstOrDefault());
            }
        }

        private static void DrawMarkers(CutsceneAsset asset, CutsceneRequirements requirements)
        {
            if (requirements.Markers.Count == 0) return;

            EditorGUILayout.LabelField("마커", EditorStyles.miniBoldLabel);
            var markers = Object.FindObjectsByType<CutsceneMarker>(FindObjectsSortMode.None);
            var missing = new List<string>();

            foreach (string key in requirements.Markers)
            {
                var own = markers.Where(m => m.Cutscene == asset && m.Key == key).ToArray();
                var found = own.Length > 0 ? own : markers.Where(m => m.Cutscene == null && m.Key == key).ToArray();
                if (found.Length == 0) missing.Add(key);

                string note = found.Length > 1 ? $" — {found.Length}개 (하나만 있어야 함)" : own.Length == 0 && found.Length == 1 ? " (공용)" : "";
                ObjectRow(found.Length == 1, key + note, found.FirstOrDefault());
            }

            if (missing.Count > 0 && !Application.isPlaying && GUILayout.Button($"빠진 마커 {missing.Count}개 만들기"))
            {
                CreateMarkers(asset, missing);
            }
        }

        private static void DrawSignals(CutsceneRequirements requirements)
        {
            if (requirements.Signals.Count == 0) return;

            EditorGUILayout.LabelField("신호", EditorStyles.miniBoldLabel);
            var signals = Object.FindObjectsByType<CutsceneSignal>(FindObjectsSortMode.None);
            foreach (string key in requirements.Signals)
            {
                ObjectRow(signals.Any(s => s.Key == key), key, signals.FirstOrDefault(s => s.Key == key));
            }
        }

        // 씬 루트의 [Cutscene]/<컷씬 ID> 아래에 마커를 만들고, 씬 뷰 중앙에 놓는다
        private static void CreateMarkers(CutsceneAsset asset, List<string> keys)
        {
            var root = GameObject.Find(CutsceneRootName);
            if (root == null)
            {
                root = new GameObject(CutsceneRootName);
                Undo.RegisterCreatedObjectUndo(root, "Create Cutscene Root");
            }

            string groupName = string.IsNullOrWhiteSpace(asset.Id) ? asset.name : asset.Id;
            var group = root.transform.Find(groupName);
            if (group == null)
            {
                group = new GameObject(groupName).transform;
                Undo.RegisterCreatedObjectUndo(group.gameObject, "Create Cutscene Marker Group");
                Undo.SetTransformParent(group, root.transform, "Create Cutscene Marker Group");
            }

            Vector3 origin = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            origin.z = 0f;

            for (int i = 0; i < keys.Count; i++)
            {
                var markerObject = new GameObject($"CM_{groupName}_{keys[i]}");
                Undo.RegisterCreatedObjectUndo(markerObject, "Create Cutscene Marker");
                Undo.SetTransformParent(markerObject.transform, group, "Create Cutscene Marker");
                markerObject.transform.position = origin + Vector3.right * (i * 0.6f);

                var marker = markerObject.AddComponent<CutsceneMarker>();
                var serialized = new SerializedObject(marker);
                serialized.FindProperty("_cutscene").objectReferenceValue = asset;
                serialized.FindProperty("_key").stringValue = keys[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = group.gameObject;
        }

        private static void Row(bool ok, string label)
        {
            EditorGUILayout.LabelField((ok ? "✔  " : "✘  ") + label, ok ? EditorStyles.label : ErrorStyle);
        }

        private static void ObjectRow(bool ok, string label, Object sceneObject)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                Row(ok, label);
                using (new EditorGUI.DisabledScope(sceneObject == null))
                {
                    if (GUILayout.Button("선택", GUILayout.Width(44)))
                    {
                        Selection.activeObject = sceneObject;
                        EditorGUIUtility.PingObject(sceneObject);
                    }
                }
            }
        }

        private static GUIStyle _errorStyle;

        private static GUIStyle ErrorStyle
        {
            get
            {
                if (_errorStyle == null)
                {
                    _errorStyle = new GUIStyle(EditorStyles.label);
                    _errorStyle.normal.textColor = new Color(0.95f, 0.35f, 0.3f);
                }
                return _errorStyle;
            }
        }
    }
}
