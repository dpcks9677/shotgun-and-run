// 총구 섬광 마력 리워크 임포트 (eval_file로 실행, 먼저 python vfx/magic_vfx.py --write)
// MuzzleFlash(6프레임) · MuzzleFlashCrit(6프레임) · MagicSpark(2프레임: 반짝 점 · 1px 점)를 PPU 16 · Point · 무압축으로 가로 슬라이스한다.
// 이미 있는 슬라이스는 이름으로 찾아 ID를 유지하고(프리팹 참조 보존) 칸만 맞추며, 모자란 칸만 새로 만든다.
var sb = new System.Text.StringBuilder();
const string vfx = "Assets/09.VFX/Weapon/Pellet";

System.Action<string, int, UnityEngine.Vector2> slice = (png, frame, pivot) =>
{
    UnityEditor.AssetDatabase.ImportAsset(png, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
    var ti = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(png);
    ti.textureType = UnityEditor.TextureImporterType.Sprite;
    ti.spriteImportMode = UnityEditor.SpriteImportMode.Multiple;
    ti.spritePixelsPerUnit = 16f;
    ti.filterMode = UnityEngine.FilterMode.Point;
    ti.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
    ti.mipmapEnabled = false;
    ti.alphaIsTransparency = true;
    ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;

    var tex = new UnityEngine.Texture2D(2, 2);
    UnityEngine.ImageConversion.LoadImage(tex, System.IO.File.ReadAllBytes(png));
    string prefix = System.IO.Path.GetFileNameWithoutExtension(png);
    var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories(); factory.Init();
    var dp = factory.GetSpriteEditorDataProviderFromObject(ti); dp.InitSpriteEditorDataProvider();
    var old = new System.Collections.Generic.List<UnityEditor.SpriteRect>(dp.GetSpriteRects());
    var rects = new System.Collections.Generic.List<UnityEditor.SpriteRect>();
    for (int i = 0; i < tex.width / frame; i++)
    {
        string name = prefix + "_" + i;
        var r = old.Find(o => o.name == name) ?? new UnityEditor.SpriteRect { name = name, spriteID = UnityEditor.GUID.Generate() };
        r.rect = new UnityEngine.Rect(i * frame, 0, frame, tex.height);
        r.alignment = UnityEngine.SpriteAlignment.Custom;
        r.pivot = pivot;
        rects.Add(r);
    }
    dp.SetSpriteRects(rects.ToArray());
    var pairs = new System.Collections.Generic.List<UnityEditor.SpriteNameFileIdPair>();
    foreach (var r in rects) pairs.Add(new UnityEditor.SpriteNameFileIdPair(r.name, r.spriteID));
    dp.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(pairs);
    dp.Apply();
    ti.SaveAndReimport();
    sb.Append(prefix + " " + rects.Count + "; ");
};

// 섬광은 총구(왼쪽 가운데)가 피벗, 불티는 가운데
slice(vfx + "/MuzzleFlash.png", 16, new UnityEngine.Vector2(0f, 0.5f));
slice(vfx + "/MuzzleFlashCrit.png", 16, new UnityEngine.Vector2(0f, 0.5f));
slice(vfx + "/MagicSpark.png", 3, new UnityEngine.Vector2(0.5f, 0.5f));
return sb.ToString();
