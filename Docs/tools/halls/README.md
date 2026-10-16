# 홀 생성 도구 (level-design 1.5)

존 사이의 "통로 + 작은 방" 사슬을 울퉁불퉁한 벽선의 홀로 바꾸는 작업 스크립트. v16 남서 시험 구역(H1~H4)을 만들 때 썼다.

| 파일 | 하는 일 |
|---|---|
| `v16.py` | v15 레이아웃 → v16 시험 구역 배치(방 · 통로 · 홀 사각형 · 문) + 면적 · 시야 노출 검사 (`python v16.py check <asset>`) |
| `halls.py` | 첫 벽선 생성기 (2u 격자 갉기, 벽 타일 규칙 검사). `carve` · `cells`를 다른 스크립트가 씀 |
| `halls3.py` | 현재 생성기: 계단식 벽선(`carve2`) + 가르는 기둥 + 데드존 검사. `FIXED`(사용자가 고른 모양 고정) · `KEEP_OLD` · `MANUAL`(부분 손질) |
| `h2fix.py` | H2: 시드 877 벽선 + 윗면 기준 정사각 기둥(바닥 6×9) 2개 자리 찾기 → `halls_blocks_H2.json` |
| `tilepreview.py` | DungeonDark 타일 + 페인터 벽 규칙(북쪽 벽 3줄)으로 홀 미리보기 렌더 |
| `draw16.py` · `center.py` | 도형 초안 · 비교 이미지, 존 중심 거리 |
| `halls_blocks*.json` | 홀별 벽 덩어리 (x, y, w, h). `halls_blocks_H3_rieul.json` = H3 ㄹ자 고정 모양 |
| `layout_edit.py` · `region1.py` | 지역 적용: 에셋 편집(방 · 통로 · 덩어리), 여러 사각형 홀 벽선 · 칸막이 섹션 · 안뜰 · 검사, 미리보기 |
| `region2.py` · `region2_water.py` | 지역 2(중앙 · 하수도): 수로 · 해자 · 쌍굴 홀, Catacomb 미리보기 / I · J 물 · 쇠창살 · 물가 마감(Z3 리워크 v2-A 규칙)을 Z3_Sewer 프리팹에 칠하는 C# 생성 (`_paint_region2_water.cs`) |
| `region3.py` | 지역 3(북서): 십자 광장 · ㄹ자 · 모래시계 · H자 · 쌍굴 홀, 기본 · Catacomb 타일 미리보기. 벽선은 `roughen_union(strict=True)` — 겹친 다른 사각형 안으로 들어가는 덩어리는 버림 |
| `_quickmap.py` · `_expo3.py` | 바닥을 이름별 색으로 빠르게 그림 / 두 에셋의 시야 노출 비교 (새로 생긴 쌍만 NEW) |
| `_audit_chest_points.cs` · `_audit_chest.py` | 상자 지점 전체 점검: 바닥 위인지, 벽 · 물 충돌 · 방 소품(Wall · Block 타일맵, 스프라이트 충돌 범위)에 막히지 않는지, 존 지점이 존 안인지, 구간 지점이 두 존 사이 경로에서 얼마나 벗어나는지(우회 40u 초과 표시), 스폰 20u · 탈출구 10u 안인지, 지점 수 > 최대 생성 수인지. (에디터에서는 Physics2D 겹침 검사가 벽도 못 잡아서 타일맵으로 셈) |
| `_dump_chest_cells.cs` · `_plan_connector_wall.py` · `_apply_connector_wall.cs` | 커넥터(홀 · R7) 상자를 벽 앞으로 옮김 (level-design 4장 벽 앞 규칙): 칸 지도 덤프 → 계획(`Backups/chest_wall_plan.txt`, `why <x> <y>`로 후보가 걸린 조건 보기) → 프리팹에 적용(바꿀 칸만 SetTile) |
| `lights.py` | 홀 광원 (`Docs/hall-lighting-plan.md`): 가까운 존 테마 계산 · 벽걸이 · 바닥 광원 자리 · 밝기 검사. `draft` = 시안 `Docs/drafts/props/hall-lights-draft.png`, `apply` = `_apply_hall_lights.template.cs`로 `_apply_hall_lights.cs`(생성물, 커밋 안 함) + `lights_next.json`. 상자 지점은 `CHEST_POINTS=<_dump_chest_points.cs 결과>`로 넘김 |
| `lights_more.py` | 홀 · 통로 · 존 · Z3 광원 추가 (2026-10-14, 전역 빛 0.12 뒤 "조명 부족" 피드백): 기존 `lights_prev.json`은 그대로 두고, 구역마다 반경 안 칸 40%가 되도록 벽걸이(Decor) · 바닥 광원(Block)을 탐욕으로 더함. 존은 벽걸이만 · 방 소품 칸 피함 · Z3는 벽 랜턴 몇 개만. `draft` = 시안 `Docs/drafts/props/hall-lights-more-draft.png`, `apply` = `_apply_hall_lights.template.cs`로 `_apply_hall_lights.cs` + `lights_next.json` (check는 생성된 C#의 `MODE`를 "check"로 바꿔 실행, 씬을 Additive로 열어야 하면 앞뒤에 OpenScene · SetActiveScene · CloseScene을 감쌈). 입력: `CHEST_POINTS`, `Backups/lights_dump.txt`(`_dump_lights.cs`), `Backups/zone_tiles.txt`(`_dump_zone_tiles.cs`) |
| `_dump_lights.cs` · `_dump_zone_tiles.cs` | 읽기 전용: 씬의 모든 타일 광원 위치 · 반경(DungeonStageScene을 Additive로 열고 바로 닫음) / 존 방 프리팹이 차지한 칸 → `Backups/` |
| `lights_prev.json` | 지금 `Rooms/Halls_Decor` 프리팹에 칠해진 광원 배치. apply는 프리팹이 이것과 정확히 같을 때만 바꿈 (손 수정 보호) → 성공하면 `lights_next.json`을 이 이름으로 바꿈 |
| `decor.py` | 홀 공용 장식 (`Docs/hall-decor-plan.md`): 광원과 같은 테마 지도 + 존 문 거리 농도로 벽걸이 · 벽 붙임 블록 · 데칼 자리, 길 연결 · 상자 · 광원 여백 검사. `draft [all\|sw\|se\|center\|nw]` = 시안 `Docs/drafts/props/hall-decor-<지역>-draft.png`, `check` · `apply` = `_apply_hall_props.template.cs`로 `_apply_hall_props.cs`(생성물, 커밋 안 함) + `decor_next.json`. 그림은 `lights.render(under=)`에 끼워 광원과 함께 그림. 상자 지점은 `CHEST_POINTS`로 넘김 |
| `decor_prev.json` | 지금 `Rooms/Halls_Props` 프리팹에 칠해진 장식 배치. apply는 프리팹이 이것과 정확히 같을 때만 바꿈 (손 수정 보호) → 성공하면 `decor_next.json`을 이 이름으로 바꿈. **주의:** 프리팹 `Halls_Props Wall`의 무너진 계단 10개(71c13994, decor.py 밖에서 칠함)는 이 파일에 없어서 지금 그대로 apply하면 "다른 칸 10"으로 멈춤 (계단을 지우지 않게 일부러 넣지 않음). v21(2026-10-14) 적용 때는 생성된 C#의 PREV · NEW에만 계단을 더하고 Wall 층 검사를 빼서 칠함 |
| `_lights_report.cs` · `_lights_check.cs` · `_reopen_check_halls.cs` | 빛 오브젝트 구조(`__Lights/<방>`) · 동작 검사, 씬 다시 열고 Halls_Decor 타일 · 빛 수 확인 |
| `_*.cs` | `unity command eval_file`용: 레이아웃 검증, 상자 지점 조회 · 이동, 씬 타일맵 덤프 · 되돌림. **씬을 고치는 스크립트는 열린 씬이 DungeonStageScene이 아니면 중단** (2026-10-06 ShelterScene을 덮어쓴 사고 뒤 추가) |

주의
- 스크립트는 처음에 세션 임시 폴더에서 돌렸던 것을 옮겨 왔다. `v16.py build`는 **v15 레이아웃을 v16으로 바꾸는 일회성 변환**이고 `SRC`는 같은 폴더의 `Stage1Layout_v15_backup.asset`(커밋 안 함)을 읽는다. 이미 v16이 적용된 `Stage1Layout.asset`을 `SRC`로 쓰면 홀이 중복된다 — 원본은 `git show 58f86069:Assets/_Sandbox/Yechann/Layouts/Stage1Layout.asset > Stage1Layout_v15_backup.asset`으로 꺼낸다.
- H1~H4는 모두 `halls3.py` `FIXED`로 고정(사용자가 고른 모양). 문 위치를 바꿔도 모양이 다시 생성되지 않는다.
- **Z3 하수도 주의:** 페인터는 방 프리팹의 Water · Plate · Pond Frame · Floor 칸을 바닥으로 보므로, Z3_Sewer의 북쪽 벽 밑 채움(y148, wall_decor2.py)이 있으면 Z3 북쪽 벽을 한 줄 올린다(I 수로 끝 아치 벽 y136 x144~149의 물 채움도 같음). 칠할 때는 `_z3_fill148.cs`를 `__MODE__` = `out`으로 실행(채움을 `Backups/z3_fill148.txt`로 빼 둠) → `dungeon_paint paint` → `in`으로 실행(벽 맨 아래 타일 86 · 82 칸에만 다시 넣음) → 칠하기 전 덤프와 비교해 Z3 방 바닥 무늬가 바뀐 칸은 `_restore_cells.cs`로 되돌림 → `dungeon_paint baseline`. 페인터는 칠할 때마다 맵 전체 바닥 무늬 · 얼룩을 다시 뽑으므로, 바꾼 범위 밖의 바닥 그림은 칠하기 전 덤프로 모두 되돌린다(다른 사람이 손본 R7 비밀통로 덮개 등과 어긋나지 않게). (예전 방식 `_restore_region.cs`는 x190~250 · y140~160을 통째로 되돌려서, 그 범위에 레이아웃 변경이 있으면 새 칸까지 덮는다 — 지역 2 적용 때 K 홀 아래 · I–Z3 통로를 덮어 다시 칠함)
- 칠하기 순서(v16 적용 때): 덤프 → `dungeon_paint baseline`(Catacomb 벽 발치 바닥 490칸은 페인터 규칙과 같은 결과) → `paint` → Z3 되돌림 → `baseline` → 덤프 비교(시험 구역 밖 구조 변경 0 확인).
- 결과물(`Stage1Layout_v16.asset`, PNG)은 이 폴더에 생기니 커밋하지 않는다.
- 생성 규칙(벽선 · 기둥 · 통로)은 `Docs/level-design.md` 1.5에 정리.
- 통로 끝점은 양쪽 방 **안**에 둔다 (`measure_stage_distance.py`가 통로 중심선이 지나는 방으로 연결을 읽음). 이 도구는 겹치거나 맞닿은 방 사각형(여러 사각형 홀)을 한 방으로 묶는다.
