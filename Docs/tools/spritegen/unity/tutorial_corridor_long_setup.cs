// 빨강 복도 연장(+10칸, 열린 뒤쪽 끝) 후속 씬 설정 (2026-10-08 사용자 지시)
//  - 선행: TutorialLayout의 Escape-Corridor 끝 y를 52로 늘려 타일 반영(복도 y12~52, 시작 뒤쪽은 막다른 벽 없이 이어짐)
//  - EscapeStart 마커를 (6.5, 40)으로: 플레이어는 복도 뒤쪽에서 달려 나옴 (달리기 약 2.9초 = 첫 흔들림 3초)
//  - ShakeFx/Chase: 추격 낙하 착지 지점 6개를 고정 좌표로 (복도 뒤편 → 입구 쪽 순서, 0.4초 간격).
//      계산: 플레이어 y(t) = 40 - 9t, 더미가 t_i = 0.4i에 떨어지기 시작해 약 0.58초 뒤 착지 → y_i = 40 - 9(0.4i + 0.58) + 2.5 = 37.3 - 3.6i. 예측 없이 표로 고정.
//  - ShakeFx/Points · Behind: 새 길이(y 40 부근)까지 지점 늘림 / 시작 뒤쪽으로 옮김
//  - 화면 덮개(ScreenFader)를 씬 시작부터 검게 (컷씬 1초 페이드인으로 이어받음)
// Play 아님 상태에서 TutorialMapScene을 열고 실행. 지점은 자식 Transform이라 이후 손으로 옮겨도 된다.
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.path != "Assets/_Sandbox/Yechann/TutorialMapScene.unity") return "활성 씬이 TutorialMapScene이 아님: " + scene.path;
if (UnityEngine.Application.isPlaying) return "Play 모드에서는 실행하지 않음";
var sb = new System.Text.StringBuilder();

// 1) 시작 마커
var startMarker = UnityEngine.GameObject.Find("/[Tutorial]/Markers/CM_EscapeStart");
if (startMarker == null) return "CM_EscapeStart 없음";
UnityEditor.Undo.RecordObject(startMarker.transform, "EscapeStart");
var before = startMarker.transform.position;
startMarker.transform.position = new UnityEngine.Vector3(6.5f, 40f, 0f);
sb.AppendLine("EscapeStart " + before + " -> " + startMarker.transform.position);

// 2) ShakeFx 지점
var fxGo = UnityEngine.GameObject.Find("/[Tutorial]/Props/ShakeFx");
if (fxGo == null) return "ShakeFx 없음";
var fx = fxGo.GetComponent<ShotgunAndRun.Sandbox.Cutscene.CutsceneShakeDebris>();
System.Action<string> remove = n => { var t = fxGo.transform.Find(n); if (t != null) UnityEditor.Undo.DestroyObjectImmediate(t.gameObject); };
remove("Chase"); remove("Points"); remove("Behind");
System.Func<string, float[][], string, UnityEngine.Transform> make = (n, pts, prefix) =>
{
    var root = new UnityEngine.GameObject(n);
    UnityEditor.Undo.RegisterCreatedObjectUndo(root, n);
    root.transform.SetParent(fxGo.transform, false);
    for (int i = 0; i < pts.Length; i++)
    {
        var g = new UnityEngine.GameObject(prefix + (i + 1).ToString("00"));
        g.transform.SetParent(root.transform, false);
        g.transform.position = new UnityEngine.Vector3(pts[i][0], pts[i][1], 0f);
    }
    return root.transform;
};
// 추격 낙하 착지 중심 (복도 중앙 x 6, 번갈아 ±0.25로 단조로움 방지)
var chase = new float[6][];
for (int i = 0; i < 6; i++) chase[i] = new[] { 6f + (i % 2 == 0 ? -0.25f : 0.25f), 37.3f - 3.6f * i };
var chaseRoot = make("Chase", chase, "C");
// 작은 돌 · 먼지 지점: 복도 전체 (y 17~40) + 방. 카메라에 보이는 지점만 쓰임
var points = new[] {
    new[]{4.2f, 40.0f}, new[]{7.8f, 39.2f}, new[]{5.0f, 37.0f}, new[]{8.0f, 36.0f}, new[]{4.4f, 34.0f}, new[]{7.2f, 33.0f},
    new[]{5.0f, 30.6f}, new[]{8.0f, 29.0f}, new[]{4.4f, 27.0f}, new[]{7.4f, 25.6f}, new[]{5.0f, 24.0f}, new[]{8.2f, 22.0f},
    new[]{4.4f, 20.0f}, new[]{7.2f, 18.6f}, new[]{8.2f, 17.4f},
    new[]{2.5f, 10.5f}, new[]{6.0f, 11.0f}, new[]{10.5f, 10.5f}, new[]{4.0f, 7.5f}, new[]{9.0f, 7.0f}, new[]{12.0f, 8.5f}, new[]{7.0f, 4.5f}
};
var pointsRoot = make("Points", points, "P");
// 시작 지점 바로 뒤쪽(EscapeStart y40 뒤 = 북쪽 y41~44): 큰 돌덩이(CollapseBig)가 떨어짐
var behind = new[] { new[]{4.4f, 41.2f}, new[]{6.6f, 41.9f}, new[]{8.5f, 41.4f}, new[]{5.4f, 42.9f}, new[]{7.6f, 43.0f}, new[]{6.3f, 43.6f} };
var behindRoot = make("Behind", behind, "B");
var so = new UnityEditor.SerializedObject(fx);
so.FindProperty("_chaseRoot").objectReferenceValue = chaseRoot;
so.FindProperty("_pointsRoot").objectReferenceValue = pointsRoot;
so.FindProperty("_behindRoot").objectReferenceValue = behindRoot;
so.FindProperty("_chaseFirstDelay").floatValue = 0f;
so.FindProperty("_chaseInterval").floatValue = 0.4f;
so.FindProperty("_heapDropHeight").floatValue = 11f;
so.FindProperty("_startPlug").objectReferenceValue = null;
so.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.EditorUtility.SetDirty(fx);
sb.AppendLine("ShakeFx: Chase " + chase.Length + " · Points " + points.Length + " · Behind " + behind.Length);

// 3) ScreenFader 씬 시작 검정
int faders = 0;
foreach (var f in UnityEngine.Object.FindObjectsByType<ShotgunAndRun.Sandbox.Cutscene.ScreenFader>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
{
    var fso = new UnityEditor.SerializedObject(f);
    fso.FindProperty("_startBlack").boolValue = true;
    fso.ApplyModifiedPropertiesWithoutUndo();
    UnityEditor.EditorUtility.SetDirty(f);
    faders++;
}
sb.AppendLine("ScreenFader 씬 시작 검정 " + faders + "개");
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
return sb.ToString();
