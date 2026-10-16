namespace ShotgunAndRun.Cutscene
{
    // HUD 명령: 키로 구분한 HUD(HudGate)를 단계별로 열고 닫습니다. 컷씬이 HUD 숨김이면 열어 둔 HUD도 컷씬 동안은 가려집니다.
    public sealed partial class CutsceneContext
    {
        /// <summary>
        /// 키가 같은 HUD(HudGate)를 열거나 닫습니다. 컷씬이 끝난 뒤에도 유지됩니다. (예: "Health", "Dash", "Ammo", "QuickSlot")
        /// </summary>
        public void ShowHud(string key, bool visible = true)
        {
            HudGate.Set(key, visible);
        }
    }
}
