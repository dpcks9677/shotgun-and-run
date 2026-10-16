// 크리티컬 탄환 시트 임포트 (eval_file로 실행, 먼저 python ui/crit_shell.py import)
// 시트(76x60)를 PPU 16 · Point · 무압축 · 밉맵 끔으로 슬라이스한다: Shell_Crit 18x9 + Spark_01..09 22x13. 피벗은 모두 가운데라 스파크를 탄환 위에 같은 중심으로 겹치면 된다.
// 이미 있는 슬라이스는 이름으로 찾아 갱신한다(중복 생성 없음). 좌표 규칙은 crit_shell.py의 build_sheet와 같다(Unity y = 60 - y - 높이).
var sb = new System.Text.StringBuilder();
const string png = "Assets/04.Sprites/UI/CritShell.png";
const int sheetH = 60;

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

void Add(string name, int x, int yTop, int w, int h)
{
    var r = old.Find(o => o.name == name) ?? new UnityEditor.SpriteRect { name = name, spriteID = UnityEditor.GUID.Generate() };
    r.rect = new UnityEngine.Rect(x, sheetH - yTop - h, w, h);
    r.alignment = UnityEngine.SpriteAlignment.Center;
    r.pivot = new UnityEngine.Vector2(0.5f, 0.5f);
    rects.Add(r);
}

Add("Shell_Crit", 1, 1, 18, 9);
for (int i = 0; i < 9; i++)
    Add("Spark_0" + (i + 1), 1 + 24 * (i % 3), 12 + 15 * (i / 3), 22, 13);

dp.SetSpriteRects(rects.ToArray());
var pairs = new System.Collections.Generic.List<UnityEditor.SpriteNameFileIdPair>();
foreach (var r in rects) pairs.Add(new UnityEditor.SpriteNameFileIdPair(r.name, r.spriteID));
dp.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(pairs);
dp.Apply();
ti.SaveAndReimport();
sb.Append("slices " + rects.Count);
return sb.ToString();
