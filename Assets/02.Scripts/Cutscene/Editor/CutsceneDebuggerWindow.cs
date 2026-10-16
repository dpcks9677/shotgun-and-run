using System.Linq;
using ShotgunAndRun.Core;
using UnityEditor;
using UnityEngine;

namespace ShotgunAndRun.Cutscene.Editor
{
    /// <summary>
    /// Play 모드에서 씬의 컷씬을 바로 재생하거나, 완료 플래그를 지워 다시 볼 수 있게 하는 디버그 창입니다.
    /// 재생하면 씬의 진행 흐름(ICutsceneDebugHost, 예: TutorialFlow)이 그 컷씬이 시작되기 직전의 상태를 만든 뒤 재생합니다.
    /// 이미 본 컷씬이나 앞 · 뒤 단계로 옮겨도 같은 상태에서 다시 볼 수 있고, 재생 중이면 중단하고 새로 시작합니다.
    /// 메뉴: ShotgunAndRun → Cutscene Debugger
    /// </summary>
    public sealed class CutsceneDebuggerWindow : EditorWindow
    {
        private Vector2 _scroll;
        private bool _teleportPlayer = true;
        private bool _resumeAfter;
        private bool _saveCompletionFlags;

        [MenuItem("ShotgunAndRun/Cutscene Debugger")]
        private static void Open()
        {
            GetWindow<CutsceneDebuggerWindow>("Cutscene Debugger");
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Play 모드에서 사용합니다. 씬 검사는 컷씬 에셋 인스펙터에서 할 수 있습니다.", MessageType.Info);
                return;
            }

            var director = CutsceneDirector.Current;
            if (director == null)
            {
                EditorGUILayout.HelpBox("씬에 CutsceneDirector가 없습니다.", MessageType.Warning);
                return;
            }

            var quests = GameManager.Instance != null ? GameManager.Instance.Progress?.Quests : null;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("재생 중", director.Playing != null ? director.Playing.Id + (director.IsDebugRun ? " (디버그)" : string.Empty) : "-");
                using (new EditorGUI.DisabledScope(!CutsceneDirector.IsPlaying))
                {
                    if (GUILayout.Button("중단", GUILayout.Width(50)))
                    {
                        director.Abort();
                    }
                }
            }

            foreach (string state in director.DescribeDebugStates())
            {
                EditorGUILayout.HelpBox(state, MessageType.None);
            }

            _teleportPlayer = EditorGUILayout.ToggleLeft("재생 시 플레이어를 연출 위치로 텔레포트", _teleportPlayer);
            _resumeAfter = EditorGUILayout.ToggleLeft("끝까지 재생하면 그 지점부터 실제 진행을 이어서 진행", _resumeAfter);
            _saveCompletionFlags = EditorGUILayout.ToggleLeft("끝까지 재생하면 컷씬 완료 플래그를 세이브에 저장 (기본: 저장 안 함)", _saveCompletionFlags);
            if (quests == null)
            {
                EditorGUILayout.HelpBox("진행 데이터(GameManager)가 없어 플래그를 확인 · 수정할 수 없습니다.", MessageType.Info);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var asset in director.Cutscenes.Where(c => c != null))
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(asset.Id, EditorStyles.boldLabel);
                    if (quests != null)
                    {
                        bool done = asset.CompletionFlags.Count > 0 && asset.CompletionFlags.All(quests.HasFlag);
                        bool playable = asset.RequiredFlags.All(quests.HasFlag) && !asset.BlockedFlags.Any(quests.HasFlag);
                        EditorGUILayout.LabelField("상태", (done ? "완료함" : "미완료") + " / " + (playable ? "재생 가능" : "조건 불충족"));
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("재생 (조건 무시 · 상태 복원)"))
                        {
                            director.PlayDebug(asset, new CutsceneDebugOptions
                            {
                                Teleport = _teleportPlayer,
                                ResumeAfter = _resumeAfter,
                                SaveCompletionFlags = _saveCompletionFlags,
                            });
                        }

                        if (GUILayout.Button("상태 · 위치만 맞추기", GUILayout.Width(120)))
                        {
                            director.PrepareDebug(asset, teleport: true);
                        }

                        using (new EditorGUI.DisabledScope(quests == null))
                        {
                            if (GUILayout.Button("완료 플래그 지우기"))
                            {
                                foreach (string flag in asset.CompletionFlags)
                                {
                                    quests.Flags.Remove(flag);
                                }
                                GameManager.Instance.SaveProgress();
                            }
                        }

                        if (GUILayout.Button("에셋", GUILayout.Width(44)))
                        {
                            Selection.activeObject = asset;
                        }
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
