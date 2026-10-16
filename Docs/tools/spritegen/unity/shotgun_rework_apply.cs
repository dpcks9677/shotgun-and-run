// 샷건 리워크 적용 (eval_file로 실행, 먼저 python weapon/shotgun_rework.py --final)
// 1) 생성한 시트(Docs/drafts/props/shotgun-rework-sheet.png)를 shotgun.png에 덮어쓰기 — 기존 슬라이스 ID는 그대로 둔다
// 2) 새 슬라이스 shotgun_16~19 추가 (반동 절반 · 오버슈트 숙임 · 펌프 깊이 당김 · 펌프 앞으로 튐), 피벗은 기존 칸과 같은 (0.3, 0.5)
// 3) Fire · PumpAction 클립을 튀는 버전 키로 다시 씀 (20fps, Fire 0.40초 + PumpAction 0.35초 ≤ 기본 연사 간격 0.8초)
var sb = new System.Text.StringBuilder();
const string png = "Assets/04.Sprites/Weapon/shotgun.png";
const string anims = "Assets/05.Animations/Shotgun";
const float fps = 20f;

System.IO.File.Copy("Docs/drafts/props/shotgun-rework-sheet.png", png, true);
UnityEditor.AssetDatabase.ImportAsset(png, UnityEditor.ImportAssetOptions.ForceSynchronousImport);

var newRects = new System.Collections.Generic.Dictionary<string, UnityEngine.Rect>
{
    { "shotgun_16", new UnityEngine.Rect(128, 96, 64, 32) },
    { "shotgun_17", new UnityEngine.Rect(192, 96, 64, 32) },
    { "shotgun_18", new UnityEngine.Rect(256, 96, 64, 32) },
    { "shotgun_19", new UnityEngine.Rect(256, 0, 64, 32) },
};
var ti = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(png);
var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories(); factory.Init();
var dp = factory.GetSpriteEditorDataProviderFromObject(ti); dp.InitSpriteEditorDataProvider();
var rects = new System.Collections.Generic.List<UnityEditor.SpriteRect>(dp.GetSpriteRects());
var nameIds = dp.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>();
var pairs = new System.Collections.Generic.List<UnityEditor.SpriteNameFileIdPair>(nameIds.GetNameFileIdPairs());
int added = 0;
foreach (var kv in newRects)
{
    if (rects.Exists(r => r.name == kv.Key)) continue;
    var sr = new UnityEditor.SpriteRect { name = kv.Key, rect = kv.Value, alignment = UnityEngine.SpriteAlignment.Custom, pivot = new UnityEngine.Vector2(0.3f, 0.5f), spriteID = UnityEditor.GUID.Generate() };
    rects.Add(sr);
    pairs.Add(new UnityEditor.SpriteNameFileIdPair(sr.name, sr.spriteID));
    added++;
}
if (added > 0)
{
    dp.SetSpriteRects(rects.ToArray());
    nameIds.SetNameFileIdPairs(pairs);
    dp.Apply();
    ti.SaveAndReimport();
}
sb.Append("slices +" + added + " (total " + rects.Count + "); ");

var sprites = new System.Collections.Generic.Dictionary<string, UnityEngine.Sprite>();
foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetRepresentationsAtPath(png)) if (o is UnityEngine.Sprite s) sprites[s.name] = s;

System.Action<string, string[]> writeClip = (clipName, keys) =>
{
    var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AnimationClip>(anims + "/" + clipName + ".anim");
    var binding = UnityEditor.EditorCurveBinding.PPtrCurve("", typeof(UnityEngine.SpriteRenderer), "m_Sprite");
    // 스프라이트 클립은 마지막 키 뒤에 한 프레임(1/20초)이 자동으로 붙는다 → 클립 길이 = 키 수 / 20
    var frames = new UnityEditor.ObjectReferenceKeyframe[keys.Length];
    for (int i = 0; i < keys.Length; i++) frames[i] = new UnityEditor.ObjectReferenceKeyframe { time = i / fps, value = sprites[keys[i]] };
    clip.frameRate = fps;
    UnityEditor.AnimationUtility.SetObjectReferenceCurve(clip, binding, frames);
    UnityEditor.EditorUtility.SetDirty(clip);
    sb.Append(clipName + " " + clip.length.ToString("0.###") + "s; ");
};
writeClip("Fire", new[] { "shotgun_1", "shotgun_16", "shotgun_2", "shotgun_3", "shotgun_17", "shotgun_8", "shotgun_5", "shotgun_0" });
writeClip("PumpAction", new[] { "shotgun_6", "shotgun_7", "shotgun_18", "shotgun_18", "shotgun_19", "shotgun_9", "shotgun_10" });
UnityEditor.AssetDatabase.SaveAssets();
return sb.ToString();
