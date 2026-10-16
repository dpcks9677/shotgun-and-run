// 추격 낙하 수치 조정 (2026-10-08 사용자: 플레이어와의 거리 2칸, 큰 더미 4개). TutorialMapScene을 열고 Play 아님 상태에서 실행.
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.path != "Assets/_Sandbox/Yechann/TutorialMapScene.unity") return "활성 씬이 TutorialMapScene이 아님: " + scene.path;
if (UnityEngine.Application.isPlaying) return "Play 모드에서는 실행하지 않음";
var fxGo = UnityEngine.GameObject.Find("/[Tutorial]/Props/ShakeFx");
if (fxGo == null) return "ShakeFx 없음";
var fx = fxGo.GetComponent<ShotgunAndRun.Sandbox.Cutscene.CutsceneShakeDebris>();
var so = new UnityEditor.SerializedObject(fx);
so.FindProperty("_chaseCount").intValue = 4;
so.FindProperty("_chaseGap").floatValue = 2f;
so.FindProperty("_chaseInterval").floatValue = 0.4f;
so.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.EditorUtility.SetDirty(fx);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
return "추격 낙하: 개수 4, 거리 2칸, 간격 0.4초";
