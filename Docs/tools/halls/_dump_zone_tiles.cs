// 읽기 전용: 존 방 프리팹의 타일맵별 칸 (Zone|맵|x|y|타일)
var sb = new System.Text.StringBuilder();
foreach (var n in new[] { "Z1_Forge", "Z2_DwarfWorkshop", "Z4_EngineRoom", "Z6_Butchery", "Z7_GoblinCamp", "Z3_Sewer" })
{
    var root = UnityEditor.PrefabUtility.LoadPrefabContents("Assets/03.Prefabs/Environment/Rooms/" + n + ".prefab");
    sb.Append("#ROOT|").Append(n).Append('|').Append(root.transform.position.x).Append('|').Append(root.transform.position.y).Append('\n');
    foreach (var tm in root.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>(true))
        foreach (var c in tm.cellBounds.allPositionsWithin)
        {
            var t = tm.GetTile(c);
            if (t == null) continue;
            sb.Append(n).Append('|').Append(tm.name).Append('|').Append(c.x).Append('|').Append(c.y).Append('|').Append(t.name).Append('\n');
        }
    UnityEditor.PrefabUtility.UnloadPrefabContents(root);
}
System.IO.File.WriteAllText("Backups/zone_tiles.txt", sb.ToString());
return sb.Length;
