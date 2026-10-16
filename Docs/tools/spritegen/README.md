# spritegen — 던전 소품 · 지형 타일 픽셀 생성 툴킷

Python(Pillow) 코드로 픽셀을 직접 찍어 존 소품 시트와 바닥 · 벽 타일을 만든다. 구성은 다음과 같다.

- 그리는 도구: `bl.py`
- 시안 조립: `draftlib.py`
- 페인터 벽 규칙 재현: `painter_sim.py`
- Unity 임포트 파일 생성: `gen_zone.py`
- 검사 도구 세 가지: `check.py`, `verify_sheets.py`, `rehearse_import.py`

**먼저 읽을 것:** [STYLE.md](STYLE.md). 그림체, 규격, 충돌 규칙, 거부된 사례가 들어 있다. 품질은 코드보다 이 규칙을 지키는 데서 나온다.

## 설치

- Python 3.10 이상, `pip install pillow`
- 시안의 한글 라벨에는 한글 글꼴이 필요하다. Windows는 맑은 고딕, macOS는 Apple SD Gothic Neo, Linux는 Noto Sans CJK를 자동으로 찾는다. 다른 글꼴을 쓰려면 `SPRITEGEN_FONT=<ttf 경로>`를 지정한다.
- 저장소 위치는 자동으로 찾는다(이 폴더 기준 세 단계 위). 바꾸려면 `SPRITEGEN_REPO`를 지정한다.

## 먼저 확인할 것 (새 환경에서 툴킷이 제대로 도는지)

```
cd Docs/tools/spritegen
python verify_sheets.py        # 커밋된 시트 4종을 픽셀 하나까지 같게 다시 만드는지 → 모두 OK여야 함
python rehearse_import.py      # 임포트를 임시 폴더(_rehearsal/)에 해 보고 원본과 비교 → 모두 '전부 동일'이어야 함
```

## 폴더

| 경로 | 내용 |
|---|---|
| `paths.py` | 저장소 · 시트 경로, 글꼴, 쓰기 위치(`SPRITEGEN_WRITE_ROOT`) |
| `bl.py` | 캔버스 `Cv`(px · rect · line · ell · outline …), 공통 팔레트, 나무 · 돌 · 철 부품, 해골, `finalize`(패딩 + 접지 그림자) |
| `draftlib.py` | `make`(소품 항목), `sheet`(시트 조립), `draft`(시안 이미지), `dark_room`(게임 다크 바닥 · 벽 배경) |
| `painter_sim.py` | 던전 페인터 벽 규칙(`wall_tile`), Catacomb 4줄 벽 키트(`kit_tile`, `WALL_IDS`), 테스트 방 렌더(`render`) |
| `gen_zone.py` | 소품 시트 PNG · meta · Tile/AnimatedTile 에셋 생성. 다시 만들 때 guid를 재사용함 |
| `check.py` | 자동 검사(외곽선, 16px 배수, 충돌 칸, 평면 채색, 발광색, 벽 구멍, 애니메이션 프레임) |
| `known_issues.json` | 이미 승인 · 임포트된 시트의 알려진 문제. `check.py`는 이것을 KNOWN으로만 센다 |
| `zones/_template/` | **새 존은 여기서 시작**. 소품 2종 · 애니메이션 · 시안 · 임포트를 담은 최소 예시 |
| `zones/lab/` | Lab(Z8 · Z10) 바닥 · 벽(`lab.py`, `lab_draft.py`), 공용 가구 소품(`labprops.py`, `labprops_draft.py`). 지금 기준으로 가장 깔끔한 예시 |
| `zones/butchery/` | Z6 도축업장. `build.py`(시트, `import`로 임포트), `layout2.py` · `decal3.py`(방 배치 C# 생성) |
| `zones/ruins/` | 공용 폐허. `build_ruins.py`(시안), 임포트는 `python ../../gen_zone.py Ruins build_ruins` |
| `zones/mine/` | Z5 Mine 지형(`gen_mine_tiles.py`), 소품(`build_manavein.py [import]`), 바위(`rocks.py`), 거대 수정(`giant.py`), 배치(`layout_z5.py`) |
| `zones/goblincamp/` | Z7 고블린 캠프(옛 재봉소). 소품 `goblincamp_props.py [import]`(시트 `GoblinCamp`: 텐트 · 회의 테이블 · 모닥불 · 보급품 · 계단식 궤짝 탑 · 자루 더미 등), 배치 `layout_z7_camp.py`(검사 · 시안 · `place_z7_camp.cs` 생성 — 프리팹 `Z7_GoblinCamp` 생성 + 씬 인스턴스 교체). 씬 추출은 `unity/export_room_tiles_z7_camp.cs`, 팔레트는 `unity/goblincamp_palette.cs` |
| `zones/raidportal/` | 습격 웨이브 포탈(돌 문 + 소용돌이). `raid_portal.py`: 3D 상자를 광선으로 그려 정면 · 대각선 2방향, 테마 3종 — Blue(큰 · 작은, 등장 9 + 대기 6), Black(비활성: 어두운 소용돌이 + 바람에 날리는 입자, 작은, 대기 6 + 파랑→검정 전환 Fade 6), Green(적 습격용: 초록 + 코너 거미줄, 작은, 등장 9 + 대기 6). `python raid_portal.py <시안.png>`(파랑 시안), `--variants <png>`(검정 · 초록 시안), `import [Blue\|Black\|Green ...]`(타일 · 애니메이션 에셋 없이 시트 · meta만, 기본 Blue). Unity 쪽은 `unity/raid_portal_setup.cs`(THEME Blue \| Green: 클립 · 컨트롤러 · 프리팹, Green은 스포너에 연결)와 `unity/raid_portal_tp_setup.cs`(TP존 기둥을 파란 포탈로 교체 + 비활성 포탈 전환), `unity/exit_portal_setup.cs`(탈출 구역 A · B · C: 큰 검정 포탈 + 검정→파랑 점등 + 1픽셀 파란 테두리 + 선에서 올라오는 파티클, `--exit <png>` 시안) |
| `zones/lootchest/` | 루트 상자(LootChest 프리팹) 스프라이트: 2×1 묶음 3종 · 1×1 3종, 닫힘 · 열림. `python lootchest.py import`는 타일 에셋 없이 시트 · meta만 만든다(`gen_zone.main(..., tiles=False)`, 항목별 `pivot`) |
| `unity/` | Unity 에디터에서 `unity command eval_file`로 돌린 C# 예시(임포트 새로고침, 팔레트, 프리팹 배치)와 씬 비교 도구 |
| `vfx/` | 샷건 펠릿 연출(`pellet_vfx.py`: 궤적 텍스처 · 둥근 탄 머리 · 총구 섬광 · 착탄 · 부채꼴 빛, `--fade-gun-flash`로 shotgun.png 흰 섬광 20% 반투명). Unity 세팅은 `unity/pellet_vfx_setup.cs`(임포트 · 머티리얼 · 프리팹); 수류탄 · 섬광탄 폭발(`grenade_vfx.py`: 파편 불덩이 14프레임 · 바닥 충격 링 · 첫 번쩍 · 흙먼지 3종×4단계 · 그을음 · 불씨, 섬광 도넛 8 · 십자 7프레임. 인자 없이 시안 PNG, `--gif`는 GIF 2개, `import`는 `Assets/09.VFX/Consumable/{Grenade,Flash}/`에 시트 쓰기. Unity 세팅은 `unity/grenade_vfx_setup.cs`: 시트 슬라이스 · 머티리얼 · 프리팹 `GrenadeExplosion` · `FlashExplosion` · 수류탄 프리팹 `_vfx` 교체); 모루 타격 불똥(`anvil_spark_vfx.py`: 40×28 8프레임, 타격 지점이 칸 아래 가운데(피벗 0.5, 5/28). `--preview`로 시안, 인자 없이 `Assets/09.VFX/Shelter/AnvilSpark.png`에 쓰기. 슬라이스는 Unity에서 8칸 · PPU 16 · Point); 스턴 표시(`stun_vfx.py`: 별 2개 궤도 22×16 6프레임, `--compare`로 크기 비교 시안) · 피격 핏자국(`blood_vfx.py`: 8방향 × 변형 3종 × 8프레임, 회전 없이 방향마다 도트를 직접 찍음, `--preview`로 시안). 두 시트의 슬라이스 · 프리팹 연결은 `unity/blood_stun_import.cs`; 프레임 선택은 `Blood.cs`가 한다) |
| `anim/` | **기존 시트에 애니메이션 프레임을 덧붙이는 도구**. `anim_specs.py`(대상 · 효과 · 속도 목록) → `python anim/anim_add.py --all`(미리보기 `--preview 폴더`, 리허설 `SPRITEGEN_WRITE_ROOT`). 시트 PNG 맨 위에 프레임을 덧붙이고, 정적 Tile을 같은 guid의 AnimatedTile로 바꾼다. 시트를 `<존>_props.py import`로 다시 생성하면 사라지므로 다시 돌릴 것. 방 프리팹에 이미 놓인 타일은 `unity/refresh_anim_tiles.cs`로 애니메이션 데이터를 만든다(Unity에서 **도메인 리로드 뒤**에 실행 — 예전 정적 타일이 에디터에 캐시돼 있으면 등록이 안 됨). |

**Mine 스크립트 주의:** `mine2` → `mine3` → `mine5` → `mine6` 순으로 시안을 고쳐 온 흔적이다. `mine6`을 import하면 `mine2` · `mine3`의 함수를 최종판으로 덮어쓴다. 최종 결과는 `mine6` 기준이고, `mine2` · `mine3`만 단독으로 쓰면 옛 버전이 나온다. 고칠 때는 `mine6`에서 고친다.

## 작업 흐름 (존 하나)

1. **시안**
   - `zones/_template/example_props.py`를 `zones/<존>/`에 복사해 소품 함수를 그린다.
   - `python <스크립트> <출력.png>`로 시안을 만든다. 저장 위치는 `Docs/drafts/props/<이름>-draft.png` 한 파일이며, 버전은 이미지 제목에 쓴다.
2. **검사**
   - `python ../../check.py <스크립트>`의 오류가 0이어야 한다. 경고는 사람이 보고 판단한다.
   - 시안 이미지는 **확대해서 직접 본다**. 검사가 못 잡는 것이 많다: 무엇인지 안 읽히는 실루엣, 16px 반복, 시점 혼동, 다른 시트와 어긋난 톤.
3. **사용자 승인**: 피드백은 같은 파일의 버전을 올려 반영한다.
4. **임포트**(승인 후)
   - 먼저 `SPRITEGEN_WRITE_ROOT=<임시 폴더>`로 리허설한다.
   - 그다음 `python <스크립트> import` 또는 `gen_zone.main(...)`으로 실제 Assets에 쓴다.
   - Unity에서 새로고침하고 팔레트를 만든다(`unity/` 예시 참고).
   - 팔레트는 **칸 크기 수동(Manual) 1×1**로 만든다. 자동(Automatic)이면 팔레트 창이 첫 타일 스프라이트 크기로 칸을 다시 잡아, 첫 타일이 큰 소품일 때 칸이 커지고 소품이 작게 보인다(기관실 팔레트에서 실제로 겪음). 확인용: `python unity/render_palette.py <팔레트>`
5. **배치**
   - 방 프리팹 `Assets/03.Prefabs/Environment/Rooms/<방>.prefab`의 `<방> Wall / Block / Decor` 타일맵에 구역별로 놓는다.
   - 확인할 것: 소품 겹침 0, 문 통로 침범 0, 문 사이 이동 경로 존재, 상자 스폰 지점 침범 0.
6. **커밋**: 작업 단위로 나눈다. `asset`(시트 · 타일), `asset`(팔레트), `feat`(배치). 시안 이미지는 커밋하지 않는다.

## 외부 에이전트에게 맡길 때

**맡겨도 되는 것**
- 1~2단계: 시안 스크립트, 시안 PNG, 검사 결과까지.
- 임포트 리허설(`SPRITEGEN_WRITE_ROOT`).

**하면 안 되는 것** (이 저장소에서 실제로 사고가 났거나, 되돌리기 어려운 것)
- 승인 없이 `Assets/`에 쓰기. 임포트는 리허설 결과를 사람이 확인한 뒤에만 한다.
- 이미 임포트된 시트에서 기존 스프라이트의 **이름 · 순서를 바꾸거나 지우기**. guid · internalID는 이름으로 재사용되므로, 이름이 바뀌면 씬과 프리팹 참조가 끊긴다. 새 항목은 끝에 추가한다.
- 던전 레이아웃 페인터를 직접 실행하기. 페인터는 `unity command dungeon_paint`(check → paint)로만 돌리고, 손 변경이 있으면 멈춘다.
- 타일맵을 `ClearAllTiles` 후 통째로 다시 칠하기. 바꿀 칸만 `SetTile`로 교체한다(사용자가 손으로 놓은 배치가 유실된 적 있음).
- 고정 시드 난수로 guid 만들기. `gen_zone`의 SystemRandom + 저장소 충돌 검사를 쓴다.
- 프로젝트 루트의 `Temp/`(Unity 임시 폴더) 건드리기.

**넘겨줄 것**
- 이 폴더 전체
- 존 계획(소품 목록 · 구역 · 충돌 방침)
- 참고 이미지
- 기준 시트 경로(STYLE.md 첫 표)

## 알려진 문제

`known_issues.json`에는 이미 임포트된 시트의 외곽선 빠짐 등 38건이 기록되어 있다. 캔버스 가장자리에 닿은 기둥 · 깃발 · 바위 · 횃불 불꽃, 일부러 외곽선이 없는 벽 균열 · 반짝이 등이다. 보려면 `python check.py --all <스크립트>`. 고치면 시트 그림이 바뀌므로 사용자 확인 후 임포트한다.
