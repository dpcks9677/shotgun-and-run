using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShotgunAndRun.Core
{
    // 씬 이름과 게임 상태의 대응표입니다.
    // 씬을 추가할 때(보스 스테이지, 튜토리얼 등) 코드를 고치지 않고 GameManager 인스펙터에서 항목만 추가합니다.
    // 표에 없는 씬은 대기 상태(None)입니다.
    [Serializable]
    public sealed class SceneRouter
    {
        [SerializeField]
        private List<SceneRoute> _routes = new List<SceneRoute>
        {
            new SceneRoute("TitleScene", GameState.Title),
            new SceneRoute("ShelterScene", GameState.Shelter),
            new SceneRoute("DungeonStageScene", GameState.Run),
            new SceneRoute("TutorialScene", GameState.Tutorial)
        };

        public IReadOnlyList<SceneRoute> Routes => _routes;

        public GameState Resolve(string sceneName)
        {
            foreach (SceneRoute route in _routes)
            {
                if (route != null && route.SceneName == sceneName)
                {
                    return route.State;
                }
            }

            return GameState.None;
        }

        // 해당 상태로 가는 첫 번째 씬을 찾습니다.
        public bool TryGetScene(GameState state, out string sceneName)
        {
            foreach (SceneRoute route in _routes)
            {
                if (route != null && route.State == state && !string.IsNullOrEmpty(route.SceneName))
                {
                    sceneName = route.SceneName;
                    return true;
                }
            }

            sceneName = null;
            return false;
        }

#if UNITY_EDITOR
        // Build Settings에 등록되지 않은 씬 이름을 찾습니다. (오타, 씬 이름 변경 확인용)
        public List<string> FindScenesMissingFromBuild()
        {
            HashSet<string> buildScenes = new HashSet<string>();
            foreach (UnityEditor.EditorBuildSettingsScene scene in UnityEditor.EditorBuildSettings.scenes)
            {
                buildScenes.Add(System.IO.Path.GetFileNameWithoutExtension(scene.path));
            }

            List<string> missing = new List<string>();
            foreach (SceneRoute route in _routes)
            {
                if (route != null && !string.IsNullOrEmpty(route.SceneName) && !buildScenes.Contains(route.SceneName))
                {
                    missing.Add(route.SceneName);
                }
            }

            return missing;
        }
#endif
    }

    [Serializable]
    public sealed class SceneRoute
    {
        public string SceneName;
        public GameState State;

        public SceneRoute()
        {
        }

        public SceneRoute(string sceneName, GameState state)
        {
            SceneName = sceneName;
            State = state;
        }
    }
}
