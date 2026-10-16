# 던전 홀 공용 장식 배치 계획서

> **작성 일자:** 2026-10-07  
> **상태:** 배치 완료 (2026-10-07, v03) — 남은 일: 플레이 확인 · 커밋 · level-design 기록 (5.4)  
> **관련 문서:** [level-design.md](level-design.md) (1.5 홀 · 1.6 홀 배치표) / [hall-lighting-plan.md](hall-lighting-plan.md) (홀 광원, 테마 계산 4.1) / 홀 도구 `Docs/tools/halls/README.md`

---

## 1. 목표

- 홀 17곳(H1~H10, HA~HG)에 **이미 있는 존 소품 · 공용 폐허 소품**을 다시 써서 장식을 놓는다. 새 그림은 그리지 않는다.
- 장식은 **가까운 존의 무드**를 따른다 (광원과 같은 테마 지도). 존 문에 가까울수록 그 존 소품이 짙고, 멀어질수록 폐허(균열 · 잔해)로 옅어진다 — 존 분위기가 홀로 새어 나온 느낌.
- 홀의 교전 공간은 건드리지 않는다 (사용자 결정 2026-10-07: A + B안).
  - **A:** 충돌 없는 장식 — 벽걸이 · 바닥 데칼
  - **B:** 벽에 붙인 블록 소품만 — 홀 가운데(길 위)에는 두지 않는다

**제외:** 광원 (홀 광원 계획서에서 끝남), R7 회랑 (자기 프리팹 `R7_Catacomb`에 소품이 있음), 존 · 보스룸 · 기믹룸 · 탈출 구역 방 자체, 물 · 쇠창살 칸 (Z3_Sewer 프리팹 영역).

---

## 2. 현재 상태 (조사 결과)

### 2.1 홀 규모 (`Stage1Layout.asset`, 물 칸 제외)

| 항목 | 값 |
|---|---|
| 홀 바닥 | 16,599칸 |
| 벽에 닿은 바닥 변 | 북 899 · 남 887 · 동 942 · 서 954 |
| 오목 모서리 칸 (두 방향이 벽) | 278 |
| 테마별 바닥 (광원 4.1 BFS) | 중립 3,947 · Z4 2,172 · Z1 1,980 · Z3 1,493 · Z6 1,356 · Z5 1,343 · Z9 1,311 · Z2 1,081 · Z8 746 · Z10 640 · Z7 530 |

- 이미 놓인 것: 광원 91개 (`Halls_Decor` — Decor 벽걸이 61, Block 바닥 30, 배치 기록 `Docs/tools/halls/lights_prev.json`), 커넥터 상자 지점 (벽 앞, 상자 둘레 길 유지).

### 2.2 담을 곳

- `Halls_Decor`에 함께 넣지 않는다. `lights.py apply`는 프리팹 칸 수가 `lights_prev.json`과 다르면 **손 수정으로 보고 중단**하므로, 장식이 같은 타일맵에 들어가면 광원을 다시 칠할 수 없다.
- → 새 프리팹 `Rooms/Halls_Props` (`RoomDecor_Base` Variant)에 담는다 (5.1).

### 2.3 재료 크기 표기

스프라이트 크기 → 칸: 18×20 = 1×1, 34×20 = 2×1, 18×36 = 1×2, 34×36 = 2×2, 50×20 = 3×1, 34×52 = 2×3. 아래 표의 `(2×1)` 등은 이 기준.

---

## 3. 테마 대응표

존에서 실제로 쓴 타일(방 프리팹 Decor · Block · Decal 층)만 골랐다. **존 대표 소품은 홀에 쓰지 않는다** (존에 들어섰을 때의 인상을 아끼려고).

| 테마 | 벽걸이 (Decor) | 벽 붙임 블록 (Block) | 데칼 (Decal · Decor) | 쓰지 않음 (존 대표) |
|---|---|---|---|---|
| Z1 대장간 | 폐허 `Banner_Torn` · `Wall_Crack_*` (존에 벽걸이 없음) | `CoalBox_Wide`(2×1) · `QuenchBarrel`(1×2) · `WeaponRack_Axes/Spears/Swords/Empty`(2×2) | `ZoneDecal_Soot` | `Forge` · `Anvil` · `Bellows` · `Workbench_*` · `CoalBox_Large` |
| Z2 드워프 공방 | `BrassPipe_H` 계열 (가로 배관 줄) · `Warn_Sign`(1×2) | `Powder_Keg` · `Powder_Crate` · `Sack_Saltpeter/Charcoal/Sulfur` · `Shot_Crate` · `Shot_Pile_S`(1×1) · `Shot_Pile_M` · `Fuse_Reel`(2×1) | `ZoneDecal_Oil` · `Decal_Powder_Scatter` · `Decal_Powder_Spill`(2×1) | `Kiln` · `Steam_Hammer` · `Trebuchet` · `Powder_Bench` · `Dwarf_Wall_*` |
| Z3 하수도 | `Wall_Stain`(1×3) · `Wall_Grate`(2×2) | `OilBarrel_Sealed/Open/Leak/Lids/Broken`(1×1) · `OilBarrel_Tipped/Pair/Rack`(2×1) · `OilBarrels_Cluster`(2×2) | `OilSpill_S` · `OilSpill_M`(2×1) | `OilTank` · `OilPump` · `Outflow_*` |
| Z4 기관실 | `Pipe_H` · `Pipe_V` 계열 · `Chain_Wall`(1×3) | `Coal_Barrow`(2×1) · `Scrap_Gear_Pile`(2×1) · `CoalBox`(1×1) | `ZoneDecal_Oil` · `Oil_Spill`(2×2) | `Boiler` · `Furnace` · `Cage_Elevator` · `Gantry_Crane` · `Elevator_*` · `Gear_Wall` · 레일 |
| Z5 마력석 광맥 | `WallTools`(1×2) | `Minecart_Empty` · `Minecart_Ore` · `RockSlab_M` · `RockSlab_M_moss`(2×2) · `RockSlab_S` · `CoalBox`(1×1) · `CoalBox_Wide`(2×1) (v03) | `ZoneDecal_ManaDust` · `Sprout` · `Tool_Hammer/Pick/Shovel` | `Crystal_*` · `RockFacet_*` · `Minecart_Crystal` · 레일 |
| Z6 도축업장 | `Wall_Carcass` · `Wall_Sausages` · `Wall_Herbs`(1×2) · `Wall_ToolRack` · `Wall_Notice`(2×1) · `Web_*` | `Barrel_Salt/Lid/Blood` · `Crate` · `Sack` · `Bucket` · `BonePile`(1×1) · `Crate_Large` · `Sack_Pile` · `Firewood`(2×1) | `Blood_Drops/Splat/Drag/Pool` | `Butcher_Table` · `MeatRack` · `Cauldron` · `ChoppingBlock` |
| Z7 고블린 캠프 | `Wall_Shield`(1×1) · `Wall_Banner`(2×3) | `Crate` · `Sack` · `Barrel_Lid/Salt` · `Armor_Pile`(1×1) · `Crate_Large` · `Sack_Pile` · `Firewood`(2×1) | `Decal_Ash`(2×1)만 (v02) | `GoblinTent` · `War_Table` · `Campfire` · `Supply_Pile` · `Sack_Heap` · `Crate_Stack` · `Wall_WarMap` |
| Z8 연금술방 | `WallShelf_books`(2×1) · `Rune_Text`(2×2) | `Books_stack/two/open` · `SoulJar_B/G/P` · `SkullPile_S`(1×1) · `Bookshelf_broken`(2×2) | `Decal_Alch_*` · `Decal_ink` · `Decal_paper` · `Decal_wax` · `Decal_Necro_ash` | `MagicCircle_*` · `Athanor` · `Cauldron_*` · `Gargoyle` · `LabTable_*` · `Altar` |
| Z9 지하묘지 | `Niche`(2×4, Catacomb 4줄 벽만) · `Necro_BoneOrnament` · `Web_Drape` | `Tomb_*`(1×2) · `SkullPile_S`(1×1) · `SkullPile_M`(2×1) · `BonePile` · `SoulJar_G`(1×1) | `Decal_Necro_bones` | `Sarcophagus*` · `CatacombPillar` · `Ledge_*` · `LightPool` |
| Z10 의무실 | `Wall_MedHerbs` · `Wall_MedTools` · `WallShelf_jars/potions`(2×1) · `Wall_Anatomy`(2×2) | `Med_crate` · `Med_bandages` · `Med_basin` · `Bucket_water/mop/blood`(1×1) · `MedCrate_Stack_1x`(1×2) · `Stretcher_broken` · `Splint_*`(2×1) | `ZoneDecal_Blood` · `Decal_Med_*` | `Bed_*` · `Cot_*` · `SurgeryTable_*` · `Curtain_*` · `Laundry_*` |
| 중립 — Dungeon 홀 | `Wall_Crack_S`(1×2) · `Wall_Crack_L/Radial`(2×2) · `Wall_Crack_Horizontal` · `Beam_Wall`(2×1) · `Banner_Torn`(1×2) | `Rubble_S` · `Barrel_Broken` · `Crate_Broken`(1×1) · `Rubble_M`(2×1) · `Rubble_L` · `Pillar_Broken_L`(2×2) · `Pillar_Broken_S`(1×2) · `Pillar_Fallen` · `Beam_Fallen`(3×1) | `Floor_Crack` · `Debris` | — |
| 중립 — Catacomb 홀 | 중립 Dungeon + `Web_*` | 중립 Dungeon + `SkullPile_S/M` · `BonePile` | `Floor_Crack` · `Debris` · `Decal_Necro_bones` | — |

- **존에서 안 쓴 재료:** `ZoneDecal_ManaDust`(Z5)는 존 데칼 시트에 있지만 존에는 칠하지 않은 무늬, `Necro_BoneOrnament`(Z9)는 Alchemy 시트의 벽걸이. 같은 시트라 그림체가 맞아서 넣음.
- **폐허 바탕:** 존 테마 구역에도 중립 재료(폐허)를 섞는다. 비율은 4.1의 농도 기울기를 따른다.
- 같은 종류의 변형(`_1` · `_2` · `_3`, `S/M/L`)은 칸 좌표 해시로 섞는다 (광원과 같은 방식).
- **참고:** 홀 광원의 Z7 테마는 재봉소 때 정한 촛대다. 장식은 고블린 캠프 소품이라 둘이 어긋난다 → 6장 결정 6.

---

## 4. 배치 규칙

### 4.1 테마와 농도

1. **테마:** 광원 계획서 4.1과 같은 지도를 쓴다 (`lights.py`의 `nearest_zone` + 섹션 다듬기 + H6 가운데 방 중립). 장식과 광원의 테마 경계가 정확히 맞는다.
2. **농도 기울기:** 칸마다 가장 가까운 존 문까지 걸은 거리(같은 BFS의 `dist`)로 존 소품 비율을 정한다.

| 존 문에서 | 존 소품 : 폐허 | 데칼 밀도 |
|---|---|---|
| 12u 안 | 8 : 2 | 바닥의 6% |
| 12~24u | 5 : 5 | 3% |
| 24u 밖 · 중립 | 0 : 10 | 1% (균열 · 잔해만) |

### 4.2 벽걸이 (Decor, 충돌 없음)

- **자리:** 북쪽 벽 정면만 (광원과 같은 이유). 칸막이 · 기둥 앞면 포함. 줄은 광원과 같은 **2번째 줄 기준**, 1×3 · 2×3 · 2×4처럼 큰 것은 벽 정면 줄 수 안에 들어갈 때만 (Dungeon 3줄, Catacomb 4줄).
- **간격:** 광원을 포함해 **벽 정면 6u마다 1개 꼴** (v02). 광원과 **좌우 2u 이상** 떨어진다.
- **건너뛰기:** 길이 4u 미만 벽 구간, 통로 입구 칸 위, 아치 게이트 · 문기둥 (`Outflow_Arch`, `CatacombGatePost`).
- **가로 배관(Z2 · Z4):** 1×1 조각을 이어 붙여 2~4u 길이 한 줄로 놓는다 (끝 조각 `cap`, 중간에 `valve` · `gauge` 하나).

### 4.3 벽 붙임 블록 (Block, 이동만 막음)

- **붙이기:** 소품 한 변 전체가 벽에 붙는다 (커넥터 상자 규칙과 같음). 벽에서 **최대 2u**만 튀어나온다.
- **묶음:** 소품 2~4개(바닥 3~6칸)를 한 묶음으로 둔다. 하나만 덩그러니 놓지 않는다.
- **자리 우선순위:** ① 오목 모서리 ② 벽선 홈(갉은 구간 안쪽) ③ 칸막이 · 기둥의 벽 면 옆. 탁 트인 긴 벽 가운데는 마지막.
- **존 문 앞 표식:** 존 문마다 문에서 4~8u 떨어진 벽에 그 존 소품 묶음 1개를 먼저 놓는다 (홀에서 다음 존이 무엇인지 미리 보이게).
- **막지 말 것:**
  - 문 · 통로 입구 · 칸막이 틈 3u 안
  - 블록 앞(벽 반대쪽)으로 **빈 바닥 4u 이상** — 4u 미만으로 좁아지는 자리에는 두지 않는다
  - 문과 문 사이 4u 폭 길 연결 (`layout_edit.connected`)
  - 상자 지점 3×3 + 1u, 상자 둘레 길
  - 바닥 광원 둘레 1u. 북쪽 벽 광원 아래 칸에는 1칸 높이 소품만 (2칸 높이는 광원 그림을 가림)
  - 물 · 쇠창살 · 다리 칸과 그 둘레 1u
- **충돌:** `Block` 층 (Water 레이어, 이동만 막음 · 총알은 통과) — 결정 2.

### 4.4 바닥 데칼 (충돌 없음)

- **자리:** 바닥 칸 (물 · 상자 지점 · 블록 칸 제외). 4.1 농도대로.
- **모양:** 블록 묶음 둘레와 존 문 앞에 모은다 (흩뿌리지 않음). 피 끌린 자국(`Blood_Drag` · `Decal_Med_drag`)은 문 쪽을 향하게.
- **ZoneDecal 패치:** `ZoneDecal_<종류>_<번호>_<조각>`은 2~4조각이 한 무늬라 조각을 함께 놓는다 (존 프리팹 Decal 층과 같은 방식).

### 4.5 목표 수치 (추정, 시안에서 셈)

- **블록 묶음** — 홀 바닥 125칸당 1묶음 + 존 문 앞 표식 묶음 (v02). 맵 전체 블록 약 760칸 (홀 바닥의 약 4.6%).
- **벽걸이 장식** — 광원 포함 6u마다 1개 꼴 (v02, 처음 제안은 8u).
- **데칼:** 홀 바닥의 2~3% (약 400칸).

---

## 5. 구현

### 5.1 담을 곳

- 새 프리팹 `Assets/03.Prefabs/Environment/Rooms/Halls_Props.prefab` = `RoomDecor_Base` Variant. 자식 `Halls_Props Decal` (존 프리팹 Decal 층과 같은 정렬) · `Halls_Props Block` · `Halls_Props Decor`. `Rooms` 아래 원점에 배치.
- `TileLightEmitter`는 붙이지 않는다 (광원 없음). 페인터 타일맵 · 존 프리팹 · `Halls_Decor`는 건드리지 않는다.
- 씬 인스턴스를 저장하면 **프리팹을 같은 커밋에** 넣는다 (홀 광원 때 씬만 먼저 dev에 올라간 사고).

### 5.2 스크립트 (`Docs/tools/halls/decor.py`)

- 입력: `Stage1Layout.asset`, 씬 Wall 타일맵 덤프(`_dump_tilemaps.cs`), 상자 지점 덤프(`_dump_chest_points.cs` → `CHEST_POINTS`), 광원 배치 `lights_prev.json`.
- 테마 · 거리는 `lights.py`의 `load` · `nearest_zone` · `center_room`을 그대로 가져다 쓴다.
- 하는 일: 4.1 테마 · 농도 → 4.2 · 4.3 · 4.4 자리 생성 → 검사 → 시안 PNG + `eval_file`용 C# 생성.
- 칠하기 C#: 바뀐 칸만 `SetTile`, **`ClearAllTiles` 금지**. 이전 결과(`decor_prev.json`)와 프리팹 타일이 정확히 같을 때만 지우고 다시 놓음 (손 수정이 있으면 중단). 칠한 뒤 프리팹 안에서 `CompressBounds` → `ApplyPrefabInstance`. 열린 씬이 `DungeonStageScene`이 아니면 중단.

### 5.3 검사

- 벽걸이 칸이 실제 북쪽 벽 정면 타일 위인지, 광원과 2u 이상
- 블록이 벽에 붙었는지, 앞쪽 빈 바닥 4u, 문 사이 4u 길 연결 유지
- 상자 지점 막힘 0 (`ChestPointValidator.FindBlocked`), 상자 둘레 길
- `Halls_Decor` · 다른 방 프리팹 타일과 겹침 0, 물 칸 0
- 한 벽 구간 안 테마 섞임 0

### 5.4 작업 순서

1. 6장 결정 → 이 문서 갱신
2. `decor.py` 작성 → 시안 `Docs/drafts/props/hall-decor-<지역>-draft.png` (gitignore, 지역마다 한 파일 덮어쓰기 · 이미지 안에 버전 표시). **남서 시험 구역(H1~H4)부터** — `tilepreview.py` 실제 타일 위에 광원과 함께 그림
   - **v01 (2026-10-07):** 6장 3 · 4 · 5 · 7은 제안대로 두고 만듦. 홀마다 ×2 확대. H1 블록 5개(7칸) · 벽걸이 2칸 · 데칼 17칸, H2 8개(15칸) · 6칸 · 15칸, H3 12개(13칸) · 3칸 · 29칸, H4 3개(4칸) · 7칸 · 13칸. 4곳 모두 문 사이 4u 길 연결 유지. 맵 전체로 계산하면 블록 179개 · 291칸(홀 바닥의 1.8%) · 데칼 441칸. 실행 `CHEST_POINTS=<상자 지점 덤프> python Docs/tools/halls/decor.py draft`
   - **v02 (2026-10-07, 사용자 피드백):** ① 블록 묶음 추가 — 홀 바닥 125칸당 1묶음, 존 문 앞 표식 묶음은 따로 셈 ② 벽걸이 조금 더 — 광원 포함 6u마다 ③ Z7 데칼은 재(`Decal_Ash`)만 (먼지 무늬가 어두운 홀에서 흰 구름처럼 보임). H1 블록 12개(18칸, 6.3%) · 벽걸이 2칸 · 데칼 24칸, H2 17개(30칸, 4.5%) · 8칸 · 14칸, H3 25개(33칸, 4.7%) · 13칸 · 27칸, H4 11개(20칸, 5.9%) · 7칸 · 6칸. 4곳 모두 길 연결 유지. 맵 전체 계산 블록 455개 · 760칸(4.6%) · 데칼 432칸
   - **v02 나머지 지역 (2026-10-07):** 지역마다 한 장 `Docs/drafts/props/hall-decor-<sw|se|center|nw>-draft.png` (홀마다 ×2, `decor.py draft all`). 17곳 모두 문 사이 4u 길 연결 유지

| 홀 | 바닥 | 블록 (칸, 비율) | 벽걸이 칸 | 데칼 칸 |
|---|---|---|---|---|
| H5 | 1,415 | 45개 (100칸, 7.1%) | 14 | 57 |
| H6 | 3,370 | 86개 (159칸, 4.7%) | 37 | 65 |
| H7 | 1,305 | 36개 (39칸, 3.0%) | 31 | 42 |
| H8 | 763 | 22개 (37칸, 4.8%) | 7 | 21 |
| H9 | 1,316 | 32개 (56칸, 4.3%) | 13 | 25 |
| H10 | 976 | 25개 (33칸, 3.4%) | 8 | 17 |
| HA | 767 | 18개 (34칸, 4.4%) | 8 | 21 |
| HB | 1,082 | 26개 (39칸, 3.6%) | 16 | 26 |
| HC | 476 | 10개 (11칸, 2.3%) | 6 | 5 |
| HD | 454 | 11개 (15칸, 3.3%) | 2 | 5 |
| HE | 360 | 15개 (22칸, 6.1%) | 2 | 10 |
| HF | 1,751 | 49개 (88칸, 5.0%) | 14 | 50 |
| HG | 580 | 15개 (26칸, 4.5%) | 14 | 17 |
   - **v03 (2026-10-07, 사용자 피드백):** ① Z5 블록에 석탄 상자 · 바위 조각(`RockSlab`, Z5 벽 지형에 쓰던 것) 추가 — 광차만 줄지어 서던 것 해소 (Z5 블록: 석탄 상자 8 · 바위 9 · 광차 4) ② Z8 약품 얼룩 무늬(`ZoneDecal_Chemical`) 뺌 — 작은 데칼만. 17곳 모두 길 연결 유지, 그 밖 수치는 v02와 거의 같음

3. 시안 승인 → 나머지 지역 3곳(남동 · 중앙 하수도 · 북서) 시안
4. `Halls_Props` 프리팹 생성 · 배치 → 칠하기 → 검사
   - **완료 (2026-10-07):** `decor.py check` → 씬 대조 972칸 문제 0 → `decor.py apply` → `_apply_hall_props.cs` (프리팹 만듦 · `Rooms` 끝에 원점 배치 · 씬 저장, 놓음 972, 씬 인스턴스 오버라이드 0) → `decor_next.json`을 `decor_prev.json`으로. 상자 지점 `ChestPointValidator.FindBlocked` 127곳 막힘 0. H1 씬 캡처가 시안과 같음
   - 씬 저장 때 유니티가 다른 인스턴스 값도 다시 씀: `Z7_GoblinCamp` · `Stage1ChestPoints`의 `m_ChunkCullingBounds` 오버라이드, 애니메이션 타일 `m_AnimationSpeed` 12 ↔ 12.000001 (장식과 무관)
5. 씬 캡처 (지역별) → 플레이로 길 막힘 · 시야 확인
6. 커밋 (작업 단위): ① 프리팹 · 배치 · 도구 ② `level-design.md`에 홀 장식 기록

---

## 6. 결정 사항

| # | 내용 | 상태 |
|---|---|---|
| 1 | 장식 범위 | **결정 (2026-10-07):** A + B안 — 충돌 없는 벽걸이 · 데칼 + 벽에 붙인 블록만 |
| 2 | 테마 | **결정 (2026-10-07):** 가까운 존의 무드 (광원과 같은 테마 지도) |
| 3 | 재료 | **결정:** 새 그림 없이 존 소품 + 공용 폐허 재사용, 존 대표 소품 제외 (3장 표) |
| 4 | 블록 충돌 층 | **결정:** `Block` (이동만 막음, 총알 통과) — B안의 목적(교전 공간 유지)에 맞춤. `Wall`로 하면 엄폐물이 됨 |
| 5 | 농도 · 수치 | **결정 (v02):** 4.1 기울기 (12u · 24u), 블록 묶음 125칸당 1 + 존 문 앞 표식, 벽걸이 광원 포함 6u마다 |
| 6 | Z7 광원 테마 | 제안: 장식은 고블린 캠프 소품. 광원(촛대)은 그대로 둘지, 캠프에 맞춰 `Torch_Wall` 등으로 바꿀지 |
| 7 | 시안 범위 | **완료:** 남서 → 나머지 3지역 (v03) |
