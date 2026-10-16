// 튜토리얼 샷건 전시대를 TutorialMapScene(열려 있어야 함, Play 모드 아님)에 배치. zones/dungeon/shotgun_display.py import 뒤에 실행.
//   [Tutorial]/Props/ShotgunDisplay (36.5, 29)  ← 방 중앙
//     Dais     : 바닥 데칼 (GroundProps), 충돌 없음
//     Pedestal : SortingGroup(Characters, order 0) — DungeonStageScene의 Block · Wall 타일맵(Characters · Individual)과 같은 방식으로
//                Renderer2D의 Y축 정렬을 받아 플레이어와 앞뒤가 정해진다(별도 스크립트 없음). 기준점은 발판 중심 +0.5(타일 한 칸 중심과 같은 높이)
//       Pillar : 낮은 받침, 발판 BoxCollider2D 1.7 x 0.6, 가운데 원판 윗면(바닥 +9px)에 놓음
//       ShotgunPickup : (Props에서 옮겨 옴) 받침 홈 위(발판 중심 +12px), 루트 SpriteRenderer 제거, 트리거 반경 1.6
//         Shadow : CollapseShadow (접지 그림자), Gun : shotgun_15(손 없는 샷건, 받침 홈 위 4px) — FloatingDisplay가 ±1px 흔듦
//   CM_GunFocus 마커도 샷건 위치로. 이미 있으면 아무것도 하지 않는다.
var props = UnityEngine.GameObject.Find("[Tutorial]/Props");
if (props == null) return "no Props";
if (UnityEngine.Application.isPlaying) return "Play 모드에서는 씬에 저장되지 않는다 — 먼저 멈출 것";
var pickup = props.transform.Find("ShotgunPickup");
if (pickup == null) return "no pickup (이미 옮겨졌나?)";
if (props.transform.Find("ShotgunDisplay") != null) return "already exists";
UnityEngine.Sprite Load(string path, string name) { foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path)) { var s = o as UnityEngine.Sprite; if (s != null && s.name == name) return s; } return null; }
var dais = Load("Assets/04.Sprites/Tile/ZoneProps/TutorialShotgunDisplay/TutorialShotgunDisplay.png", "ShotgunDais");
var pillar = Load("Assets/04.Sprites/Tile/ZoneProps/TutorialShotgunDisplay/TutorialShotgunDisplay.png", "ShotgunPillar");
var gun = Load("Assets/04.Sprites/Weapon/shotgun.png", "shotgun_15");
var shadow = Load("Assets/04.Sprites/Tile/ZoneProps/TutorialCollapseDebris/TutorialCollapseDebris.png", "CollapseShadow");
if (dais == null || pillar == null || gun == null || shadow == null) return "sprite missing";
var mat = pickup.GetComponent<UnityEngine.SpriteRenderer>() != null ? pickup.GetComponent<UnityEngine.SpriteRenderer>().sharedMaterial : null;

var root = new UnityEngine.GameObject("ShotgunDisplay");
UnityEditor.Undo.RegisterCreatedObjectUndo(root, "Shotgun display");
root.transform.SetParent(props.transform, false);
root.transform.position = new UnityEngine.Vector3(36.5f, 29f, 0f);

UnityEngine.SpriteRenderer Make(string name, UnityEngine.Transform parent, UnityEngine.Sprite sp, string layer, int order, UnityEngine.Vector3 localPos) {
  var g = new UnityEngine.GameObject(name);
  g.transform.SetParent(parent, false);
  g.transform.localPosition = localPos;
  var sr = g.AddComponent<UnityEngine.SpriteRenderer>();
  sr.sprite = sp; sr.sortingLayerName = layer; sr.sortingOrder = order;
  if (mat != null) sr.sharedMaterial = mat;
  return sr;
}
Make("Dais", root.transform, dais, "GroundProps", 0, UnityEngine.Vector3.zero);

float lift = 9f / 16f;                                   // 가운데 원판 윗면은 바닥에서 9px 위
float footY = 29f + lift;                                // 기둥 발판 중심
float sortY = footY + 0.5f;                              // 정렬 기준점 (플레이어가 기둥 앞에 붙어도 앞으로 그려지게 한 칸 위)
var ped = new UnityEngine.GameObject("Pedestal");
ped.transform.SetParent(root.transform, false);
ped.transform.position = new UnityEngine.Vector3(36.5f, sortY, 0f);
var group = ped.AddComponent<UnityEngine.Rendering.SortingGroup>();
group.sortingLayerName = "Characters"; group.sortingOrder = 0;

var pillarSr = Make("Pillar", ped.transform, pillar, "Characters", 0, new UnityEngine.Vector3(0, footY - sortY, 0));
var col = pillarSr.gameObject.AddComponent<UnityEngine.BoxCollider2D>();
col.offset = UnityEngine.Vector2.zero;                   // AddComponent가 스프라이트 중심으로 맞춰 놓으므로 발판 중심으로 되돌림
col.size = new UnityEngine.Vector2(1.7f, 0.6f);

float cradleY = footY + 12f / 16f;                       // 샷건 상호작용 지점 = 받침 홈 가운데
UnityEditor.Undo.SetTransformParent(pickup, ped.transform, "pickup under pedestal");
pickup.position = new UnityEngine.Vector3(36.5f, cradleY, 0f);
var rootSr = pickup.GetComponent<UnityEngine.SpriteRenderer>();
if (rootSr != null) UnityEditor.Undo.DestroyObjectImmediate(rootSr);
var cc = pickup.GetComponent<UnityEngine.CircleCollider2D>();
UnityEditor.Undo.RecordObject(cc, "radius"); cc.radius = 1.6f;

var sh = Make("Shadow", pickup, shadow, "Characters", 1, UnityEngine.Vector3.zero);
sh.color = new UnityEngine.Color(1f, 1f, 1f, 0.7f);
var gunSr = Make("Gun", pickup, gun, "Characters", 2, new UnityEngine.Vector3(-4.3f / 16f, 9f / 16f, 0f));   // 샷건 바닥이 받침 홈 위 4px
var fd = pickup.gameObject.AddComponent<ShotgunAndRun.Sandbox.Tutorial.FloatingDisplay>();
var so = new UnityEditor.SerializedObject(fd);
so.FindProperty("_body").objectReferenceValue = gunSr.transform;
so.FindProperty("_shadow").objectReferenceValue = sh;
so.ApplyModifiedProperties();

foreach (var m in UnityEngine.Object.FindObjectsByType<ShotgunAndRun.Sandbox.Cutscene.CutsceneMarker>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None)) {
  if (m.name == "CM_GunFocus") { UnityEditor.Undo.RecordObject(m.transform, "marker"); m.transform.position = new UnityEngine.Vector3(36.5f, cradleY, 0f); }
}
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(props.scene);
return "ok footY=" + footY + " sortY=" + sortY + " cradleY=" + cradleY;
