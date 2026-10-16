// 총구 섬광 마력 리워크 프리팹 연결 (eval_file로 실행, 먼저 muzzle_magic_import.cs)
// 1) BulletTracer.prefab: LineRenderer · 탄 머리(Head) 제거 → 자식 Line(가산, 단계마다 색) · Smoke(어두운 단색) PixelLineRenderer 두 개
// 2) MuzzleFlash.prefab: 섬광 6프레임 · 크리티컬 6프레임, 20fps, 바닥 빛 0.3초, 위로 떠오르며 깜빡이는 불티(Embers) 파티클
// 다시 실행해도 같은 결과가 되도록 이미 있는 자식은 지우고 다시 만든다.
var sb = new System.Text.StringBuilder();
const string vfx = "Assets/09.VFX/Weapon/Pellet";
const float ppu = 16f;

var additive = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(vfx + "/M_PelletGlow.mat");
var unlit = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");

System.Func<string, UnityEngine.Sprite[]> sprites = png =>
{
    var list = new System.Collections.Generic.List<UnityEngine.Sprite>();
    foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetRepresentationsAtPath(png)) if (o is UnityEngine.Sprite s) list.Add(s);
    list.Sort((a, b) => a.name.Length != b.name.Length ? a.name.Length - b.name.Length : string.CompareOrdinal(a.name, b.name));
    return list.ToArray();
};
System.Func<UnityEngine.GameObject, string, UnityEngine.GameObject> freshChild = (parent, nm) =>
{
    var old = parent.transform.Find(nm);
    if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
    var go = new UnityEngine.GameObject(nm);
    go.transform.SetParent(parent.transform, false);
    return go;
};
System.Action<UnityEditor.SerializedProperty, UnityEngine.Object[]> setArray = (prop, items) =>
{
    prop.arraySize = items.Length;
    for (int i = 0; i < items.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
};

// ---- 1) BulletTracer
{
    const string path = "Assets/03.Prefabs/Pellet/BulletTracer.prefab";
    var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
    try
    {
        int layer = 0, order = 0;
        var lr = root.GetComponent<UnityEngine.LineRenderer>();
        if (lr != null) { layer = lr.sortingLayerID; order = lr.sortingOrder; UnityEngine.Object.DestroyImmediate(lr); }
        else { var keep = root.transform.Find("Line"); if (keep != null) { var k = keep.GetComponent<UnityEngine.SpriteRenderer>(); layer = k.sortingLayerID; order = k.sortingOrder - 1; } }
        var head = root.transform.Find("Head");
        if (head != null) UnityEngine.Object.DestroyImmediate(head.gameObject);

        System.Func<string, UnityEngine.Material, UnityEngine.Color, int, UnityEngine.Component> line = (nm, mat, color, ord) =>
        {
            var go = freshChild(root, nm);
            var sr = go.AddComponent<UnityEngine.SpriteRenderer>();
            sr.sharedMaterial = mat;
            sr.color = color;
            sr.sortingLayerID = layer;
            sr.sortingOrder = ord;
            return go.AddComponent(System.Type.GetType("ShotgunAndRun.Combat.PixelLineRenderer, ShotgunAndRun.Combat"));
        };
        // 연기는 선 아래, 선은 가산으로 스스로 빛난다
        var smoke = line("Smoke", unlit, new UnityEngine.Color32(14, 11, 18, 255), order);
        var main = line("Line", additive, UnityEngine.Color.white, order + 1);

        var tracer = root.GetComponent("BulletTracer");
        var so = new UnityEditor.SerializedObject(tracer);
        so.FindProperty("_line").objectReferenceValue = main;
        so.FindProperty("_smoke").objectReferenceValue = smoke;
        so.ApplyModifiedPropertiesWithoutUndo();
        UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
        sb.Append("tracer ok (layer " + UnityEngine.SortingLayer.IDToName(layer) + " " + order + "); ");
    }
    finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
}

// ---- 2) MuzzleFlash
{
    const string path = vfx + "/MuzzleFlash.prefab";
    var spark = sprites(vfx + "/MagicSpark.png");
    var sparkMat = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(vfx + "/M_MagicSpark.mat");
    if (sparkMat == null)
    {
        sparkMat = new UnityEngine.Material(additive.shader) { name = "M_MagicSpark" };
        UnityEditor.AssetDatabase.CreateAsset(sparkMat, vfx + "/M_MagicSpark.mat");
    }
    sparkMat.mainTexture = spark[0].texture;
    UnityEditor.EditorUtility.SetDirty(sparkMat);

    var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
    try
    {
        var flashRenderer = root.GetComponent<UnityEngine.SpriteRenderer>();
        var go = freshChild(root, "Embers");
        var ps = go.AddComponent<UnityEngine.ParticleSystem>();
        ps.Stop(true, UnityEngine.ParticleSystemStopBehavior.StopEmittingAndClear);
        var r = go.GetComponent<UnityEngine.ParticleSystemRenderer>();
        r.sharedMaterial = sparkMat;
        r.sortingLayerID = flashRenderer.sortingLayerID;
        r.sortingOrder = flashRenderer.sortingOrder - 1;
        r.renderMode = UnityEngine.ParticleSystemRenderMode.Billboard;

        // 계획서 5.2: 발사당 3~4개(코드에서 Emit), 0.4~0.7초, 총구 회전과 무관하게 화면 위로 떠오르며 좌우로 흔들림
        var m = ps.main;
        m.loop = false;
        m.playOnAwake = false;
        m.duration = 1f;
        m.simulationSpace = UnityEngine.ParticleSystemSimulationSpace.World;
        m.scalingMode = UnityEngine.ParticleSystemScalingMode.Hierarchy;
        m.startLifetime = new UnityEngine.ParticleSystem.MinMaxCurve(0.4f, 0.7f);
        m.startSpeed = 0f;
        m.startSize = 3f / ppu;
        m.startRotation = 0f;
        m.maxParticles = 64;
        var e = ps.emission; e.enabled = true; e.rateOverTime = 0f; e.SetBursts(new UnityEngine.ParticleSystem.Burst[0]);
        var sh = ps.shape; sh.enabled = true; sh.shapeType = UnityEngine.ParticleSystemShapeType.Circle; sh.radius = 2f / ppu; sh.radiusThickness = 1f;
        var v = ps.velocityOverLifetime; v.enabled = true; v.space = UnityEngine.ParticleSystemSimulationSpace.World;
        v.x = new UnityEngine.ParticleSystem.MinMaxCurve(0f, 0f); v.y = new UnityEngine.ParticleSystem.MinMaxCurve(0.6f, 1f); v.z = new UnityEngine.ParticleSystem.MinMaxCurve(0f, 0f);
        var n = ps.noise; n.enabled = true; n.strength = 0.15f; n.frequency = 2f; n.separateAxes = true;
        n.strengthX = 0.15f; n.strengthY = 0f; n.strengthZ = 0f;
        // FL_W → FL_Y → FL_O, 알파를 오르내려 2~3번 깜빡이게
        var c = ps.colorOverLifetime; c.enabled = true;
        var g = new UnityEngine.Gradient();
        g.SetKeys(
            new[] { new UnityEngine.GradientColorKey(new UnityEngine.Color32(255, 240, 160, 255), 0f), new UnityEngine.GradientColorKey(new UnityEngine.Color32(255, 195, 25, 255), 0.45f), new UnityEngine.GradientColorKey(new UnityEngine.Color32(229, 138, 0, 255), 1f) },
            new[] { new UnityEngine.GradientAlphaKey(1f, 0f), new UnityEngine.GradientAlphaKey(0.35f, 0.25f), new UnityEngine.GradientAlphaKey(1f, 0.45f), new UnityEngine.GradientAlphaKey(0.3f, 0.65f), new UnityEngine.GradientAlphaKey(0.8f, 0.8f), new UnityEngine.GradientAlphaKey(0f, 1f) });
        c.color = g;
        // 반짝 점 · 1px 점 중 하나를 무작위로
        var t = ps.textureSheetAnimation; t.enabled = true;
        t.mode = UnityEngine.ParticleSystemAnimationMode.Sprites;
        for (int i = t.spriteCount - 1; i >= 0; i--) t.RemoveSprite(i);
        foreach (var s in spark) t.AddSprite(s);
        t.frameOverTime = 0f;
        t.startFrame = new UnityEngine.ParticleSystem.MinMaxCurve(0f, spark.Length - 0.01f);

        var flash = root.GetComponent("MuzzleFlash");
        var so = new UnityEditor.SerializedObject(flash);
        setArray(so.FindProperty("_frames"), sprites(vfx + "/MuzzleFlash.png"));
        setArray(so.FindProperty("_criticalFrames"), sprites(vfx + "/MuzzleFlashCrit.png"));
        so.FindProperty("_framesPerSecond").floatValue = 20f;
        so.FindProperty("_glowDuration").floatValue = 0.3f;
        so.FindProperty("_embers").objectReferenceValue = ps;
        so.FindProperty("_criticalTint").colorValue = new UnityEngine.Color(1f, 0.85f, 0.6f, 1f);
        so.ApplyModifiedPropertiesWithoutUndo();
        UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
        sb.Append("muzzle ok (frames " + so.FindProperty("_frames").arraySize + " crit " + so.FindProperty("_criticalFrames").arraySize + ", spark " + spark.Length + "); ");
    }
    finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
}
UnityEditor.AssetDatabase.SaveAssets();
return sb.ToString();
