namespace ShotgunAndRun.Weapon
{
    // 펠릿 궤적이 끝난 자리에 어떤 착탄 효과를 낼지
    public enum PelletImpactKind
    {
        None,   // 사거리 끝까지 아무것도 맞히지 않음
        Wall,   // 벽 · 장애물 (불꽃 + 먼지)
        Enemy   // 피해를 받는 대상 (피 튐)
    }
}
