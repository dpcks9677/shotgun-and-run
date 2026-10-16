using UnityEngine;

namespace ShotgunAndRun.Core
{
    // Play를 시작하면 첫 씬을 불러오기 전에 GameManager를 1개 생성합니다.
    // 어떤 씬에서 Play해도 GameManager가 존재하도록 보장합니다.
    internal static class GameBootstrap
    {
        // Assets/Resources/GameManager.prefab
        private const string PrefabPath = "GameManager";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateGameManager()
        {
            if (GameManager.Instance != null)
            {
                return;
            }

            // 프리팹이 없으면 기본값으로 생성합니다.
            GameManager prefab = Resources.Load<GameManager>(PrefabPath);
            GameManager instance = prefab != null
                ? Object.Instantiate(prefab)
                : new GameObject().AddComponent<GameManager>();
            instance.name = nameof(GameManager);
        }
    }
}
