// 읽기 전용: DungeonStageScene을 additive로 열어 빛 위치 · 반경을 덤프하고 바로 닫는다
var path = "Assets/01.Scenes/DungeonStageScene.unity";
var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
var sb = new System.Text.StringBuilder();
try
{
    foreach (var e in UnityEngine.Object.FindObjectsByType<ShotgunAndRun.Environment.TileLightEmitter>(UnityEngine.FindObjectsSortMode.None))
        if (e.gameObject.scene == scene) e.Rebuild();
    foreach (var root in scene.GetRootGameObjects())
    {
        if (root.name == "Global Light 2D") { var g = root.GetComponent<UnityEngine.Rendering.Universal.Light2D>(); sb.AppendLine("GLOBAL " + g.intensity); }
        if (root.name != "__Lights") continue;
        foreach (UnityEngine.Transform grp in root.transform)
            foreach (var l in grp.GetComponentsInChildren<UnityEngine.Rendering.Universal.Light2D>(true))
            {
                if (l.blendStyleIndex != 0) continue;
                var sp = l.lightCookieSprite;
                float r = sp != null ? sp.bounds.extents.x * l.transform.lossyScale.x : -1;
                var p = l.transform.position;
                sb.AppendLine(grp.name + "," + p.x.ToString("F2") + "," + p.y.ToString("F2") + "," + r.ToString("F2") + "," + l.intensity.ToString("F2"));
            }
    }
}
finally { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
System.IO.File.WriteAllText("Backups/lights_dump.txt", sb.ToString());
return sb.ToString().Split('\n').Length + " lines; active scene " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
