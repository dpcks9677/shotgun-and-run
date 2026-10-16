namespace InventorySystem
{
    // 재료 10종입니다. 에셋(ItemData · StageLootConfig)과 버전 1 세이브에 정수로 저장되므로
    // 값을 명시해 고정합니다. 순서를 바꾸거나 기존 값을 재사용하지 말고, 새 재료는 끝에 새 값으로 추가합니다.
    public enum MaterialType
    {
        IronIngot = 0, // 철괴 (구 Steel)
        DwarvenMechanism = 1, // 드워프 기계장치 (구 PrecisionPart)
        TrollOil = 2, // 트롤 기름 (구 Lubricant)
        TinkerSpring = 3, // 땜장이 용수철 (구 Spring)
        TannedLeather = 4, // 무두질한 가죽 (구 Leather)
        ElvenSilk = 5, // 엘프 실크 (구 Polymer)
        SulfurPowder = 6, // 유황 가루 (구 Gunpowder)
        LeadChunk = 7, // 납덩이 (구 Lead)
        MagicStone = 8, // 마력석 (구 Core)
        HealingHerb = 9 // 치유 약초
    }
}
