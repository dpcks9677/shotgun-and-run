// 수류탄 · 섬광탄 폭발 연출 세팅 (eval_file로 실행, 먼저 python vfx/grenade_vfx.py import + 스크립트 컴파일)
// 1) 시트 임포트 · 슬라이스 (PPU 16, Point, 무압축). 불덩이 피벗 = 바닥 중심, 나머지는 가운데. 먼지 시트는 메시 타입 FullRect
// 2) 머티리얼: M_GrenadeAdd(가산) · M_GrenadeDust(먼지 파티클 · 스프라이트 시트) · M_GrenadeSpark(불씨 · 잔광 파티클, 가산)
// 3) 프리팹 GrenadeExplosion(파편 R=2.5u) · FlashExplosion(섬광 R=4u): 프레임 레이어 + 파티클 + 임시 Light2D + GrenadeExplosionVFX
// 4) FragmentGrenade.prefab · FlashBang.prefab의 _vfx를 새 프리팹으로 교체
// 다시 돌려도 같은 경로의 프리팹을 덮어쓰므로 GUID가 유지된다 (_vfx 참조는 4단계가 매번 다시 건다).
var sb = new System.Text.StringBuilder();
const string gDir = "Assets/09.VFX/Consumable/Grenade";
const string fDir = "Assets/09.VFX/Consumable/Flash";
const float ppu = 16f;

System.Func<string, string, int, UnityEngine.Vector2, UnityEngine.Sprite[]> slice = (png, prefix, frame, pivot) =>
{
    UnityEditor.AssetDatabase.ImportAsset(png, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
    var ti = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(png);
    var tex = new UnityEngine.Texture2D(2, 2);
    UnityEngine.ImageConversion.LoadImage(tex, System.IO.File.ReadAllBytes(png));
    int count = frame > 0 ? tex.width / frame : 1;
    int fw = frame > 0 ? frame : tex.width;
    var rects = new System.Collections.Generic.List<UnityEditor.SpriteRect>();
    var names = new System.Collections.Generic.List<string>();
    for (int i = 0; i < count; i++)
    {
        var nm = prefix + "_" + i;
        names.Add(nm);
        rects.Add(new UnityEditor.SpriteRect { name = nm, rect = new UnityEngine.Rect(i * fw, 0, fw, tex.height), alignment = UnityEngine.SpriteAlignment.Custom, pivot = pivot, spriteID = UnityEditor.GUID.Generate() });
    }
    var f0 = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories(); f0.Init();
    var dp0 = f0.GetSpriteEditorDataProviderFromObject(ti); dp0.InitSpriteEditorDataProvider();
    var cur = dp0.GetSpriteRects();
    bool same = ti.spriteImportMode == UnityEditor.SpriteImportMode.Multiple && cur.Length == rects.Count && ti.spritePixelsPerUnit == ppu
        && ti.filterMode == UnityEngine.FilterMode.Point && ti.textureCompression == UnityEditor.TextureImporterCompression.Uncompressed;
    if (same) foreach (var r in cur) { int k = names.IndexOf(r.name); if (k < 0 || r.pivot != pivot || r.rect != rects[k].rect) { same = false; break; } }
    if (!same)
    {
        ti.textureType = UnityEditor.TextureImporterType.Sprite;
        ti.spriteImportMode = UnityEditor.SpriteImportMode.Multiple;
        ti.spritePixelsPerUnit = ppu;
        ti.filterMode = UnityEngine.FilterMode.Point;
        ti.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        var f = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories(); f.Init();
        var dp = f.GetSpriteEditorDataProviderFromObject(ti); dp.InitSpriteEditorDataProvider();
        dp.SetSpriteRects(rects.ToArray());
        var np = dp.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>();
        var pairs = new System.Collections.Generic.List<UnityEditor.SpriteNameFileIdPair>();
        foreach (var r in rects) pairs.Add(new UnityEditor.SpriteNameFileIdPair(r.name, r.spriteID));
        np.SetNameFileIdPairs(pairs);
        dp.Apply();
        ti.SaveAndReimport();
        sb.Append("sliced " + prefix + "; ");
    }
    var list = new System.Collections.Generic.List<UnityEngine.Sprite>();
    foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetRepresentationsAtPath(png)) if (o is UnityEngine.Sprite s) list.Add(s);
    // 이름 끝 번호 순 (문자열 순이면 _10이 _2보다 앞선다)
    list.Sort((a, b) => int.Parse(a.name.Substring(a.name.LastIndexOf('_') + 1)).CompareTo(int.Parse(b.name.Substring(b.name.LastIndexOf('_') + 1))));
    return list.ToArray();
};

var center = new UnityEngine.Vector2(0.5f, 0.5f);
var fireball = slice(gDir + "/GrenadeFireball.png", "GrenadeFireball", 96, new UnityEngine.Vector2(0.5f, 48f / 112f));   // 피벗 = 바닥 중심
var ring = slice(gDir + "/GrenadeShockRing.png", "GrenadeShockRing", 96, center);
var core = slice(gDir + "/GrenadeFlashCore.png", "GrenadeFlashCore", 0, center);
var dust = slice(gDir + "/GrenadeDust.png", "GrenadeDust", 48, center);
// 먼지는 ParticleSystem Sprites 모드로 쓴다. 메시 타입이 Tight면 투명 여백을 잘라낸 영역이 파티클 사각형(3×2u)에 꽉 차게 늘어나 '크기 단계'가 무시되고 구름이 납작하게 커진다 → FullRect
{
    var dustPath = gDir + "/GrenadeDust.png";
    var dustImporter = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(dustPath);
    var dustSettings = new UnityEditor.TextureImporterSettings();
    dustImporter.ReadTextureSettings(dustSettings);
    if (dustSettings.spriteMeshType != UnityEngine.SpriteMeshType.FullRect)
    {
        dustSettings.spriteMeshType = UnityEngine.SpriteMeshType.FullRect;
        dustImporter.SetTextureSettings(dustSettings);
        dustImporter.SaveAndReimport();
        sb.Append("dust FullRect; ");
    }
    dust = slice(dustPath, "GrenadeDust", 48, center);
}
var scorch = slice(gDir + "/GrenadeScorch.png", "GrenadeScorch", 0, center);
var spark = slice(gDir + "/GrenadeSpark.png", "GrenadeSpark", 0, center);
var flashRing = slice(fDir + "/FlashRing.png", "FlashRing", 144, center);
var flashCross = slice(fDir + "/FlashCross.png", "FlashCross", 144, center);

// ---- 머티리얼
System.Func<string, UnityEngine.Shader, UnityEngine.Texture2D, UnityEngine.Material> material = (path, shader, tex) =>
{
    var m = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(path);
    if (m == null) { m = new UnityEngine.Material(shader); UnityEditor.AssetDatabase.CreateAsset(m, path); }
    m.shader = shader;
    if (tex != null) m.SetTexture("_MainTex", tex);
    m.SetColor("_Color", UnityEngine.Color.white);
    UnityEditor.EditorUtility.SetDirty(m);
    return m;
};
var additive = UnityEngine.Shader.Find("ShotgunAndRun/Sprite Additive");
var spriteUnlit = UnityEngine.Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
var addMat = material(gDir + "/M_GrenadeAdd.mat", additive, null);
var sparkMat = material(gDir + "/M_GrenadeSpark.mat", additive, UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(gDir + "/GrenadeSpark.png"));
var dustMat = material(gDir + "/M_GrenadeDust.mat", spriteUnlit, UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(gDir + "/GrenadeDust.png"));
var unlitMat = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
UnityEditor.AssetDatabase.SaveAssets();

int grenadeLayer = UnityEngine.SortingLayer.NameToID("Grenade");
int bloodLayer = UnityEngine.SortingLayer.NameToID("Blood");

System.Func<UnityEngine.GameObject, string, UnityEngine.Vector3, UnityEngine.GameObject> child = (parent, nm, pos) =>
{
    var go = new UnityEngine.GameObject(nm);
    go.transform.SetParent(parent.transform, false);
    go.transform.localPosition = pos;
    return go;
};
System.Func<UnityEngine.GameObject, UnityEngine.Material, int, int, UnityEngine.SpriteRenderer> spriteLayer = (go, mat, layerId, order) =>
{
    var sr = go.AddComponent<UnityEngine.SpriteRenderer>();
    sr.sharedMaterial = mat;
    sr.sortingLayerID = layerId;
    sr.sortingOrder = order;
    sr.enabled = false;
    return sr;
};

// ---- 파티클 도우미
System.Func<UnityEngine.GameObject, UnityEngine.Material, int, UnityEngine.ParticleSystem> particles = (go, mat, order) =>
{
    var ps = go.AddComponent<UnityEngine.ParticleSystem>();
    ps.Stop(true, UnityEngine.ParticleSystemStopBehavior.StopEmittingAndClear);
    var r = go.GetComponent<UnityEngine.ParticleSystemRenderer>();
    r.sharedMaterial = mat;
    r.sortingLayerID = grenadeLayer;
    r.sortingOrder = order;
    r.renderMode = UnityEngine.ParticleSystemRenderMode.Billboard;
    var m = ps.main;
    m.loop = false;
    m.playOnAwake = true;
    m.scalingMode = UnityEngine.ParticleSystemScalingMode.Hierarchy;
    m.simulationSpace = UnityEngine.ParticleSystemSimulationSpace.Local;
    m.startRotation = 0f;
    var e = ps.emission; e.enabled = true; e.rateOverTime = 0f; e.SetBursts(new UnityEngine.ParticleSystem.Burst[0]);
    var sh = ps.shape; sh.enabled = true; sh.shapeType = UnityEngine.ParticleSystemShapeType.Circle; sh.arc = 360f;
    sh.rotation = UnityEngine.Vector3.zero;
    return ps;
};
System.Func<UnityEngine.Color, float, UnityEngine.Color, float, UnityEngine.Gradient> grad2 = (c0, a0, c1, a1) =>
{
    var g = new UnityEngine.Gradient();
    g.SetKeys(new[] { new UnityEngine.GradientColorKey(c0, 0f), new UnityEngine.GradientColorKey(c1, 1f) },
              new[] { new UnityEngine.GradientAlphaKey(a0, 0f), new UnityEngine.GradientAlphaKey(a1, 1f) });
    return g;
};

// 불씨(파편): 0~0.1초에 20개, 사방으로 튀며 감속, 위로 흩날림. 3×3px 점
System.Action<UnityEngine.GameObject, UnityEngine.Material> embers = (go, mat) =>
{
    var ps = particles(go, mat, 4);
    var m = ps.main;
    m.duration = 0.1f; m.startDelay = 0f;
    m.startLifetime = 0.65f;
    m.startSpeed = new UnityEngine.ParticleSystem.MinMaxCurve(5f, 8f);
    m.startSize = 3f / ppu;
    m.maxParticles = 32;
    var e = ps.emission; e.rateOverTime = 200f;
    var sh = ps.shape; sh.radius = 0.25f; sh.radiusThickness = 1f;
    var v = ps.velocityOverLifetime; v.enabled = true; v.space = UnityEngine.ParticleSystemSimulationSpace.Local;
    v.x = new UnityEngine.ParticleSystem.MinMaxCurve(0f, 0f); v.y = new UnityEngine.ParticleSystem.MinMaxCurve(0.6f, 1.2f); v.z = new UnityEngine.ParticleSystem.MinMaxCurve(0f, 0f);   // 세 축은 같은 모드여야 한다
    v.speedModifier = new UnityEngine.ParticleSystem.MinMaxCurve(1f, new UnityEngine.AnimationCurve(new UnityEngine.Keyframe(0f, 1f), new UnityEngine.Keyframe(1f, 0.25f)));
    var c = ps.colorOverLifetime; c.enabled = true;
    c.color = grad2(new UnityEngine.Color(1f, 0.75f, 0.27f), 1f, new UnityEngine.Color(0.9f, 0.35f, 0.12f), 0f);
};

// 흙먼지: 0.15초부터(불덩이가 식기 직전). 덩어리 안(원 · 납작)에서 생겨 위로 떠오르며 퍼진다. 크기는 스프라이트 4프레임 순서 재생(배율 X)
System.Action<UnityEngine.GameObject, int> dustPs = (go, variant) =>
{
    var ps = particles(go, dustMat, 5);
    var m = ps.main;
    m.duration = 0.25f; m.startDelay = 0.15f;
    m.startLifetime = new UnityEngine.ParticleSystem.MinMaxCurve(0.65f, 0.8f);
    m.startSpeed = new UnityEngine.ParticleSystem.MinMaxCurve(0.2f, 0.35f);
    m.startSize3D = true; m.startSizeX = 48f / ppu; m.startSizeY = 32f / ppu; m.startSizeZ = 1f;
    m.maxParticles = 8;
    var e = ps.emission; e.rateOverTime = 16f;
    var sh = ps.shape; sh.radius = 1.25f; sh.radiusThickness = 1f; sh.scale = new UnityEngine.Vector3(1f, 0.55f, 1f); sh.position = new UnityEngine.Vector3(0f, 0.3f, 0f);
    var v = ps.velocityOverLifetime; v.enabled = true; v.space = UnityEngine.ParticleSystemSimulationSpace.Local;
    v.x = new UnityEngine.ParticleSystem.MinMaxCurve(0f, 0f); v.y = new UnityEngine.ParticleSystem.MinMaxCurve(0.4f, 0.8f); v.z = new UnityEngine.ParticleSystem.MinMaxCurve(0f, 0f);
    var t = ps.textureSheetAnimation; t.enabled = true;
    t.mode = UnityEngine.ParticleSystemAnimationMode.Sprites;
    t.timeMode = UnityEngine.ParticleSystemAnimationTimeMode.Lifetime;
    for (int i = t.spriteCount - 1; i >= 0; i--) t.RemoveSprite(i);
    for (int i = 0; i < 4; i++) t.AddSprite(dust[variant * 4 + i]);
    t.frameOverTime = new UnityEngine.ParticleSystem.MinMaxCurve(1f, new UnityEngine.AnimationCurve(new UnityEngine.Keyframe(0f, 0f), new UnityEngine.Keyframe(0.9f, 1f), new UnityEngine.Keyframe(1f, 1f)));
    var c = ps.colorOverLifetime; c.enabled = true;
    var g = new UnityEngine.Gradient();
    g.SetKeys(new[] { new UnityEngine.GradientColorKey(new UnityEngine.Color(1f, 0.6f, 0.45f), 0f), new UnityEngine.GradientColorKey(UnityEngine.Color.white, 0.4f), new UnityEngine.GradientColorKey(UnityEngine.Color.white, 1f) },
              new[] { new UnityEngine.GradientAlphaKey(0f, 0f), new UnityEngine.GradientAlphaKey(0.5f, 0.15f), new UnityEngine.GradientAlphaKey(0.3f, 0.6f), new UnityEngine.GradientAlphaKey(0f, 1f) });
    c.color = g;
};

// 섬광 잔광: 0.1~0.25초에 12개, 노랑 점이 느리게 흩날리다 사라짐
System.Action<UnityEngine.GameObject, UnityEngine.Material> flashSparks = (go, mat) =>
{
    var ps = particles(go, mat, 4);
    var m = ps.main;
    m.duration = 0.15f; m.startDelay = 0.1f;
    m.startLifetime = 0.5f;
    m.startSpeed = new UnityEngine.ParticleSystem.MinMaxCurve(0.4f, 0.8f);
    m.startSize = 3f / ppu;
    m.maxParticles = 16;
    var e = ps.emission; e.rateOverTime = 80f;
    var sh = ps.shape; sh.radius = 3.6f; sh.radiusThickness = 0.8f;
    var c = ps.colorOverLifetime; c.enabled = true;
    c.color = grad2(new UnityEngine.Color(1f, 0.88f, 0.47f), 0.9f, new UnityEngine.Color(1f, 0.8f, 0.3f), 0f);
};

// ---- 필드 설정
System.Action<UnityEngine.Object, string, object> setField = (obj, field, value) =>
{
    var so = new UnityEditor.SerializedObject(obj);
    var p = so.FindProperty(field);
    if (p == null) throw new System.Exception("no field " + field + " on " + obj);
    if (value is UnityEngine.Object o) p.objectReferenceValue = o;
    else if (value is float fl) p.floatValue = fl;
    so.ApplyModifiedPropertiesWithoutUndo();
};
// 프레임 레이어 한 줄
System.Action<UnityEditor.SerializedProperty, UnityEngine.SpriteRenderer, UnityEngine.Sprite[], float, float, float, float, bool> layerRow = (el, sr, frames, fps, delay, hold, duration, fade) =>
{
    el.FindPropertyRelative("Renderer").objectReferenceValue = sr;
    var fr = el.FindPropertyRelative("Frames");
    fr.arraySize = frames.Length;
    for (int i = 0; i < frames.Length; i++) fr.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
    el.FindPropertyRelative("FramesPerSecond").floatValue = fps;
    el.FindPropertyRelative("Delay").floatValue = delay;
    el.FindPropertyRelative("Hold").floatValue = hold;
    el.FindPropertyRelative("Duration").floatValue = duration;
    el.FindPropertyRelative("FadeOut").boolValue = fade;
};
System.Func<UnityEngine.GameObject, UnityEngine.Color, float, UnityEngine.Rendering.Universal.Light2D> addLight = (root, color, radius) =>
{
    var go = new UnityEngine.GameObject("Light");
    go.SetActive(false);                 // lightType 설정자는 바꾸기 전 종류로 메시를 만든다 → 꺼 둔 채 설정 (TileLightEmitter와 같은 이유)
    go.transform.SetParent(root.transform, false);
    var l = go.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
    l.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
    l.blendStyleIndex = 0;               // Multiply — 바닥 타일(Lit)을 밝힌다
    l.color = color;
    l.pointLightOuterRadius = radius;
    l.pointLightInnerRadius = 0f;
    l.shadowsEnabled = false;
    l.volumeIntensityEnabled = false;
    go.SetActive(true);
    return l;
};

// ---- 파편 수류탄 폭발: GrenadeExplosion (R = 2.5u)
UnityEngine.GameObject fragRoot = new UnityEngine.GameObject("GrenadeExplosion");
UnityEngine.GameObject flashRoot = new UnityEngine.GameObject("FlashExplosion");
UnityEngine.GameObject fragPrefab = null, flashPrefab = null;
try
{
    var vfx = fragRoot.AddComponent<Consumable.GrenadeExplosionVFX>();
    var scorchSr = spriteLayer(child(fragRoot, "Scorch", UnityEngine.Vector3.zero), unlitMat, bloodLayer, 0);
    var ringSr = spriteLayer(child(fragRoot, "ShockRing", UnityEngine.Vector3.zero), addMat, bloodLayer, 1);
    var fireSr = spriteLayer(child(fragRoot, "Fireball", UnityEngine.Vector3.zero), unlitMat, grenadeLayer, 2);
    var flashSr = spriteLayer(child(fragRoot, "Flash", new UnityEngine.Vector3(0f, 0.5f, 0f)), addMat, grenadeLayer, 3);
    embers(child(fragRoot, "Embers", UnityEngine.Vector3.zero), sparkMat);
    for (int i = 0; i < 3; i++) dustPs(child(fragRoot, "Dust_" + (char)('A' + i), UnityEngine.Vector3.zero), i);
    var light = addLight(fragRoot, new UnityEngine.Color(1f, 140f / 255f, 40f / 255f), 3.75f);

    var so = new UnityEditor.SerializedObject(vfx);
    so.FindProperty("_referenceRadius").floatValue = 2.5f;
    so.FindProperty("_lifetime").floatValue = 8.35f;
    var layers = so.FindProperty("_layers");
    layers.arraySize = 4;
    layerRow(layers.GetArrayElementAtIndex(0), scorchSr, scorch, 1f, 0.05f, 0f, 8.25f, true);          // 그을음: 8초 페이드
    layerRow(layers.GetArrayElementAtIndex(1), ringSr, ring, 1f / 0.06f, 0.05f, 0f, 0.30f, false);       // 바닥 충격 링 0.05~0.35초
    layerRow(layers.GetArrayElementAtIndex(2), fireSr, fireball, 30f, 0f, 0f, 14f / 30f, false);         // 불덩이 14프레임 @30fps
    layerRow(layers.GetArrayElementAtIndex(3), flashSr, core, 1f, 0f, 0f, 0.09f, true);                  // 첫 순간 번쩍
    so.FindProperty("_light").objectReferenceValue = light;
    so.FindProperty("_lightIntensity").floatValue = 1.4f;
    so.FindProperty("_lightDuration").floatValue = 0.45f;
    so.ApplyModifiedPropertiesWithoutUndo();

    fragPrefab = UnityEditor.PrefabUtility.SaveAsPrefabAsset(fragRoot, gDir + "/GrenadeExplosion.prefab");

    // ---- 섬광탄 폭발: FlashExplosion (R = 4u)
    var fvfx = flashRoot.AddComponent<Consumable.GrenadeExplosionVFX>();
    var fRingSr = spriteLayer(child(flashRoot, "Ring", UnityEngine.Vector3.zero), addMat, grenadeLayer, 1);
    var fCrossSr = spriteLayer(child(flashRoot, "Cross", UnityEngine.Vector3.zero), addMat, grenadeLayer, 3);
    flashSparks(child(flashRoot, "Sparks", UnityEngine.Vector3.zero), sparkMat);
    var fLight = addLight(flashRoot, new UnityEngine.Color(1f, 225f / 255f, 120f / 255f), 4.8f);

    var fso = new UnityEditor.SerializedObject(fvfx);
    fso.FindProperty("_referenceRadius").floatValue = 4f;
    fso.FindProperty("_lifetime").floatValue = 0.75f;
    var fl = fso.FindProperty("_layers");
    fl.arraySize = 2;
    layerRow(fl.GetArrayElementAtIndex(0), fRingSr, flashRing, 24f, 0f, 0f, 0.40f, false);              // 도넛 8프레임 @24fps, 마지막 프레임 0.4초까지 유지
    layerRow(fl.GetArrayElementAtIndex(1), fCrossSr, flashCross, 20f, 0f, 0.05f, 0.40f, false);         // 십자 7프레임: 첫 프레임 0.1초 유지 후 20fps
    fso.FindProperty("_light").objectReferenceValue = fLight;
    fso.FindProperty("_lightIntensity").floatValue = 1.0f;
    fso.FindProperty("_lightDuration").floatValue = 0.35f;
    fso.ApplyModifiedPropertiesWithoutUndo();

    flashPrefab = UnityEditor.PrefabUtility.SaveAsPrefabAsset(flashRoot, fDir + "/FlashExplosion.prefab");
}
finally
{
    UnityEngine.Object.DestroyImmediate(fragRoot);
    UnityEngine.Object.DestroyImmediate(flashRoot);
}

// ---- 수류탄 프리팹의 _vfx 교체
System.Action<string, string, UnityEngine.GameObject> assign = (prefabPath, typeName, vfxPrefab) =>
{
    var root = UnityEditor.PrefabUtility.LoadPrefabContents(prefabPath);
    try
    {
        UnityEngine.MonoBehaviour target = null;
        foreach (var mb in root.GetComponents<UnityEngine.MonoBehaviour>()) if (mb != null && mb.GetType().Name == typeName) target = mb;
        if (target == null) throw new System.Exception("no " + typeName + " on " + prefabPath);
        var so = new UnityEditor.SerializedObject(target);
        so.FindProperty("_vfx").objectReferenceValue = vfxPrefab;
        so.ApplyModifiedPropertiesWithoutUndo();
        UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
    }
    finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
};
assign("Assets/03.Prefabs/Grenade/FragmentGrenade.prefab", "FragmentGrenade", fragPrefab);
assign("Assets/03.Prefabs/Grenade/FlashBang.prefab", "FlashBang", flashPrefab);

UnityEditor.AssetDatabase.SaveAssets();
sb.Append("fireball " + fireball.Length + " ring " + ring.Length + " dust " + dust.Length + " flashRing " + flashRing.Length + " cross " + flashCross.Length + "; ");
sb.Append("frag " + UnityEditor.AssetDatabase.AssetPathToGUID(gDir + "/GrenadeExplosion.prefab"));
sb.Append(" flash " + UnityEditor.AssetDatabase.AssetPathToGUID(fDir + "/FlashExplosion.prefab"));
return sb.ToString();
