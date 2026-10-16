// 탄 발사 연출 오버레이 시트 임포트 (eval_file로 실행, 먼저 python ui/shell_fire.py import)
// 시트(78x76)를 PPU 16 · Point · 무압축 · 밉맵 끔으로 슬라이스한다: Fire_01..Fire_10, 각 36x13.
// 피벗은 (13/36, 0.5) — 오버레이 안에서 반전된 탄(18x9)이 놓인 자리(x 4, y 2)의 중심이라, 오버레이 피벗을 탄 중심에 두면 정확히 겹친다.
// 이미 있는 슬라이스는 이름으로 찾아 갱신한다(중복 생성 없음). 좌표 규칙은 shell_fire.py의 build_sheet와 같다(Unity y = 76 - y - 13).
var sb = new System.Text.StringBuilder();
const string png = "Assets/04.Sprites/UI/ShellFire.png";
const int sheetH = 76, w = 36, h = 13, count = 10;

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

var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories(); factory.Init();
var dp = factory.GetSpriteEditorDataProviderFromObject(ti); dp.InitSpriteEditorDataProvider();
var old = new System.Collections.Generic.List<UnityEditor.SpriteRect>(dp.GetSpriteRects());
var rects = new System.Collections.Generic.List<UnityEditor.SpriteRect>();

for (int i = 0; i < count; i++)
{
    string name = "Fire_" + (i + 1).ToString("00");
    var r = old.Find(o => o.name == name) ?? new UnityEditor.SpriteRect { name = name, spriteID = UnityEditor.GUID.Generate() };
    r.rect = new UnityEngine.Rect(1 + 38 * (i % 2), sheetH - (1 + 15 * (i / 2)) - h, w, h);
    r.alignment = UnityEngine.SpriteAlignment.Custom;
    r.pivot = new UnityEngine.Vector2(13f / 36f, 0.5f);
    rects.Add(r);
}

dp.SetSpriteRects(rects.ToArray());
var pairs = new System.Collections.Generic.List<UnityEditor.SpriteNameFileIdPair>();
foreach (var r in rects) pairs.Add(new UnityEditor.SpriteNameFileIdPair(r.name, r.spriteID));
dp.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(pairs);
dp.Apply();
ti.SaveAndReimport();
sb.Append("slices " + rects.Count);
return sb.ToString();
