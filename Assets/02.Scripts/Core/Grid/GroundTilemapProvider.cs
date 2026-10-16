using UnityEngine;
using UnityEngine.Tilemaps;

public class GroundTilemapProvider : MonoBehaviour
{
    private static Tilemap _groundTilemap = null;
    public static Tilemap Ground => _groundTilemap;

    private void Awake()
    {
        if (_groundTilemap == null)
        {
            _groundTilemap = GetComponent<Tilemap>();
        }
    }

    private void OnDestroy()
    {
        _groundTilemap = null;
    }
}
