// 투척 투사체 스프라이트 임포트 (먼저 python items/grenade_projectiles.py import)
// unity command eval_file --file Docs/tools/spritegen/unity/grenade_projectiles_import.cs
// Single 스프라이트 · PPU 16 · Point · 무압축 · 가운데 피벗
var sb = new System.Text.StringBuilder();
foreach (var png in new[] { "Assets/04.Sprites/Consumable/grenade-projectile-frag.png", "Assets/04.Sprites/Consumable/grenade-projectile-flash.png" })
{
    UnityEditor.AssetDatabase.ImportAsset(png, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
    var ti = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(png);
    ti.textureType = UnityEditor.TextureImporterType.Sprite;
    ti.spriteImportMode = UnityEditor.SpriteImportMode.Single;
    ti.spritePixelsPerUnit = 16f;
    ti.filterMode = UnityEngine.FilterMode.Point;
    ti.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
    ti.mipmapEnabled = false;
    ti.alphaIsTransparency = true;
    ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
    var settings = new UnityEditor.TextureImporterSettings();
    ti.ReadTextureSettings(settings);
    settings.spriteAlignment = (int)UnityEngine.SpriteAlignment.Center;
    ti.SetTextureSettings(settings);
#pragma warning disable 618
    ti.spritesheet = new UnityEditor.SpriteMetaData[0];   // 기본 프리셋이 남긴 슬라이스 항목 제거
#pragma warning restore 618
    ti.SaveAndReimport();
    var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(png);
    sb.Append(png + " guid " + UnityEditor.AssetDatabase.AssetPathToGUID(png) + " sprite " + (sprite != null ? sprite.rect.ToString() : "null") + "; ");
}
return sb.ToString();
