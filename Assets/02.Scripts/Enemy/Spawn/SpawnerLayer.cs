using UnityEngine;

public sealed class SpawnerLayer : MonoBehaviour
{
    private static SpawnerLayer _instance;

    public static Transform Root
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<SpawnerLayer>();
                if (_instance == null)
                    _instance = new GameObject(nameof(SpawnerLayer)).AddComponent<SpawnerLayer>();
            }

            return _instance.transform;
        }
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}
