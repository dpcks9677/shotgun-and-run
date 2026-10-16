// 씬 시작과 함께 복도 뒤편부터 큰 잔해 더미를 한 번에 하나씩 떨어뜨리는 추격 낙하 연결 (2026-10-08 사용자 지시:
//  "작은 걸 여러 개 떨구는 게 아니라 한 번에 큰 거 하나씩. 복도 막히는 것도 하나로만" → 충돌은 입구 더미 하나만)
//  - CutsceneShakeDebris에 큰 더미 스프라이트(CollapseDebris_A · B)와 추격 대상(Player)을 연결
//  - 통째로 떨어지던 CollapseDebrisStart가 남아 있으면 지움 (입구 더미 CollapseDebris는 그대로, 붕괴 신호가 떨어뜨림)
// 선행: python zones/dungeon/collapse_debris.py import, tutorial_shake_fx_setup.cs, tutorial_plugs_setup.cs. TutorialMapScene을 열고 Play 아님 상태에서 실행.
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
var props = UnityEngine.GameObject.Find("/[Tutorial]/Props");
var fxGo = UnityEngine.GameObject.Find("/[Tutorial]/Props/ShakeFx");
var player = UnityEngine.GameObject.Find("/Player");
if (props == null || fxGo == null || player == null) return "Props/ShakeFx/Player 없음";
var fx = fxGo.GetComponent<ShotgunAndRun.Sandbox.Cutscene.CutsceneShakeDebris>();
var so = new UnityEditor.SerializedObject(fx);
var arr = so.FindProperty("_chaseHeaps");
arr.arraySize = 2;
arr.GetArrayElementAtIndex(0).objectReferenceValue = a;
arr.GetArrayElementAtIndex(1).objectReferenceValue = b;
so.FindProperty("_chaseTarget").objectReferenceValue = player.transform;
so.FindProperty("_startPlug").objectReferenceValue = null;
so.ApplyModifiedPropertiesWithoutUndo();
var start = props.transform.Find("CollapseDebrisStart");
bool hadStart = start != null;
if (hadStart) UnityEditor.Undo.DestroyObjectImmediate(start.gameObject);
UnityEditor.EditorUtility.SetDirty(fx);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
return "큰 더미 A · B + 추격 대상 연결, CollapseDebrisStart 삭제=" + hadStart;
