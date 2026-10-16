# 서드파티 에셋 안내

이 저장소는 **유료 · 재배포 금지 에셋의 원본을 포함하지 않는다.** 해당 에셋의 이미지와 사운드는 같은 크기 · 같은 길이의 **플레이스홀더**(단색 도형, 무음)로 교체되어 있고, `.meta`(GUID · 슬라이스 설정)와 Tile 에셋은 그대로 두었다. 그래서 프로젝트는 열리고 실행되지만, 교체된 에셋이 쓰이는 곳은 단색 도형으로 보이고 소리가 나지 않는다.

## 원본이 필요한 에셋

| 에셋 | 제작자 | 라이선스 요지 | 교체된 위치 |
|---|---|---|---|
| [Tiny RPG Character Asset Pack 01 v2.0 - Full 22 Characters](https://zerie.itch.io/tiny-rpg-character-asset-pack) | Zerie | 게임에 사용 · 수정 가능. 원본 · 수정본의 재배포, AI 학습 · NFT 사용 금지. 크레딧은 권장 | `Assets/04.Sprites/Player/Swordsman with shadows`, `Assets/04.Sprites/Enemy/* with shadows`, `Assets/04.Sprites/NPC` |
| [Dungeon Tileset - Fantasy Dreamland Reborn (FDR Dungeon)](https://elvgames.itch.io/fdr-dungeon) | ElvGames | 게임에 사용 · 수정 가능, 크레딧 표기. 재판매(수정본 포함), 생성형 AI 학습 · NFT 사용 금지 | `Assets/04.Sprites/Tile/Dungeon/Dungeon*.png` 5장 |

Credits: Tiny RPG Character Asset Pack by Zerie, Dungeon Tileset - Fantasy Dreamland Reborn by ElvGames.

플레이어 · 적 · NPC 스프라이트에는 위 팩의 캐릭터를 수정한 것이 포함되어 있어 같이 교체했다.

## 출처를 확인하지 못해 교체한 것

재배포 가능 여부를 확인하지 못한 에셋은 안전을 위해 같은 방식으로 교체했다.

- 이미지: `Player/Reload/96x32 UISlidersPack.png`, `Consumable/grenades_by_mtk.png`, `Enemy/Dummy/Dummy-hit-Sheet.png`
- 사운드: 직접 합성하지 않은 효과음 전부(적 · 무기 · 소모품 · 플레이어 · UI · 컷씬 일부)

## 제거한 것

- Cartoon FX Remaster(`Assets/09.VFX/Consumable/JMO Assets`): Unity Asset Store EULA상 소스 재배포가 금지이고, 이 프로젝트에서 참조하지 않아 폴더째 제거했다.
- `Assets/_Sandbox`: 개인 테스트 씬.

## 교체된 파일 목록

- 이미지: [replaced-images.txt](Docs/public-release/replaced-images.txt)
- 사운드: [replaced-sounds.txt](Docs/public-release/replaced-sounds.txt)

## 원본 연결 방법

1. 위 두 에셋을 직접 구매해 압축을 푼다. 버전은 Tiny RPG **v2.0 Full 22 Characters**, FDR은 **Dungeon Tileset - Fantasy Dreamland Reborn**이어야 한다.
2. 저장소 루트에서 복원 스크립트를 실행한다. Python 3.8 이상이면 된다.

   ```
   python Docs/tools/restore_assets.py --tiny-rpg "<Tiny RPG 폴더>" --fdr "<FDR 폴더>"
   ```

   - Tiny RPG: `<캐릭터> with shadows` 폴더의 PNG 중 **파일 이름과 크기가 같은** 66개를 덮어쓴다.
   - FDR: `Tilesets/FDR_Dungeon.png`, `FDR_Dungeon_Animations.png`를 `Dungeon.png`, `DungeonAnimations.png`로 덮어쓴다.
   - `--dry`를 붙이면 파일을 바꾸지 않고 결과만 보여 준다.
3. Unity로 돌아가면 자동으로 다시 임포트된다. `.meta`는 건드리지 않는다.

복원되지 않는 파일(스크립트가 끝에 목록으로 보여 준다)은 팀이 직접 수정하거나 만든 것이라 구매해도 원본이 없다. 플레이스홀더로 남는다.

- 적: `Goblin Thrower`, `Elite Skeleton Archer`, `Dummy`
- 플레이어: `Swordsman_hands`, `Swordsman_shotgun1`
- NPC: Axeman, Grun, Guard, Lona, Elena 전부와 `Knight_Attack`, `Soldier_Attack`
- 맵: `DungeonDark`, `DungeonDarkStain`, `DungeonWallMid`(FDR 타일을 바탕으로 팀이 만든 것으로 보이며, 구매 파일에는 없음)

복원한 Tiny RPG 시트는 팀이 그림자를 조금 손본 원본과 발밑 몇 픽셀이 다를 수 있다.

사운드는 원본 파일을 배포하지 않으므로 직접 준비한 파일을 같은 이름으로 덮어쓰거나, 무음 상태로 둔다.

## 포함된 오픈 라이선스 항목

- TextMesh Pro 예제 폰트(Anton, Bangers, Oswald, Roboto, Liberation Sans): 각 폰트 옆의 라이선스 파일 참고
- Catacomb 타일(`Assets/04.Sprites/Tile/Catacomb`의 시트 4장): RF Catacombs v1.0 by [Szadi art](https://szadiart.itch.io)를 수정한 것. 공개 도메인이며 개인 · 상업 사용과 수정이 가능하고 에셋 팩 재판매만 금지된다(원문: `RF_Catacombs - License.txt`). Credits: Szadi art
- 한글 폰트 Mulmaru(Mushsooni): SIL Open Font License 1.1, 원문은 `Assets/TextMesh Pro/Fonts/Mulmaru - OFL.txt`
