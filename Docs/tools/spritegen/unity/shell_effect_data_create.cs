// 탄창 HUD 탄환 효과 데이터(ShellEffectData) 에셋 생성 · 갱신 (eval_file로 실행, 먼저 crit_shell_import.cs · shell_fire_import.cs)
// CritShell.png의 Shell_Crit · Spark_01..09, ShellFire.png의 Fire_01..10을 연결한다. 프레임 길이 · 흔들림 · 빈 탄피 시점은 shell_fire.py의 FRAMES와 같다(python shell_fire.py import가 출력).
// 에셋이 이미 있으면 같은 에셋의 스프라이트 · 프레임 값만 다시 채운다(guid 유지).
const string assetPath = "Assets/07.Datas/UI/ShellEffectData.asset";
const string critSheet = "Assets/04.Sprites/UI/CritShell.png";
const string fireSheet = "Assets/04.Sprites/UI/ShellFire.png";

float[] durations = { 0.040f, 0.030f, 0.040f, 0.035f, 0.035f, 0.035f, 0.040f, 0.045f, 0.050f, 0.060f };
UnityEngine.Vector2[] shake =
{
    new(0, 0), new(0, 0), new(2, 0), new(-1, 0), new(1, -1), new(-1, 1), new(1, 0), new(-1, 0), new(0, 1), new(0, 0),
};
const int emptyFrom = 6;

UnityEngine.Sprite Load(string path, string name)
{
    foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
        if (o is UnityEngine.Sprite sp && sp.name == name) return sp;
    throw new System.Exception("스프라이트 없음: " + name + " (" + path + ")");
}

if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/07.Datas/UI"))
    UnityEditor.AssetDatabase.CreateFolder("Assets/07.Datas", "UI");

var data = UnityEditor.AssetDatabase.LoadAssetAtPath<ShotgunAndRun.UI.ShellEffectData>(assetPath);
bool created = data == null;
if (created)
{
    data = UnityEngine.ScriptableObject.CreateInstance<ShotgunAndRun.UI.ShellEffectData>();
    UnityEditor.AssetDatabase.CreateAsset(data, assetPath);
}

data.CriticalShell = Load(critSheet, "Shell_Crit");
data.Sparks = new UnityEngine.Sprite[9];
for (int i = 0; i < 9; i++) data.Sparks[i] = Load(critSheet, "Spark_0" + (i + 1));

data.FireFrames = new ShotgunAndRun.UI.ShellEffectData.FireFrame[durations.Length];
for (int i = 0; i < durations.Length; i++)
{
    data.FireFrames[i] = new ShotgunAndRun.UI.ShellEffectData.FireFrame
    {
        Sprite = Load(fireSheet, "Fire_" + (i + 1).ToString("00")),
        Duration = durations[i],
        Shake = shake[i],
        Empty = i >= emptyFrom,
    };
}

UnityEditor.EditorUtility.SetDirty(data);
UnityEditor.AssetDatabase.SaveAssets();
return (created ? "created " : "updated ") + assetPath + " guid " + UnityEditor.AssetDatabase.AssetPathToGUID(assetPath);
