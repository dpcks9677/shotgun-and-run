// 튜토리얼 빨강 복도 입구 잔해(CollapseDebris)를 큰 잔해 A · B로 교체 (2026-10-08 사용자: "아까 작업한 큰 잔해들이 복도를 막아야 해")
// 먼저: python zones/dungeon/collapse_debris.py import (시트 TutorialCollapseDebris). TutorialMapScene을 열고 Play 아님 상태에서 실행.
//  - 임시 스프라이트 11개(Ruins 소품)를 지우고 Debris_A(앞) + Debris_B(뒤, 좌우 반전)를 넣는다. 둘 다 96x64(6x4칸)로 복도 폭 6칸을 채운다.
//  - 피벗 = 그림 중심. 앞 더미 A의 앞가장자리가 충돌 상자(6x3, 월드 y13.5~16.5) 아래변에 닿도록 A를 (0, +0.1)에 둔다. B는 뒤쪽 +1.4 (깊이감).
//  - 그리기 순서: Environment 레이어(Ground · Wall 위, 캐릭터 아래). 옛 임시 스프라이트는 Default 레이어라 바닥 타일맵(Background) 밑에 가려져 있었다.
//  - 오브젝트는 시작할 때 꺼져 있고 붕괴 신호(CollapseDebris)가 켠다. 충돌 상자는 그대로.
var sb = new System.Text.StringBuilder();
const string sheet = "Assets/04.Sprites/Tile/ZoneProps/TutorialCollapseDebris/TutorialCollapseDebris.png";
UnityEditor.AssetDatabase.ImportAsset(sheet, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
UnityEngine.Sprite a = null, b = null;
foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(sheet))
{
    var sp = o as UnityEngine.Sprite;
    if (sp == null) continue;
    if (sp.name == "CollapseDebris_A") a = sp;
    if (sp.name == "CollapseDebris_B") b = sp;
}
if (a == null || b == null) return "CollapseDebris_A/B 스프라이트 없음";
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.path != "Assets/_Sandbox/Yechann/TutorialMapScene.unity") return "활성 씬이 TutorialMapScene이 아님: " + scene.path;
if (UnityEngine.Application.isPlaying) return "Play 모드에서는 실행하지 않음";
var go = UnityEngine.GameObject.Find("/[Tutorial]/Props/CollapseDebris");
bool wasActive = go != null;
if (go == null)
{
    // 비활성이면 Find가 못 찾는다 → 부모에서 찾음
    var props = UnityEngine.GameObject.Find("/[Tutorial]/Props");
    if (props == null) return "[Tutorial]/Props를 찾지 못함";
    var t = props.transform.Find("CollapseDebris");
    if (t == null) return "CollapseDebris를 찾지 못함";
    go = t.gameObject;
}
int removed = 0;
for (int i = go.transform.childCount - 1; i >= 0; i--)
{
    UnityEditor.Undo.DestroyObjectImmediate(go.transform.GetChild(i).gameObject);
    removed++;
}
System.Func<string, UnityEngine.Sprite, UnityEngine.Vector3, bool, int, int> add = (name, sprite, pos, flip, order) =>
{
    var c = new UnityEngine.GameObject(name);
    UnityEditor.Undo.RegisterCreatedObjectUndo(c, name);
    c.transform.SetParent(go.transform, false);
    c.transform.localPosition = pos;
    var sr = c.AddComponent<UnityEngine.SpriteRenderer>();
    sr.sprite = sprite;
    sr.flipX = flip;
    sr.sortingLayerName = "Environment";
    sr.sortingOrder = order;
    return 0;
};
add("Debris_B", b, new UnityEngine.Vector3(0f, 1.4f, 0f), true, -1);
add("Debris_A", a, new UnityEngine.Vector3(0f, 0.1f, 0f), false, 0);
UnityEditor.EditorUtility.SetDirty(go);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
sb.AppendLine("CollapseDebris: 임시 스프라이트 " + removed + "개 제거, Debris_A(앞) · Debris_B(뒤, 반전) 추가. active=" + go.activeSelf + ", 콜라이더 " + go.GetComponent<UnityEngine.BoxCollider2D>().size);
return sb.ToString();
