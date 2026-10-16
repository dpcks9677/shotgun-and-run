// 복도를 막는 큰 잔해 더미가 진동과 함께 "떨어져" 길을 막게 함 (2026-10-08 사용자: 큰 잔해 = 진동과 함께 복도를 막는 잔해)
//  1) 출발 지점 바로 뒤에 같은 더미(CollapseDebrisStart, 월드 (6, 31.6))를 복제: 시작할 때 꺼 둠, 첫 흔들림 때 카메라에 보이면 떨어져 막는다.
//  2) 복도 입구 더미(CollapseDebris)는 컷씬 신호가 SetActive로 바로 켜던 것을 ShakeFx.DropEntrancePlug 호출로 바꿔 떨어뜨림.
//  선행: tutorial_collapse_debris_setup.cs(A · B 더미 교체), tutorial_shake_fx_setup.cs(ShakeFx). TutorialMapScene을 열고 Play 아님 상태에서 실행.
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.path != "Assets/_Sandbox/Yechann/TutorialMapScene.unity") return "활성 씬이 TutorialMapScene이 아님: " + scene.path;
if (UnityEngine.Application.isPlaying) return "Play 모드에서는 실행하지 않음";
var props = UnityEngine.GameObject.Find("/[Tutorial]/Props");
var fxGo = UnityEngine.GameObject.Find("/[Tutorial]/Props/ShakeFx");
if (props == null || fxGo == null) return "Props/ShakeFx 없음";
var fx = fxGo.GetComponent<ShotgunAndRun.Sandbox.Cutscene.CutsceneShakeDebris>();
var entrance = props.transform.Find("CollapseDebris");
if (entrance == null) return "CollapseDebris 없음";
if (entrance.Find("Debris_A") == null) return "CollapseDebris에 Debris_A 없음 (tutorial_collapse_debris_setup.cs 먼저)";

var oldStart = props.transform.Find("CollapseDebrisStart");
if (oldStart != null) UnityEditor.Undo.DestroyObjectImmediate(oldStart.gameObject);
var start = UnityEngine.Object.Instantiate(entrance.gameObject, props.transform);
UnityEditor.Undo.RegisterCreatedObjectUndo(start, "CollapseDebrisStart");
start.name = "CollapseDebrisStart";
start.SetActive(false);
start.transform.position = new UnityEngine.Vector3(6f, 31.6f, 0f);
var bInStart = start.transform.Find("Debris_B");
if (bInStart != null) bInStart.localPosition = new UnityEngine.Vector3(0f, 1.0f, 0f);   // 뒤쪽 벽에 기댄 깊이감

var so = new UnityEditor.SerializedObject(fx);
so.FindProperty("_startPlug").objectReferenceValue = start;
so.FindProperty("_entrancePlug").objectReferenceValue = entrance.gameObject;
so.ApplyModifiedPropertiesWithoutUndo();

// 신호 재연결: 옛 SetActive(true) 호출 → DropEntrancePlug
int rewired = 0;
foreach (var sig in UnityEngine.Object.FindObjectsByType<ShotgunAndRun.Sandbox.Cutscene.CutsceneSignal>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
{
    if (sig.Key != "CollapseDebris") continue;
    var sso = new UnityEditor.SerializedObject(sig);
    var calls = sso.FindProperty("_onRaised.m_PersistentCalls.m_Calls");
    calls.ClearArray();
    calls.arraySize = 1;
    var c = calls.GetArrayElementAtIndex(0);
    c.FindPropertyRelative("m_Target").objectReferenceValue = fx;
    c.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = "ShotgunAndRun.Sandbox.Cutscene.CutsceneShakeDebris, Assembly-CSharp";
    c.FindPropertyRelative("m_MethodName").stringValue = "DropEntrancePlug";
    c.FindPropertyRelative("m_Mode").intValue = 1;
    c.FindPropertyRelative("m_CallState").intValue = 2;
    sso.ApplyModifiedProperties();
    UnityEditor.EditorUtility.SetDirty(sig);
    rewired++;
}
UnityEditor.EditorUtility.SetDirty(fx);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
return "CollapseDebrisStart 생성(비활성), 신호 재연결 " + rewired + "개, 입구 active=" + entrance.gameObject.activeSelf;
