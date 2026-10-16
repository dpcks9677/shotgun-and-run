// 회색(도주) 복도 오른쪽 끝 = 탈출구의 막는 끝벽을 걷어 열린 통로로 만든다 (2026-10-08 사용자: "우측 탈출구도 열어야 해")
//  - 바닥은 x120까지(y25~30). 오른쪽 끝에 있던 테두리 열(x121)과 위·아래 벽의 끝 조각(x120 · 121)이 끝을 막아 보였음.
//  - 위·아래 벽은 x120까지 곧게 이어 붙이고, 그 너머(x121)는 바깥 어둠 타일 → 바닥이 어둠으로 바로 이어짐 (복도 뒤쪽 끝과 같은 처리).
//  주의: dungeon_paint로 TutorialMapScene을 다시 칠하면 되살아난다 → 칠한 뒤 이 스크립트를 다시 실행. 바꿀 칸만 SetTile.
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.path != "Assets/_Sandbox/Yechann/TutorialMapScene.unity") return "활성 씬이 TutorialMapScene이 아님: " + scene.path;
if (UnityEngine.Application.isPlaying) return "Play 모드에서는 실행하지 않음";
UnityEngine.Tilemaps.Tilemap wall = null, ground = null;
foreach (var r in scene.GetRootGameObjects())
    if (r.name == "Grid")
    {
        wall = r.transform.Find("Wall Tilemap").GetComponent<UnityEngine.Tilemaps.Tilemap>();
        ground = r.transform.Find("Ground Tilemap").GetComponent<UnityEngine.Tilemaps.Tilemap>();
    }
if (wall == null || ground == null) return "타일맵 없음";
int endX = -1;
for (int x = 140; x >= 100; x--) { if (ground.HasTile(new UnityEngine.Vector3Int(x, 28, 0))) { endX = x; break; } }
if (endX < 0) return "복도 오른쪽 끝 바닥을 못 찾음";
var voidTile = wall.GetTile(new UnityEngine.Vector3Int(endX + 4, 28, 0));
if (voidTile == null) return "어둠 타일을 못 찾음";
UnityEditor.Undo.RecordObject(wall, "탈출구 열기");
int changed = 0;
// 바닥 줄(y25~30): 끝 테두리 열 → 어둠
for (int y = 25; y <= 30; y++)
{
    var p = new UnityEngine.Vector3Int(endX + 1, y, 0);
    if (wall.GetTile(p) != voidTile) { wall.SetTile(p, voidTile); changed++; }
}
// 위·아래 벽(y31~33 · y24): 끝 조각을 곧은 조각으로 바꾸고 그 너머는 어둠
int[] wallRows = { 24, 31, 32, 33 };
foreach (int y in wallRows)
{
    var straight = wall.GetTile(new UnityEngine.Vector3Int(endX - 2, y, 0));
    if (straight == null) continue;
    var pe = new UnityEngine.Vector3Int(endX, y, 0);
    if (wall.GetTile(pe) != straight) { wall.SetTile(pe, straight); wall.SetTransformMatrix(pe, wall.GetTransformMatrix(new UnityEngine.Vector3Int(endX - 2, y, 0))); changed++; }
    var pv = new UnityEngine.Vector3Int(endX + 1, y, 0);
    if (wall.GetTile(pv) != voidTile) { wall.SetTile(pv, voidTile); changed++; }
}
UnityEditor.EditorUtility.SetDirty(wall);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
return "끝 바닥 x" + endX + ", 벽 " + changed + "칸 변경";
