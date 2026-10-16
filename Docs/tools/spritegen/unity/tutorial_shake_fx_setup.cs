// 튜토리얼 빨강 구간 카메라 흔들림 연출: 흔들릴 때마다 지점에 먼지 · 잔해 (2026-10-08 사용자 요청)
// 먼저: python zones/dungeon/collapse_debris.py import (시트 TutorialCollapseDebris: 돌 4 · 그림자 · 먼지 6프레임) + CutsceneShakeDebris.cs 컴파일.
// TutorialMapScene을 열어 둔 상태에서 실행. [Tutorial]/Props 아래에 ShakeFx를 만든다 (이미 있으면 지우고 다시 만듦 — 지점을 손으로 옮겼다면 먼저 백업).
//  - ShakeFx/Points : 복도 · 방 곳곳 (카메라에 보이는 지점만 쓰임)
//  - ShakeFx/Focus  : 복도 입구 주변 (큰 흔들림에서 먼지 뭉치 + 낙하 집중)
//  - ShakeFx/Behind : 출발 지점(EscapeStart 월드 (6.5, 29)) 바로 뒤쪽 y30~33. 이 지점이 카메라에 보일 때 흔들림마다 큰 잔해(CollapseBig_1~3)가 연달아 떨어짐
// 지점은 자식 Transform이다. 옮기거나 복제해서 늘리면 그대로 반영된다 (선택하면 기즈모로 표시).
var sb = new System.Text.StringBuilder();
const string sheet = "Assets/04.Sprites/Tile/ZoneProps/TutorialCollapseDebris/TutorialCollapseDebris.png";
UnityEditor.AssetDatabase.ImportAsset(sheet, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
var sprites = new System.Collections.Generic.Dictionary<string, UnityEngine.Sprite>();
foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(sheet))
{
    var sp = o as UnityEngine.Sprite;
    if (sp != null) sprites[sp.name] = sp;
}
string[] need = { "CollapseRock_1", "CollapseRock_2", "CollapseRock_3", "CollapseRock_4", "CollapseShadow",
                  "CollapseDust_0", "CollapseDust_1", "CollapseDust_2", "CollapseDust_3", "CollapseDust_4", "CollapseDust_5",
                  "CollapseBig_1", "CollapseBig_2", "CollapseBig_3" };
foreach (var n in need)
{
    if (!sprites.ContainsKey(n)) return "스프라이트 없음: " + n + " (시트 임포트 확인)";
}

var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.path != "Assets/_Sandbox/Yechann/TutorialMapScene.unity") return "활성 씬이 TutorialMapScene이 아님: " + scene.path;
if (UnityEngine.Application.isPlaying) return "Play 모드에서는 실행하지 않음";
var props = UnityEngine.GameObject.Find("/[Tutorial]/Props");
if (props == null) return "[Tutorial]/Props를 찾지 못함";

var old = props.transform.Find("ShakeFx");
if (old != null) UnityEditor.Undo.DestroyObjectImmediate(old.gameObject);

var root = new UnityEngine.GameObject("ShakeFx");
UnityEditor.Undo.RegisterCreatedObjectUndo(root, "ShakeFx");
root.transform.SetParent(props.transform, false);
var pointsRoot = new UnityEngine.GameObject("Points");
pointsRoot.transform.SetParent(root.transform, false);
var focusRoot = new UnityEngine.GameObject("Focus");
focusRoot.transform.SetParent(root.transform, false);
var behindRoot = new UnityEngine.GameObject("Behind");
behindRoot.transform.SetParent(root.transform, false);

// 월드 좌표: 빨강 방 x0~14 · y0~13, 복도 x3~9 · y12~30. 잔해 더미(CollapseDebris) 중심 (6, 15), 높이 13~17.
float[][] pts = {
    new[]{4.2f, 29.0f}, new[]{7.8f, 28.2f}, new[]{5.0f, 25.6f}, new[]{8.0f, 24.0f}, new[]{4.4f, 22.0f}, new[]{7.2f, 20.0f},
    new[]{5.2f, 18.6f}, new[]{8.2f, 17.4f},
    new[]{2.5f, 10.5f}, new[]{6.0f, 11.0f}, new[]{10.5f, 10.5f}, new[]{4.0f, 7.5f}, new[]{9.0f, 7.0f}, new[]{12.0f, 8.5f}, new[]{7.0f, 4.5f}
};
float[][] foc = {
    new[]{3.8f, 12.0f}, new[]{5.2f, 11.4f}, new[]{6.8f, 12.1f}, new[]{8.2f, 11.5f}, new[]{5.5f, 10.2f}, new[]{7.2f, 10.0f},
    new[]{4.6f, 18.0f}, new[]{6.2f, 18.6f}, new[]{7.8f, 18.0f}
};
for (int i = 0; i < pts.Length; i++)
{
    var g = new UnityEngine.GameObject("P" + (i + 1).ToString("00"));
    g.transform.SetParent(pointsRoot.transform, false);
    g.transform.position = new UnityEngine.Vector3(pts[i][0], pts[i][1], 0f);
}
for (int i = 0; i < foc.Length; i++)
{
    var g = new UnityEngine.GameObject("F" + (i + 1).ToString("00"));
    g.transform.SetParent(focusRoot.transform, false);
    g.transform.position = new UnityEngine.Vector3(foc[i][0], foc[i][1], 0f);
}

float[][] beh = {
    new[]{4.4f, 30.2f}, new[]{6.6f, 30.9f}, new[]{8.5f, 30.4f}, new[]{5.4f, 31.9f}, new[]{7.6f, 32.0f}, new[]{6.3f, 32.6f}
};
for (int i = 0; i < beh.Length; i++)
{
    var g = new UnityEngine.GameObject("B" + (i + 1).ToString("00"));
    g.transform.SetParent(behindRoot.transform, false);
    g.transform.position = new UnityEngine.Vector3(beh[i][0], beh[i][1], 0f);
}

var fx = root.AddComponent<ShotgunAndRun.Sandbox.Cutscene.CutsceneShakeDebris>();
var so = new UnityEditor.SerializedObject(fx);
var rocks = so.FindProperty("_rocks");
rocks.arraySize = 4;
for (int i = 0; i < 4; i++) rocks.GetArrayElementAtIndex(i).objectReferenceValue = sprites["CollapseRock_" + (i + 1)];
so.FindProperty("_shadow").objectReferenceValue = sprites["CollapseShadow"];
var dust = so.FindProperty("_dustFrames");
dust.arraySize = 6;
for (int i = 0; i < 6; i++) dust.GetArrayElementAtIndex(i).objectReferenceValue = sprites["CollapseDust_" + i];
var big = so.FindProperty("_bigRocks");
big.arraySize = 3;
for (int i = 0; i < 3; i++) big.GetArrayElementAtIndex(i).objectReferenceValue = sprites["CollapseBig_" + (i + 1)];
so.FindProperty("_behindRoot").objectReferenceValue = behindRoot.transform;
so.FindProperty("_pointsRoot").objectReferenceValue = pointsRoot.transform;
so.FindProperty("_focusRoot").objectReferenceValue = focusRoot.transform;
so.ApplyModifiedPropertiesWithoutUndo();

UnityEditor.EditorUtility.SetDirty(fx);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
sb.AppendLine("ShakeFx 생성: 지점 " + pts.Length + " + 입구 " + foc.Length + " + 뒤쪽 " + beh.Length + ", 돌 4 · 큰 잔해 3 · 그림자 1 · 먼지 6 연결. 씬은 저장하지 않음(dirty).");
return sb.ToString();
