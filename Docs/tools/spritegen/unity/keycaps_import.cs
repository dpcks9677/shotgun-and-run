// 튜토리얼 키캡 시트 임포트 + TMP 스프라이트 에셋 생성 (eval_file로 실행, 먼저 python ui/keycaps.py)
// 시트를 PPU 16 · Point · 무압축 · 밉맵 끔으로 12칸 슬라이스하고, TMP_SpriteAsset(<sprite name="Key_W">용)을 만든다.
// 이미 있는 슬라이스 · 에셋은 이름으로 찾아 갱신한다(중복 생성 없음). 좌표 규칙은 keycaps.py와 같다(x = 1 + 16*i, y = 1, 14x14).
var sb = new System.Text.StringBuilder();
const string png = "Assets/04.Sprites/UI/TutorialKeycaps.png";
const string assetPath = "Assets/04.Sprites/UI/TutorialKeycaps SpriteAsset.asset";
string[] names = { "Key_W", "Key_A", "Key_S", "Key_D", "Key_E", "Key_R", "Key_1", "Key_2", "Key_3", "Key_4", "Mouse_L", "Mouse_R" };
const int cell = 16, cap = 14;
const float glyphBearingY = 0.8f, spriteScale = 1.25f;   // 키캡이 글자보다 약간 커 보이게(레퍼런스)

// 1) 임포트 설정 + 슬라이스
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
for (int i = 0; i < names.Length; i++)
{
    string name = names[i];
    var r = old.Find(o => o.name == name) ?? new UnityEditor.SpriteRect { name = name, spriteID = UnityEditor.GUID.Generate() };
    r.rect = new UnityEngine.Rect(1 + cell * i, 1, cap, cap);
    r.alignment = UnityEngine.SpriteAlignment.Center;
    r.pivot = new UnityEngine.Vector2(0.5f, 0.5f);
    rects.Add(r);
}
dp.SetSpriteRects(rects.ToArray());
var pairs = new System.Collections.Generic.List<UnityEditor.SpriteNameFileIdPair>();
foreach (var r in rects) pairs.Add(new UnityEditor.SpriteNameFileIdPair(r.name, r.spriteID));
dp.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(pairs);
dp.Apply();
ti.SaveAndReimport();
sb.Append("slices " + rects.Count + "; ");

// 2) TMP 스프라이트 에셋 (없으면 새로 만들고 있으면 갱신)
var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(png);
var sprites = new System.Collections.Generic.Dictionary<string, UnityEngine.Sprite>();
foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(png))
    if (o is UnityEngine.Sprite sp) sprites[sp.name] = sp;

var sa = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_SpriteAsset>(assetPath);
bool created = sa == null;
if (created)
{
    sa = UnityEngine.ScriptableObject.CreateInstance<TMPro.TMP_SpriteAsset>();
    UnityEditor.AssetDatabase.CreateAsset(sa, assetPath);
}
sa.spriteSheet = tex;

// 머티리얼은 서브에셋 하나를 재사용한다
var mat = sa.material;
if (mat == null)
{
    mat = new UnityEngine.Material(UnityEngine.Shader.Find("TextMeshPro/Sprite")) { name = "TutorialKeycaps SpriteAsset Material" };
    UnityEditor.AssetDatabase.AddObjectToAsset(mat, sa);
    sa.material = mat;
}
mat.SetTexture(UnityEngine.Shader.PropertyToID("_MainTex"), tex);

// TMP 3.x는 구형 spriteInfoList가 아니라 글리프 표(spriteGlyphTable)와 문자 표(spriteCharacterTable)를 쓴다. 구형 목록만 채우면 모든 키가 첫 칸 그림으로 나온다
sa.spriteInfoList = new System.Collections.Generic.List<TMPro.TMP_Sprite>();
sa.spriteGlyphTable.Clear();
sa.spriteCharacterTable.Clear();
for (int i = 0; i < names.Length; i++)
{
    var rc = rects[i].rect;
    var glyph = new TMPro.TMP_SpriteGlyph
    {
        index = (uint)i,
        metrics = new UnityEngine.TextCore.GlyphMetrics(rc.width, rc.height, 0f, rc.height * glyphBearingY, rc.width + 1f),   // 베이스라인에 맞춤: 실제 글자와 어긋나면 glyphBearingY를 조정
        glyphRect = new UnityEngine.TextCore.GlyphRect((int)rc.x, (int)rc.y, (int)rc.width, (int)rc.height),
        scale = 1f,
        atlasIndex = 0,
        sprite = sprites[names[i]],
    };
    sa.spriteGlyphTable.Add(glyph);

    var character = new TMPro.TMP_SpriteCharacter(0xFFFE, glyph) { name = names[i], scale = spriteScale };
    sa.spriteCharacterTable.Add(character);
}

sa.UpdateLookupTables();
UnityEditor.EditorUtility.SetDirty(sa);
UnityEditor.EditorUtility.SetDirty(mat);
UnityEditor.AssetDatabase.SaveAssets();
sb.Append((created ? "created " : "updated ") + assetPath + " sprites " + sa.spriteCharacterTable.Count);
return sb.ToString();
