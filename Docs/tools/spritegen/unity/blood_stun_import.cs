// 스턴 · 핏자국 재임포트 (python vfx/stun_vfx.py M, python vfx/blood_vfx.py 로 PNG를 쓴 뒤 실행)
// Docs 안의 .cs는 컴파일되지 않는다. Assets/<임시>/Editor/ 로 복사해 BloodStunImport.Run()을 부르고(eval 또는 -executeMethod), 끝나면 지운다.
// 1) Enemy_Stun.png: 22x16 6프레임 슬라이스. 기존 spriteID를 이름으로 재사용하므로 Stun.anim 참조가 유지된다. PPU 16 · Point · 무압축
// 2) Blood.png: 57x57 칸, 이름 Blood_v{변형}_d{방향}_f{프레임} 192개. PPU 16 · Point · 무압축, 피벗 정중앙
// 3) Blood.prefab: Blood._sprites에 [변형][방향][프레임] 순서로 채우고 기본 스프라이트를 첫 칸으로
// 4) Blood.anim: 스프라이트 곡선 제거 (프레임 재생은 Blood.cs가 한다). 색 곡선(톤 다운)은 그대로 둔다
using System;
using System.Collections.Generic;
using System.Linq;
using ShotgunAndRun.Combat;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class BloodStunImport
{
    private const string StunPng = "Assets/04.Sprites/Enemy/Enemy_Stun.png";
    private const string StunAnim = "Assets/05.Animations/Enemy/Stun/Stun.anim";
    private const string BloodPng = "Assets/04.Sprites/Player/Damaged/Blood.png";
    private const string BloodPrefab = "Assets/03.Prefabs/Damaged/Blood.prefab";
    private const string BloodAnim = "Assets/05.Animations/Damaged/Blood.anim";

    private const int StunW = 22, StunH = 16, StunFrames = 6;
    private const int Cell = 57, Frames = 8, Dirs = 8, Variants = 3;

    public static void Run()
    {
        ImportStun();
        ImportBlood();
        AssetDatabase.SaveAssets();
        Debug.Log("BloodStunImport OK");
    }

    private static void Slice(string png, List<(string name, Rect rect)> cells)
    {
        AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceSynchronousImport);
        var ti = (TextureImporter)AssetImporter.GetAtPath(png);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.spritePixelsPerUnit = 16f;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.wrapMode = TextureWrapMode.Clamp;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(ti);
        provider.InitSpriteEditorDataProvider();
        var oldIds = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);

        var rects = cells.Select(c => new SpriteRect
        {
            name = c.name,
            rect = c.rect,
            alignment = SpriteAlignment.Center,
            pivot = new Vector2(0.5f, 0.5f),
            spriteID = oldIds.TryGetValue(c.name, out GUID id) ? id : GUID.Generate(),
        }).ToArray();
        provider.SetSpriteRects(rects);
        var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        names.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());
        provider.Apply();
        ti.SaveAndReimport();
    }

    private static Dictionary<string, Sprite> LoadSprites(string png)
    {
        return AssetDatabase.LoadAllAssetRepresentationsAtPath(png).OfType<Sprite>().ToDictionary(s => s.name);
    }

    private static void ImportStun()
    {
        var cells = new List<(string, Rect)>();
        for (int i = 0; i < StunFrames; i++)
        {
            cells.Add(("Enemy_Stun_" + i, new Rect(i * StunW, 0, StunW, StunH)));
        }
        Slice(StunPng, cells);

        // 기존 클립이 새 스프라이트를 그대로 가리키는지 확인
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(StunAnim);
        var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single(b => b.propertyName == "m_Sprite");
        var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
        if (keys.Length != StunFrames || keys.Any(k => k.value == null || !k.value.name.StartsWith("Enemy_Stun_")))
        {
            throw new Exception("Stun.anim 스프라이트 참조가 깨짐");
        }
    }

    private static void ImportBlood()
    {
        int height = 0;
        var tex = new Texture2D(2, 2);
        ImageConversion.LoadImage(tex, System.IO.File.ReadAllBytes(BloodPng));
        height = tex.height;

        var cells = new List<(string, Rect)>();
        for (int v = 0; v < Variants; v++)
        {
            for (int d = 0; d < Dirs; d++)
            {
                for (int f = 0; f < Frames; f++)
                {
                    int row = v * Dirs + d;
                    cells.Add(($"Blood_v{v}_d{d}_f{f}", new Rect(f * Cell, height - (row + 1) * Cell, Cell, Cell)));
                }
            }
        }
        Slice(BloodPng, cells);

        var sprites = LoadSprites(BloodPng);
        var ordered = cells.Select(c => sprites[c.Item1]).ToArray();

        var root = PrefabUtility.LoadPrefabContents(BloodPrefab);
        try
        {
            var so = new SerializedObject(root.GetComponent<Blood>());
            var prop = so.FindProperty("_sprites");
            prop.arraySize = ordered.Length;
            for (int i = 0; i < ordered.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = ordered[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            root.GetComponent<SpriteRenderer>().sprite = ordered[0];
            PrefabUtility.SaveAsPrefabAsset(root, BloodPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BloodAnim);
        foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip).Where(b => b.propertyName == "m_Sprite").ToArray())
        {
            AnimationUtility.SetObjectReferenceCurve(clip, b, null);
        }
        EditorUtility.SetDirty(clip);
    }
}
