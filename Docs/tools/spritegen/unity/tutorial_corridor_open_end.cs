// 빨강 복도 뒤쪽 끝(시작 지점 쪽)의 막는 벽(위 3줄 벽 + 모서리)을 걷어 열린 통로로 만든다 (2026-10-08 사용자: "벽이 여전히 막혀있어")
//  - 바닥은 y54까지, 그 위 벽 y55~57(x2~9)을 바깥 어둠 타일(벽 시트 56)로 바꾸고, 모서리(2,54)(9,54)는 옆벽 조각으로 이음.
//  - 바닥이 어둠에 바로 이어져 끝이 보이지 않는다(시작 화면은 y48까지만 보이고 페이드인으로 덮임).
//  주의: dungeon_paint로 TutorialMapScene을 다시 칠하면 끝 벽이 되살아난다 → 칠한 뒤 이 스크립트를 다시 실행.
// TutorialMapScene을 열고 Play 아님 상태에서 실행. 바꿀 칸만 SetTile (통째로 비우지 않음).
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
// 끝 바닥 줄 찾기 (x 3~8의 가장 높은 바닥)
int top = -1;
for (int y = 70; y >= 30; y--) { if (ground.HasTile(new UnityEngine.Vector3Int(6, y, 0))) { top = y; break; } }
if (top < 0) return "복도 끝 바닥을 못 찾음";
var leftEdge = wall.GetTile(new UnityEngine.Vector3Int(2, top - 1, 0));
var rightEdge = wall.GetTile(new UnityEngine.Vector3Int(9, top - 1, 0));
var voidTile = wall.GetTile(new UnityEngine.Vector3Int(0, top + 6, 0));
if (leftEdge == null || rightEdge == null || voidTile == null) return "이웃 타일을 못 찾음 (left=" + leftEdge + ", right=" + rightEdge + ", void=" + voidTile + ")";
UnityEditor.Undo.RecordObject(wall, "복도 끝 열기");
int changed = 0;
for (int y = top + 1; y <= top + 3; y++)
    for (int x = 2; x <= 9; x++)
    {
        var p = new UnityEngine.Vector3Int(x, y, 0);
        if (wall.GetTile(p) == voidTile) continue;
        wall.SetTile(p, voidTile); changed++;
    }
wall.SetTile(new UnityEngine.Vector3Int(2, top, 0), leftEdge);
wall.SetTile(new UnityEngine.Vector3Int(9, top, 0), rightEdge);
changed += 2;
UnityEditor.EditorUtility.SetDirty(wall);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
return "끝 바닥 y" + top + ", 벽 " + changed + "칸 변경 (어둠 타일 " + voidTile.name + ")";
