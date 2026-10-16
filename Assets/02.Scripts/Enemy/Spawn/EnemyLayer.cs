using System.Collections.Generic;
using UnityEngine;

public sealed class EnemyLayer : MonoBehaviour
{
    private static EnemyLayer _instance;
    private readonly Dictionary<Enemy, Stack<Enemy>> _available = new();
    private readonly Dictionary<Enemy, Enemy> _prefabByInstance = new();
    private readonly HashSet<Enemy> _active = new();

    public static IEnumerable<Enemy> ActiveEnemies => _instance != null ? _instance._active : System.Array.Empty<Enemy>();

    public static Transform Root
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<EnemyLayer>();
                if (_instance == null)
                    _instance = new GameObject(nameof(EnemyLayer)).AddComponent<EnemyLayer>();
            }

            return _instance.transform;
        }
    }

    public static Enemy Acquire(Enemy prefab, Vector2 position)
    {
        EnemyLayer layer = Root.GetComponent<EnemyLayer>();
        if (!layer._available.TryGetValue(prefab, out Stack<Enemy> available))
        {
            available = new Stack<Enemy>();
            layer._available.Add(prefab, available);
        }

        Enemy enemy;
        if (available.Count > 0)
        {
            enemy = available.Pop();
        }
        else
        {
            enemy = Instantiate(prefab, position, Quaternion.identity, layer.transform);
            enemy.CaptureInitialColliderStates();
        }

        layer._prefabByInstance[enemy] = prefab;
        enemy.transform.position = position;
        enemy.gameObject.SetActive(true);
        layer._active.Add(enemy);
        return enemy;
    }

    public static void Release(Enemy enemy)
    {
        if (!enemy.gameObject.activeSelf) return;

        if (_instance == null || !_instance._prefabByInstance.TryGetValue(enemy, out Enemy prefab))
        {
            Destroy(enemy.gameObject);
            return;
        }

        enemy.gameObject.SetActive(false);
        _instance._active.Remove(enemy);
        enemy.ResetForPool();
        _instance._available[prefab].Push(enemy);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}
