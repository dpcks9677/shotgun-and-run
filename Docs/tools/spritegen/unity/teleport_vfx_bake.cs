// 텔레포트 원본 파티클 이펙트를 프레임 시트로 굽는다 (eval_file로 실행). 이어서 python vfx/teleport_vfx.py 로 픽셀화한다.
// 준비: 원본 TeleportBeamFx.prefab · M_TeleportGlow.mat(+meta)을 Assets/_TempBake/에 꺼내 둔다
//   git show 0592596d:Assets/09.VFX/Cutscene/<파일> > Assets/_TempBake/<파일>  (또는 ~/Downloads/TeleportBeamFx_Soft.unitypackage)
// 조절: 바닥 고리 섬광(루트 파티클)은 굽지 않는다 — 바닥 도넛 동심원은 vfx/teleport_vfx.py가 픽셀로 그린다.
//       일자 파티클(Shards · Column)은 폭을 LINE_WIDTH배로 줄여 굽는다 (2026-10-10 사용자: 더 얇게)
// 결과: Temp/teleport_raw.png — 64px/unit, 프레임 80x128칸의 4배(320x512) × 18, 0.05초 간격, 검은 배경 가산 렌더. 굽고 나면 _TempBake는 지운다.
UnityEditor.AssetDatabase.ImportAsset("Assets/_TempBake", UnityEditor.ImportAssetOptions.ImportRecursive | UnityEditor.ImportAssetOptions.ForceSynchronousImport);
var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_TempBake/TeleportBeamFx.prefab");
if (prefab == null) return "Assets/_TempBake/TeleportBeamFx.prefab 없음";
const float LINE_WIDTH = 0.5f;
const int layer = 31; const int ppu = 64; const float wU = 5f, hU = 8f, groundU = 1.25f; const int frames = 18; const float fps = 20f;
int fw = (int)(wU * ppu), fh = (int)(hU * ppu);
// 다른 씬 오브젝트가 찍히지 않게 멀리 떨어진 곳 · 전용 레이어에서 굽는다
var origin = new UnityEngine.Vector3(10000f, 10000f, 0f);
var fx = UnityEngine.Object.Instantiate(prefab, origin, UnityEngine.Quaternion.identity);
foreach (var t in fx.GetComponentsInChildren<UnityEngine.Transform>(true)) t.gameObject.layer = layer;
foreach (var ps in fx.GetComponentsInChildren<UnityEngine.ParticleSystem>(true))
{
    ps.Stop(true, UnityEngine.ParticleSystemStopBehavior.StopEmittingAndClear);
    ps.useAutoRandomSeed = false; ps.randomSeed = 1234;
    var m = ps.main; m.stopAction = UnityEngine.ParticleSystemStopAction.None;
}
var root = fx.GetComponent<UnityEngine.ParticleSystem>();
fx.GetComponent<UnityEngine.ParticleSystemRenderer>().enabled = false;          // 바닥 고리 섬광은 굽지 않는다
foreach (var ps in fx.GetComponentsInChildren<UnityEngine.ParticleSystem>(true))
{
    var m = ps.main;
    if (ps.name == "Shards") { m.startSizeMultiplier *= LINE_WIDTH; }
    if (ps.name == "Column") { m.startSizeXMultiplier *= LINE_WIDTH; }
}
var camGo = new UnityEngine.GameObject("BakeCam"); var cam = camGo.AddComponent<UnityEngine.Camera>();
cam.orthographic = true; cam.orthographicSize = hU / 2f; cam.cullingMask = 1 << layer;
cam.clearFlags = UnityEngine.CameraClearFlags.SolidColor; cam.backgroundColor = UnityEngine.Color.black;
cam.transform.position = new UnityEngine.Vector3(origin.x, origin.y + hU / 2f - groundU, -10f);
var rt = new UnityEngine.RenderTexture(fw, fh, 24, UnityEngine.RenderTextureFormat.ARGB32); cam.targetTexture = rt; cam.aspect = wU / hU;
var sheet = new UnityEngine.Texture2D(fw * frames, fh, UnityEngine.TextureFormat.RGBA32, false);
var tmp = new UnityEngine.Texture2D(fw, fh, UnityEngine.TextureFormat.RGBA32, false);
for (int i = 0; i < frames; i++)
{
    root.Simulate((i + 1) / fps, true, true, true);
    cam.Render();
    UnityEngine.RenderTexture.active = rt; tmp.ReadPixels(new UnityEngine.Rect(0, 0, fw, fh), 0, 0); tmp.Apply(); UnityEngine.RenderTexture.active = null;
    sheet.SetPixels(i * fw, 0, fw, fh, tmp.GetPixels());
}
sheet.Apply();
System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath("Temp/teleport_raw.png"), sheet.EncodeToPNG());
cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(camGo); UnityEngine.Object.DestroyImmediate(fx); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
return "Temp/teleport_raw.png";
