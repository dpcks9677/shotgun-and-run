// 타이틀 배경을 월드 방으로 구성한다 (TitleScene). 이미지 한 장이 아니라 게임과 같은 타일 · 포탈 · 화로 · Light2D를 쓴다.
//   unity command eval_file --file Docs/tools/spritegen/unity/title_room_setup.cs --timeout 120
// 저장하지 않는다. 확인 후 save_scene. 이미 구성된 씬에는 다시 돌리지 않는다(TitleRoom이 있으면 중단).
// capture_game_view의 save_path는 Assets/ 기준이다(Docs/…로 주면 Assets/Docs/…에 생김) — 찍은 뒤 옮기고 지울 것. 오류가 나면 씬을 다시 열어 버린다.
//
// 좌표: 카메라 정사영 크기 5.625 = 320x180(PPU 16). 타일 그리드 원점은 화면 왼쪽 아래보다 0.75칸 아래(아래 줄 일부가 잘리게),
//       그래서 위쪽 벽 6줄(58 · 가운데 4 · 86)이 화면 맨 위에 딱 맞는다.
var sb = new System.Text.StringBuilder();
const string ScenePath = "Assets/01.Scenes/TitleScene.unity";
const string DarkDir = "Assets/04.Sprites/Tile/Dungeon/Dark/";
var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
try
{
    if (UnityEngine.GameObject.Find("TitleRoom") != null) throw new System.Exception("TitleRoom이 이미 있다");

    T Load<T>(string path) where T : UnityEngine.Object
    {
        var a = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        if (a == null) throw new System.Exception("에셋 없음: " + path);
        return a;
    }
    UnityEngine.Tilemaps.TileBase Tile(string n) => Load<UnityEngine.Tilemaps.TileBase>(DarkDir + n + ".asset");

    var root = new UnityEngine.GameObject("TitleRoom");
    var gridGo = new UnityEngine.GameObject("Grid", typeof(UnityEngine.Grid));
    gridGo.transform.SetParent(root.transform, false);
    gridGo.transform.position = new UnityEngine.Vector3(-10f, -6.375f, 0f);

    UnityEngine.Tilemaps.Tilemap Map(string name, string layer)
    {
        var go = new UnityEngine.GameObject(name, typeof(UnityEngine.Tilemaps.Tilemap), typeof(UnityEngine.Tilemaps.TilemapRenderer));
        go.transform.SetParent(gridGo.transform, false);
        go.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>().sortingLayerName = layer;
        return go.GetComponent<UnityEngine.Tilemaps.Tilemap>();
    }
    var floor = Map("Floor", "Background");
    var wall = Map("Wall", "Wall");
    var decor = Map("Decor", "Environment");

    // 바닥 0~5줄, 벽 6(86) · 7~10(가운데 벽돌) · 11(58)
    var floorMain = Tile("DungeonDark_Floor_8");
    var floorVar = new[] { Tile("DungeonDark_Floor_9"), Tile("DungeonDark_Floor_10"), Tile("DungeonDark_Floor_11"), Tile("DungeonDark_Floor_12") };
    for (int r = 0; r <= 5; r++)
        for (int c = 0; c < 20; c++)
        {
            int h = (int)(((uint)c * 73856093u ^ (uint)r * 19349663u) % 100u);      // 칸 좌표 해시 (실행마다 같다)
            floor.SetTile(new UnityEngine.Vector3Int(c, r, 0), h < 8 ? floorVar[h % 4] : floorMain);
        }
    var w86 = Tile("DungeonDark_Wall_86"); var wMid = Tile("DungeonDark_Wall_86_2"); var w58 = Tile("DungeonDark_Wall_58");
    for (int c = 0; c < 20; c++)
    {
        wall.SetTile(new UnityEngine.Vector3Int(c, 6, 0), w86);
        for (int r = 7; r <= 10; r++) wall.SetTile(new UnityEngine.Vector3Int(c, r, 0), wMid);
        wall.SetTile(new UnityEngine.Vector3Int(c, 11, 0), w58);
    }

    // 화로: 애니메이션 타일 + 광원표(LightSourceTable)에 있는 불빛은 TileLightEmitter가 만든다
    var brazier = Load<UnityEngine.Tilemaps.TileBase>("Assets/04.Sprites/Tile/Dungeon/Props/Brazier_Pillar.asset");
    foreach (int c in new[] { 11, 18 }) decor.SetTile(new UnityEngine.Vector3Int(c, 5, 0), brazier);
    var emitter = root.AddComponent<ShotgunAndRun.Environment.TileLightEmitter>();
    var so = new UnityEditor.SerializedObject(emitter);
    so.FindProperty("_table").objectReferenceValue = Load<ShotgunAndRun.Environment.LightSourceTable>("Assets/07.Datas/Environment/LightSourceTable.asset");
    so.ApplyModifiedPropertiesWithoutUndo();

    // 포탈: 습격 포탈 Big(파란) + 열리는 연출 → 대기. 컨트롤러는 새로 만든다(원본 컨트롤러는 Active 파라미터를 코드가 켜야 해서 타이틀에서는 못 씀)
    const string CtrlPath = "Assets/05.Animations/Environment/ExitPortal/TitlePortal.controller";
    var ctrl = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(CtrlPath);
    if (ctrl == null)
    {
        ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
        var sm = ctrl.layers[0].stateMachine;
        var up = sm.AddState("Fade_Up"); up.motion = Load<UnityEngine.AnimationClip>("Assets/05.Animations/Environment/ExitPortal/ExitPortal_Front_Fade_Up.anim");
        var idle = sm.AddState("Idle_Active"); idle.motion = Load<UnityEngine.AnimationClip>("Assets/05.Animations/Environment/ExitPortal/ExitPortal_Front_Idle_Active.anim");
        sm.defaultState = up;
        var t = up.AddTransition(idle); t.hasExitTime = true; t.exitTime = 1f; t.duration = 0f;
    }
    var portal = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(Load<UnityEngine.GameObject>("Assets/03.Prefabs/Environment/ExitPortalFx_Front.prefab"), scene);
    portal.transform.SetParent(root.transform, false);
    portal.transform.position = new UnityEngine.Vector3(5.625f, -2.0f, 0f);
    portal.GetComponentInChildren<UnityEngine.Animator>().runtimeAnimatorController = ctrl;

    // 플레이어: 프리팹 전체(전투 스크립트 포함)는 쓰지 않고 몸 · 총 스프라이트만. 총 위치는 Player.prefab 실측
    // (AimOrigin + WeaponPivot + WeaponSprite = (-0.198, -0.238), 오른쪽 조준)
    var player = new UnityEngine.GameObject("TitlePlayer");
    player.transform.SetParent(root.transform, false);
    player.transform.position = new UnityEngine.Vector3(2.875f, -3.75f, 0f);
    var bodyGo = new UnityEngine.GameObject("Body", typeof(UnityEngine.SpriteRenderer), typeof(UnityEngine.Animator));
    bodyGo.transform.SetParent(player.transform, false);
    var bodySr = bodyGo.GetComponent<UnityEngine.SpriteRenderer>();
    bodySr.sortingLayerName = "Characters";
    foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/04.Sprites/Player/Swordsman with shadows/Swordsman.png"))
        if (o is UnityEngine.Sprite sp && sp.name == "Swordsman_0") bodySr.sprite = sp;
    bodyGo.GetComponent<UnityEngine.Animator>().runtimeAnimatorController = Load<UnityEngine.RuntimeAnimatorController>("Assets/05.Animations/Player/Player.controller");
    var gunGo = new UnityEngine.GameObject("WeaponSprite", typeof(UnityEngine.SpriteRenderer));
    gunGo.transform.SetParent(player.transform, false);
    gunGo.transform.localPosition = new UnityEngine.Vector3(-0.198f, -0.238f, 0f);
    var gunSr = gunGo.GetComponent<UnityEngine.SpriteRenderer>();
    gunSr.sortingLayerName = "Weapon";
    foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/04.Sprites/Weapon/shotgun.png"))
        if (o is UnityEngine.Sprite sp && sp.name == "shotgun_0") gunSr.sprite = sp;

    // 카메라: TitleScene에는 Main Camera 프리팹이 이미 있다(CameraRecoil). 새로 만들지 말고 크기만 320x180 기준으로 (AudioListener 중복 방지)
    // 다른 카메라(UI · 미니맵 등)는 건드리지 않도록 MainCamera 태그 카메라만 바꾼다
    UnityEngine.Camera.main.orthographicSize = 5.625f;

    // 빛: 전역(어둡게 깔기) + 포탈 푸른 번짐(Additive, 포탈 중앙)
    var gl = new UnityEngine.GameObject("Global Light 2D", typeof(UnityEngine.Rendering.Universal.Light2D));
    var l2d = gl.GetComponent<UnityEngine.Rendering.Universal.Light2D>();
    l2d.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;
    l2d.intensity = 0.45f; l2d.color = new UnityEngine.Color(0.7f, 0.76f, 1f, 1f);
    var pg = new UnityEngine.GameObject("PortalGlow", typeof(UnityEngine.Rendering.Universal.Light2D));
    pg.transform.SetParent(root.transform, false);
    pg.transform.position = new UnityEngine.Vector3(5.625f, 0.6f, 0f);
    var pl = pg.GetComponent<UnityEngine.Rendering.Universal.Light2D>();
    pl.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
    pl.color = new UnityEngine.Color(0.3f, 0.6f, 1f, 1f);
    pl.intensity = 0.3f; pl.pointLightOuterRadius = 6f; pl.pointLightInnerRadius = 0f; pl.falloffIntensity = 0.8f;
    pl.blendStyleIndex = 1;

    // 로고: 스프라이트(title_redraw_0 = 183x97) 비율 그대로 3/4 크기. 옛 로고용 240x135 그대로 두면 가로세로가 다르게 늘어난다
    var logoRt = (UnityEngine.RectTransform)UnityEngine.GameObject.Find("Logo").transform;
    logoRt.sizeDelta = new UnityEngine.Vector2(183f * 0.75f, 97f * 0.75f);
    logoRt.anchoredPosition = new UnityEngine.Vector2(-12f, 30f);

    UnityEditor.EditorUtility.SetDirty(root);
    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
    sb.AppendLine("방 구성 완료 (저장 전)");
}
catch (System.Exception e)
{
    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
    sb.AppendLine("실패 → 씬을 다시 열어 변경을 버렸다: " + e.Message);
}
return sb.ToString();
