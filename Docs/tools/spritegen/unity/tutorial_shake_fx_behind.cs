// 이미 만든 ShakeFx에 "출발 구간 뒤쪽 큰 잔해"만 덧붙임 (손으로 옮긴 Points · Focus 지점은 건드리지 않음). TutorialMapScene을 열고 Play 아님 상태에서 실행.
const string sheet = "Assets/04.Sprites/Tile/ZoneProps/TutorialCollapseDebris/TutorialCollapseDebris.png";
UnityEditor.AssetDatabase.ImportAsset(sheet, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
var big = new UnityEngine.Sprite[3];
foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(sheet))
{
    var sp = o as UnityEngine.Sprite;
    if (sp == null) continue;
    for (int i = 0; i < 3; i++) if (sp.name == "CollapseBig_" + (i + 1)) big[i] = sp;
}
for (int i = 0; i < 3; i++) if (big[i] == null) return "CollapseBig_" + (i + 1) + " 스프라이트 없음";
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.path != "Assets/_Sandbox/Yechann/TutorialMapScene.unity") return "활성 씬이 TutorialMapScene이 아님: " + scene.path;
if (UnityEngine.Application.isPlaying) return "Play 모드에서는 실행하지 않음";
var root = UnityEngine.GameObject.Find("/[Tutorial]/Props/ShakeFx");
if (root == null) return "ShakeFx 없음 (tutorial_shake_fx_setup.cs 먼저)";
var fx = root.GetComponent<ShotgunAndRun.Sandbox.Cutscene.CutsceneShakeDebris>();
var old = root.transform.Find("Behind");
if (old != null) UnityEditor.Undo.DestroyObjectImmediate(old.gameObject);
var behindRoot = new UnityEngine.GameObject("Behind");
UnityEditor.Undo.RegisterCreatedObjectUndo(behindRoot, "Behind");
behindRoot.transform.SetParent(root.transform, false);
// 출발 지점 EscapeStart (6.5, 29) 바로 뒤(북쪽). 복도 바닥은 y33 근처에서 끝남.
float[][] beh = {
    new[]{4.4f, 30.2f}, new[]{6.6f, 30.9f}, new[]{8.5f, 30.4f}, new[]{5.4f, 31.9f}, new[]{7.6f, 32.0f}, new[]{6.3f, 32.6f}
};
for (int i = 0; i < beh.Length; i++)
{
    var g = new UnityEngine.GameObject("B" + (i + 1).ToString("00"));
    g.transform.SetParent(behindRoot.transform, false);
    g.transform.position = new UnityEngine.Vector3(beh[i][0], beh[i][1], 0f);
}
var so = new UnityEditor.SerializedObject(fx);
var arr = so.FindProperty("_bigRocks");
arr.arraySize = 3;
for (int i = 0; i < 3; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = big[i];
so.FindProperty("_behindRoot").objectReferenceValue = behindRoot.transform;
so.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.EditorUtility.SetDirty(fx);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
return "Behind 지점 " + beh.Length + "개 + 큰 잔해 3종 연결 (씬 저장 안 함)";
