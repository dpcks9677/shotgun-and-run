// Thrower 폭탄 스프라이트 임포트 (먼저 python enemy/enemy_bomb.py import)
// unity command eval_file --file Docs/tools/spritegen/unity/enemy_bomb_import.cs
// 10x10 2프레임 Multiple · PPU 16 · Point · 무압축 · 가운데 피벗
const string png = "Assets/04.Sprites/Enemy/EnemyBomb/enemy-bomb.png";
const int frame = 10;
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
#pragma warning disable 618
var sprites = new UnityEditor.SpriteMetaData[2];
for (int i = 0; i < sprites.Length; i++)
{
    sprites[i] = new UnityEditor.SpriteMetaData
    {
        name = "enemy-bomb_" + i,
        rect = new UnityEngine.Rect(i * frame, 0, frame, frame),
        alignment = (int)UnityEngine.SpriteAlignment.Center,
        pivot = new UnityEngine.Vector2(0.5f, 0.5f)
    };
}
ti.spritesheet = sprites;
#pragma warning restore 618
ti.SaveAndReimport();
var sb = new System.Text.StringBuilder(png + " guid " + UnityEditor.AssetDatabase.AssetPathToGUID(png));
foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetRepresentationsAtPath(png))
    if (o is UnityEngine.Sprite s) sb.Append(" | " + s.name + " " + s.rect);
return sb.ToString();
