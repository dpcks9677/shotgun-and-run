// 튜토리얼 벽 안내 표시기 설치 (eval_file로 실행. 먼저 keycaps_import.cs를 돌려 키캡 스프라이트 에셋이 있어야 한다)
// 1) 월드용 글자 머티리얼(Mulmaru SDF 복제 + 남색 외곽선) 2) 프리팹 TutorialWallText(글자만) · TutorialWallGuide(변형, TutorialWallGuide 컴포넌트)
// 3) TutorialScene에 상시 문구(이동 · 대시)와 흐름이 켜고 끄는 안내(Pickup · Loot · Player) 배치
// 4) 쓰지 않는 Zone_Move · Zone_Dash 삭제, TutorialPopup 프리팹에서 안내용 Panel 제거, TutorialFlow의 저장된 안내 문구를 코드 기본값으로 맞춤
// 이미 있는 프리팹 · 오브젝트는 건드리지 않고 넘어가므로 다시 돌려도 중복되지 않는다. 씬이 수정된 채면(isDirty) 시작하지 않는다.
var sb = new System.Text.StringBuilder();
const string dir = "Assets/03.Prefabs/Tutorial";
const string fontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Mulmaru SDF.asset";
const string spritePath = "Assets/04.Sprites/UI/TutorialKeycaps SpriteAsset.asset";
const string matPath = dir + "/M_TutorialGuideText.mat";
const string textPrefabPath = dir + "/TutorialWallText.prefab";
const string guidePrefabPath = dir + "/TutorialWallGuide.prefab";
const string popupPrefabPath = dir + "/TutorialPopup.prefab";

var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (UnityEditor.EditorApplication.isPlaying) return "플레이 중이라 중단";
if (scene.name != "TutorialScene") return "활성 씬이 TutorialScene이 아니라 중단: " + scene.name;
if (scene.isDirty) return "TutorialScene이 저장되지 않은 상태라 중단";

var font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(fontPath);
var spriteAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_SpriteAsset>(spritePath);
if (font == null || spriteAsset == null) return "글꼴 또는 키캡 스프라이트 에셋이 없음 (keycaps_import.cs 먼저)";

// 1) 글자 머티리얼: 공용 Mulmaru 머티리얼은 건드리지 않고 복제해서 외곽선을 켠다
var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(matPath);
if (mat == null)
{
    mat = new UnityEngine.Material(font.material) { name = "M_TutorialGuideText" };
    mat.EnableKeyword("OUTLINE_ON");
    mat.SetFloat("_OutlineWidth", 0.25f);
    mat.SetColor("_OutlineColor", new UnityEngine.Color32(27, 32, 51, 255));
    UnityEditor.AssetDatabase.CreateAsset(mat, matPath);
    sb.Append("material 생성; ");
}

// 2) 프리팹
if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(textPrefabPath) == null)
{
    var root = new UnityEngine.GameObject("TutorialWallText");
    try
    {
        var textGo = new UnityEngine.GameObject("Text");
        textGo.transform.SetParent(root.transform, false);
        var tmp = textGo.AddComponent<TMPro.TextMeshPro>();
        tmp.font = font;
        tmp.fontSharedMaterial = mat;
        tmp.spriteAsset = spriteAsset;
        tmp.fontSize = 8f;
        tmp.color = UnityEngine.Color.white;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        tmp.richText = true;
        tmp.rectTransform.sizeDelta = new UnityEngine.Vector2(20f, 2f);
        var renderer = textGo.GetComponent<UnityEngine.MeshRenderer>();
        renderer.sortingLayerName = "Environment";
        renderer.sortingOrder = 10;
        UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, textPrefabPath);
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(root);
    }

    sb.Append("TutorialWallText 생성; ");
}

if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(guidePrefabPath) == null)
{
    var textPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(textPrefabPath);
    var inst = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(textPrefab);
    try
    {
        inst.name = "TutorialWallGuide";
        var guide = inst.AddComponent<ShotgunAndRun.Tutorial.TutorialWallGuide>();
        var so = new UnityEditor.SerializedObject(guide);
        so.FindProperty("_text").objectReferenceValue = inst.GetComponentInChildren<TMPro.TMP_Text>();
        so.ApplyModifiedPropertiesWithoutUndo();
        UnityEditor.PrefabUtility.SaveAsPrefabAsset(inst, guidePrefabPath);
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(inst);
    }

    sb.Append("TutorialWallGuide 생성; ");
}

var textAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(textPrefabPath);
var guideAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(guidePrefabPath);

// 3) 씬 배치
var tutorialRoot = UnityEngine.GameObject.Find("[Tutorial]");
if (tutorialRoot == null) return "[Tutorial] 루트를 찾지 못함";
var holder = tutorialRoot.transform.Find("WallGuides");
if (holder == null)
{
    var h = new UnityEngine.GameObject("WallGuides");
    UnityEditor.Undo.RegisterCreatedObjectUndo(h, "WallGuides");
    h.transform.SetParent(tutorialRoot.transform, false);
    holder = h.transform;
}

// 위치 기준: 복도 1(바닥 y 3~8) 위 북쪽 벽은 x 14~32, y 9 이상. 흐름 안내는 진열대 · 상자 위
var pickup = UnityEngine.GameObject.Find("ShotgunPickup");
var chest = UnityEngine.Object.FindFirstObjectByType<InventorySystem.LootChest>();
UnityEngine.Vector3 pickupPos = pickup != null ? pickup.transform.position + new UnityEngine.Vector3(0f, 3f, 0f) : new UnityEngine.Vector3(36.5f, 33.5f, 0f);
UnityEngine.Vector3 lootPos = chest != null ? chest.transform.position + new UnityEngine.Vector3(0f, 3f, 0f) : new UnityEngine.Vector3(62.5f, 33.5f, 0f);

System.Action<string, UnityEngine.GameObject, UnityEngine.Vector3, string, string> place = (name, asset, pos, text, key) =>
{
    if (holder.Find(name) != null) return;
    var go = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(asset, holder);
    UnityEditor.Undo.RegisterCreatedObjectUndo(go, name);
    go.name = name;
    go.transform.position = pos;
    var tmp = go.GetComponentInChildren<TMPro.TMP_Text>();
    if (text != null) tmp.text = text;
    if (key != null)
    {
        var so = new UnityEditor.SerializedObject(go.GetComponent<ShotgunAndRun.Tutorial.TutorialWallGuide>());
        so.FindProperty("_key").stringValue = key;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    sb.Append(name + " 배치; ");
};

place("Guide_Move", textAsset, new UnityEngine.Vector3(19f, 10.5f, 0f),
    "이동 : <sprite name=\"Key_W\"><sprite name=\"Key_A\"><sprite name=\"Key_S\"><sprite name=\"Key_D\">", null);
place("Guide_Dash", textAsset, new UnityEngine.Vector3(28f, 10.5f, 0f), "대시 : <sprite name=\"Mouse_R\">", null);
place("Guide_Pickup", guideAsset, pickupPos, null, "Pickup");
place("Guide_Loot", guideAsset, lootPos, null, "Loot");
place("Guide_Player", guideAsset, UnityEngine.Vector3.zero, null, "Player");

// 플레이어 머리 위 안내는 적 · 총알에 가려지지 않게 캐릭터보다 위 레이어에 둔다 (벽 고정 안내는 환경 레이어 그대로)
var playerGuide = holder.Find("Guide_Player");
if (playerGuide != null)
{
    var playerRenderer = playerGuide.GetComponentInChildren<UnityEngine.MeshRenderer>();
    playerRenderer.sortingLayerName = "Foreground";
    playerRenderer.sortingOrder = 10;
}

// 4) 쓰지 않는 구역 삭제
foreach (string zoneName in new[] { "Zone_Move", "Zone_Dash" })
{
    var zone = UnityEngine.GameObject.Find(zoneName);
    if (zone != null)
    {
        UnityEditor.Undo.DestroyObjectImmediate(zone);
        sb.Append(zoneName + " 삭제; ");
    }
}

// 5) TutorialFlow에 저장된 안내 문구를 코드 기본값으로 맞춘다 (코드 기본값만 바꿔서는 씬 저장 값이 남는다)
var flow = UnityEngine.Object.FindFirstObjectByType<ShotgunAndRun.Tutorial.TutorialFlow>();
if (flow == null) return "TutorialFlow를 찾지 못함";
var tmpGo = new UnityEngine.GameObject("__flowDefaults");
try
{
    var defaults = new UnityEditor.SerializedObject(tmpGo.AddComponent<ShotgunAndRun.Tutorial.TutorialFlow>());
    var target = new UnityEditor.SerializedObject(flow);
    foreach (string field in new[] { "_shootGuide", "_reloadGuide", "_chestOpenGuide", "_chestTakeGuide", "_healGuide", "_pickupGuide", "_arenaHint" })
    {
        target.FindProperty(field).stringValue = defaults.FindProperty(field).stringValue;
    }

    target.ApplyModifiedProperties();
}
finally
{
    UnityEngine.Object.DestroyImmediate(tmpGo);
}

sb.Append("문구 맞춤; ");

// 6) TutorialPopup 프리팹에서 안내용 Panel 제거 (암전만 남김)
var popupRoot = UnityEditor.PrefabUtility.LoadPrefabContents(popupPrefabPath);
try
{
    var panel = popupRoot.transform.Find("Panel");
    if (panel != null)
    {
        UnityEngine.Object.DestroyImmediate(panel.gameObject);
        UnityEditor.PrefabUtility.SaveAsPrefabAsset(popupRoot, popupPrefabPath);
        sb.Append("Popup Panel 제거; ");
    }
}
finally
{
    UnityEditor.PrefabUtility.UnloadPrefabContents(popupRoot);
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEditor.AssetDatabase.SaveAssets();
return sb.ToString();
