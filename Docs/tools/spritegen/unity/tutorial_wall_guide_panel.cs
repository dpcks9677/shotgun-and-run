// 튜토리얼 안내 배경 판 적용 (eval_file로 실행. 먼저 python ui/guide_panel.py, tutorial_wall_guide_setup.cs 다음에 돌린다)
// 1) TutorialGuidePanel.png를 Sprite Single · PPU 16 · Point · 9슬라이스(테두리 3px)로 임포트
// 2) TutorialWallText 프리팹의 글자 아래에 Panel(SpriteRenderer, Sliced)을 달고, TutorialWallGuide 프리팹의 _panel에 연결
// 3) 씬의 Guide_Move · Guide_Dash(글자만 있던 상시 문구)를 TutorialWallGuide(키 Move · Dash)로 바꾼다. 문구와 위치는 그대로.
//    탈출 컷씬이 끝난 뒤 TutorialFlow가 켠다.
// 이미 적용돼 있으면 건너뛰므로 다시 돌려도 안전하다. 씬이 수정된 채면(isDirty) 시작하지 않는다.
var sb = new System.Text.StringBuilder();
const string dir = "Assets/03.Prefabs/Tutorial";
const string pngPath = "Assets/04.Sprites/UI/TutorialGuidePanel.png";
const string textPrefabPath = dir + "/TutorialWallText.prefab";
const string guidePrefabPath = dir + "/TutorialWallGuide.prefab";

var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (UnityEditor.EditorApplication.isPlaying) return "플레이 중이라 중단";
if (scene.name != "TutorialScene") return "활성 씬이 TutorialScene이 아니라 중단: " + scene.name;
if (scene.isDirty) return "TutorialScene이 저장되지 않은 상태라 중단";

// 1) 스프라이트 임포트
UnityEditor.AssetDatabase.ImportAsset(pngPath, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
var ti = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(pngPath);
if (ti == null) return "패널 PNG가 없음 (python ui/guide_panel.py 먼저)";
ti.textureType = UnityEditor.TextureImporterType.Sprite;
ti.spriteImportMode = UnityEditor.SpriteImportMode.Single;
ti.spritePixelsPerUnit = 16f;
ti.filterMode = UnityEngine.FilterMode.Point;
ti.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
ti.mipmapEnabled = false;
ti.alphaIsTransparency = true;
ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
ti.spriteBorder = new UnityEngine.Vector4(3f, 3f, 3f, 3f);
ti.SaveAndReimport();
var panelSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(pngPath);
sb.Append("panel sprite; ");

// 2) 프리팹: 글자 아래에 판을 단다
var baseRoot = UnityEditor.PrefabUtility.LoadPrefabContents(textPrefabPath);
try
{
    var text = baseRoot.transform.Find("Text");
    if (text.Find("Panel") == null)
    {
        var panelGo = new UnityEngine.GameObject("Panel");
        panelGo.transform.SetParent(text, false);
        var sr = panelGo.AddComponent<UnityEngine.SpriteRenderer>();
        sr.sprite = panelSprite;
        sr.drawMode = UnityEngine.SpriteDrawMode.Sliced;
        sr.size = new UnityEngine.Vector2(4f, 1f);
        var main = text.GetComponent<UnityEngine.MeshRenderer>();
        sr.sortingLayerID = main.sortingLayerID;
        sr.sortingOrder = main.sortingOrder - 1;
        UnityEditor.PrefabUtility.SaveAsPrefabAsset(baseRoot, textPrefabPath);
        sb.Append("Panel 추가; ");
    }
}
finally
{
    UnityEditor.PrefabUtility.UnloadPrefabContents(baseRoot);
}

var guideRoot = UnityEditor.PrefabUtility.LoadPrefabContents(guidePrefabPath);
try
{
    var guide = guideRoot.GetComponent<ShotgunAndRun.Tutorial.TutorialWallGuide>();
    var so = new UnityEditor.SerializedObject(guide);
    var panelProp = so.FindProperty("_panel");
    if (panelProp.objectReferenceValue == null)
    {
        panelProp.objectReferenceValue = guideRoot.transform.Find("Text/Panel").GetComponent<UnityEngine.SpriteRenderer>();
        so.ApplyModifiedPropertiesWithoutUndo();
        UnityEditor.PrefabUtility.SaveAsPrefabAsset(guideRoot, guidePrefabPath);
        sb.Append("_panel 연결; ");
    }
}
finally
{
    UnityEditor.PrefabUtility.UnloadPrefabContents(guideRoot);
}

// 3) 씬: 글자만 있던 Guide_Move · Guide_Dash를 안내 오브젝트로 교체
var guideAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(guidePrefabPath);
var holder = UnityEngine.GameObject.Find("[Tutorial]").transform.Find("WallGuides");
foreach (var (name, key) in new[] { ("Guide_Move", "Move"), ("Guide_Dash", "Dash") })
{
    var old = holder.Find(name);
    if (old == null || old.GetComponent<ShotgunAndRun.Tutorial.TutorialWallGuide>() != null) continue;

    var pos = old.position;
    string message = old.GetComponentInChildren<TMPro.TMP_Text>().text;
    UnityEditor.Undo.DestroyObjectImmediate(old.gameObject);

    var go = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(guideAsset, holder);
    UnityEditor.Undo.RegisterCreatedObjectUndo(go, name);
    go.name = name;
    go.transform.position = pos;
    go.GetComponentInChildren<TMPro.TMP_Text>().text = message;
    var gso = new UnityEditor.SerializedObject(go.GetComponent<ShotgunAndRun.Tutorial.TutorialWallGuide>());
    gso.FindProperty("_key").stringValue = key;
    gso.ApplyModifiedPropertiesWithoutUndo();
    sb.Append(name + " 교체; ");
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEditor.AssetDatabase.SaveAssets();
return sb.ToString();
