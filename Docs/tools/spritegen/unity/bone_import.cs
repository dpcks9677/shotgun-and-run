// 스켈레톤 뼛조각 세팅 (python vfx/bone_vfx.py로 Bone.png를 쓴 뒤 실행. blood_stun_import.cs와 같은 방식으로 임시 Editor 폴더에 복사해 BoneImport.Run() 호출)
// 1) Bone.png: 57x57 칸 192개(Bone_v{변형}_d{방향}_f{프레임}), PPU 16 · Point · 무압축, 피벗 정중앙
// 2) Bone.anim: Blood.anim 복제 후 g · b 곡선을 r 곡선으로 교체 → 붉은 기 없이 흰색에서 회색으로만 어두워진다
// 3) Bone.controller: Blood.controller 복제, 상태의 클립을 Bone.anim으로
// 4) Bone.prefab: Blood.prefab 복제, 컨트롤러 · _sprites를 뼈로
// 5) SniperEnemy · EliteSniperEnemy(스켈레톤 궁수)의 EnemyHealth._bloodPrefab을 Bone.prefab으로
using System;
using System.Collections.Generic;
using System.Linq;
using ShotgunAndRun.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class BoneImport
{
    private const string BonePng = "Assets/04.Sprites/Player/Damaged/Bone.png";
    private const string BloodPrefab = "Assets/03.Prefabs/Damaged/Blood.prefab";
    private const string BonePrefab = "Assets/03.Prefabs/Damaged/Bone.prefab";
    private const string BloodAnim = "Assets/05.Animations/Damaged/Blood.anim";
    private const string BoneAnim = "Assets/05.Animations/Damaged/Bone.anim";
    private const string BloodController = "Assets/05.Animations/Damaged/Blood.controller";
    private const string BoneController = "Assets/05.Animations/Damaged/Bone.controller";
    private static readonly string[] SkeletonPrefabs =
    {
        "Assets/03.Prefabs/Enemy/SniperEnemy.prefab",
        "Assets/03.Prefabs/Enemy/EliteSniperEnemy.prefab",
    };

    private const int Cell = 57, Frames = 8, Dirs = 8, Variants = 3;

    public static void Run()
    {
        var sprites = ImportSheet();
        MakeClipAndController();
        MakePrefab(sprites);
        AssignToSkeletons();
        AssetDatabase.SaveAssets();
        Debug.Log("BoneImport OK");
    }

    private static Sprite[] ImportSheet()
    {
        var tex = new Texture2D(2, 2);
        ImageConversion.LoadImage(tex, System.IO.File.ReadAllBytes(BonePng));
        var cells = new List<(string name, Rect rect)>();
        for (int v = 0; v < Variants; v++)
            for (int d = 0; d < Dirs; d++)
                for (int f = 0; f < Frames; f++)
                    cells.Add(($"Bone_v{v}_d{d}_f{f}", new Rect(f * Cell, tex.height - (v * Dirs + d + 1) * Cell, Cell, Cell)));

        AssetDatabase.ImportAsset(BonePng, ImportAssetOptions.ForceSynchronousImport);
        var ti = (TextureImporter)AssetImporter.GetAtPath(BonePng);
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
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>()
            .SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());
        provider.Apply();
        ti.SaveAndReimport();

        var byName = AssetDatabase.LoadAllAssetRepresentationsAtPath(BonePng).OfType<Sprite>().ToDictionary(s => s.name);
        return cells.Select(c => byName[c.name]).ToArray();
    }

    private static void MakeClipAndController()
    {
        AssetDatabase.CopyAsset(BloodAnim, BoneAnim);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BoneAnim);
        var r = EditorCurveBinding.FloatCurve("", typeof(SpriteRenderer), "m_Color.r");
        var curve = AnimationUtility.GetEditorCurve(clip, r);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(SpriteRenderer), "m_Color.g"), curve);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(SpriteRenderer), "m_Color.b"), curve);

        AssetDatabase.CopyAsset(BloodController, BoneController);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BoneController);
        foreach (var st in controller.layers[0].stateMachine.states)
        {
            st.state.motion = clip;
        }
        EditorUtility.SetDirty(controller);
    }

    private static void MakePrefab(Sprite[] sprites)
    {
        AssetDatabase.CopyAsset(BloodPrefab, BonePrefab);
        var root = PrefabUtility.LoadPrefabContents(BonePrefab);
        try
        {
            root.name = "Bone";
            root.GetComponent<Animator>().runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BoneController);
            var so = new SerializedObject(root.GetComponent<Blood>());
            var prop = so.FindProperty("_sprites");
            prop.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            root.GetComponent<SpriteRenderer>().sprite = sprites[0];
            PrefabUtility.SaveAsPrefabAsset(root, BonePrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void AssignToSkeletons()
    {
        var bone = AssetDatabase.LoadAssetAtPath<GameObject>(BonePrefab);
        foreach (var path in SkeletonPrefabs)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var health = root.GetComponentsInChildren<MonoBehaviour>(true).First(m => m != null && m.GetType().Name == "EnemyHealth");
                var so = new SerializedObject(health);
                so.FindProperty("_bloodPrefab").objectReferenceValue = bone;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
