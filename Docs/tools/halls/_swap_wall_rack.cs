// Halls_Props Block 층의 WallWeaponRack(벽걸이, 충돌 없음) 3개를 같은 크기의 바닥 거치대로 바꿈 (바꿀 칸만 SetTile, 프리팹만 수정)
string prefabPath = "Assets/03.Prefabs/Environment/Rooms/Halls_Props.prefab";
string dir = "Assets/04.Sprites/Tile/ZoneProps/Forge/";
var swaps = new (int x, int y, string tile)[] { (34, 210, "WeaponRack_Spears"), (93, 242, "WeaponRack_Swords"), (98, 240, "WeaponRack_Spears") };
var wall = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.TileBase>(dir + "WallWeaponRack.asset");
var root = UnityEditor.PrefabUtility.LoadPrefabContents(prefabPath);
try
{
    var tm = root.transform.Find("Halls_Props Block").GetComponent<UnityEngine.Tilemaps.Tilemap>();
    foreach (var s in swaps)
    {
        var cell = new UnityEngine.Vector3Int(s.x, s.y, 0);
        if (tm.GetTile(cell) != wall) throw new System.Exception("WallWeaponRack이 아님: " + cell);
        tm.SetTile(cell, UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.TileBase>(dir + s.tile + ".asset"));
    }
    tm.CompressBounds();
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
}
finally
{
    UnityEditor.PrefabUtility.UnloadPrefabContents(root);
}
return "swapped " + swaps.Length;
