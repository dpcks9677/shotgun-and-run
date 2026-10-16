# 샷건 앤 런 (가제)

2D 탑뷰 샷건 슈터 + 익스트랙션 슈터. 빠른 템포로 던전을 탐색하고, 다수의 적을 한 번에 쓸어버리는 쾌감과 샷건 특유의 묵직한 반동을 핵심 재미로 삼는다.

- **레퍼런스**: [Enter the Gungeon](https://store.steampowered.com/app/311690/Enter_the_Gungeon/), [Escape from Duckov](https://store.steampowered.com/app/2985550/Escape_from_Duckov/)
- **개발 환경**: Unity `6000.3.22f1`
- **플레이 인원**: 싱글 전용

## 게임 플레이

쉘터에서 준비를 마치고 던전에 출격해, 적을 처치하며 재료를 모은 뒤 제한 시간 안에 탈출 구역으로 빠져나오는 것이 목표다.

- 무기는 샷건 1종이며, 모은 재료로 파츠 5종(총열·약실&노리쇠·탄띠·개머리판·탄약)을 강화해 성장시킨다.
- 던전은 정적으로 만든 큰 맵 1장이다. 메인 존 10곳을 커넥터가 이어 주고, 스폰 지점 3곳 중 1곳에서 시작한다.
- 탈출 구역은 런 시작 3분 뒤 활성화되며, 구역 안에서 10초를 버티면 탈출에 성공한다.
- 전 구간에서 주기적으로 습격이 발생해 오래 머물수록 위험해진다.
- 기본 12분을 넘기면 최대 체력 절반·이동속도 −20% 페널티가 걸려 탈출을 강제한다.
- 사망하면 가방 재료와 장착 소모품을 잃고, 런 XP는 30%만 유지한다.

**기본 조작**

| 입력 | 동작 |
|---|---|
| `W` `A` `S` `D` | 이동 |
| 마우스 | 조준 |
| 좌클릭 | 발사 (클릭당 1발) |
| 우클릭 | 회피(대쉬), 대쉬 중 무적 |
| `R` | 재장전 |
| `E` (2초 홀드) | 상자 개방 · 상호작용 |
| `1` `2` `3` | 소모품 사용 |
| `I` | 인벤토리 |
| `Tab` | 지도 |

## 핵심 루프

쉘터 준비 → 출격(랜덤 스폰) → 방 탐색 · 전투 → 상자 루팅 → 탈출 → 쉘터 (반복)

- **이너 루프**: 맵을 탐색하며 커넥터와 메인 존의 상자에서 재료를 루팅하고, 존 수비대와 순찰 적을 상대한다.
- **아우터 루프**: 탈출에 성공하면 재료가 창고에 쌓이고 런 XP를 얻는다. 쉘터에서 무기 파츠를 강화하고, 플레이어 레벨을 올리고, 소모품을 제작해 다시 출격한다.

**쉘터 시설**: 창고 · 무기 강화 · 플레이어 레벨 · 퀘스트 게시판 · 소모품 제작 · 출격

## 게임필(Game Feel) 목표

| 구분 | 요소 | 적용 상황 |
|---|---|---|
| 카메라 | 흔들림 | 총알 발사 시 반동 연출 |
| 연출 | 피격 번쩍임 | 적 피격 순간 |
| UI | 비네팅 | 체력이 낮을 때 |
| VFX | 피격 파티클 | 적 피격 시 |
| 사운드 | 타격 SFX (Pitch 변조) | 무기 발사마다 |
| 모션 | 넉백 | 적 피격 시, Weight별 차등 |

## 아트 · 에셋 레퍼런스

- [Tiny RPG Character Asset Pack 01](https://zerie.itch.io/tiny-rpg-character-asset-pack)
- [Pixel Hex Battle Tileset](https://zeshio.itch.io/pixel-hex-battle-1)
- [Dungeon Tilemap](https://white-play.itch.io/dungeon-tilemap)
- [16x16 Pixel Art Mini Dungeon (Tilemap)](https://aztharis.itch.io/tilemap-mini-dungeon)
- [Dungeon Gathering - UI-HUD Expansion](https://snowhex.itch.io/dungeon-gathering-ui-hud-expansion)
- [FDR Dungeon](https://elvgames.itch.io/fdr-dungeon)
- RF Catacombs by Szadi art (공개 도메인, 수정본 포함 — [라이선스](Assets/04.Sprites/Tile/Catacomb/RF_Catacombs%20-%20License.txt))

## 유료 에셋 연결

이 저장소에는 유료 · 재배포 금지 에셋의 원본이 들어 있지 않다. 해당 이미지와 사운드는 플레이스홀더(단색 도형, 무음)로 교체되어 있어서, 그대로 실행하면 캐릭터와 던전이 도형으로 보이고 소리가 나지 않는다. 원래 모습으로 보려면 아래 두 에셋을 직접 구매해 연결한다.

- [Tiny RPG Character Asset Pack 01 v2.0 - Full 22 Characters](https://zerie.itch.io/tiny-rpg-character-asset-pack) (Zerie)
- [Dungeon Tileset - Fantasy Dreamland Reborn](https://elvgames.itch.io/fdr-dungeon) (ElvGames)

1. 두 에셋을 구매해 압축을 푼다.
2. 저장소 루트에서 복원 스크립트를 실행한다.
   ```
   python Docs/tools/restore_assets.py --tiny-rpg "<Tiny RPG 폴더>" --fdr "<FDR 폴더>"
   ```
3. Unity를 열면 자동으로 다시 임포트된다.

팀이 직접 수정한 일부 스프라이트(고블린 투척병, 일부 NPC 등)와 사운드는 복원되지 않는다. 자세한 내용은 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) 참고.

## 라이선스

- 소스 코드: [MIT](LICENSE)
- 팀이 만든 아트 · 사운드 · 씬 · 프리팹 · 기획 문서: All rights reserved ([ASSETS_LICENSE.md](ASSETS_LICENSE.md))
- 서드파티 에셋: 각 제작자의 라이선스 ([THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md))

## 팀 구성

| 이름 | 역할 | 담당 업무 |
|---|---|---|
| 김예찬 | PM · 프로그래머 | 기획 총괄, 보스 설계 |
| 김민석 | 플레이어 프로그래머 | 플레이어 & 무기 메카닉, 아이템 |
| 정성훈 | 전투 프로그래머 | 적 AI & 전투 시스템 (FSM 중심) |
| 김호준 | 프로그래머 · UI 디자이너 | 파밍/탈출 시스템, 쉘터, UI/UX |

## 참고 문서

- [컨셉 & 코어 루프 기획서 (Notion)](https://app.notion.com/p/3b5ac2bc1c00830fab0801f7d2b45c45?source=copy_link)
