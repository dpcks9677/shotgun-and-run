# 샷건 마력 리워크 계획서 (외형 · 총구 섬광 · 펠릿 선)

> **작성 일자:** 2026-10-13  
> **상태:** 적용 완료(2026-10-13, 미커밋) — 시트 · 슬라이스 16~19 · Fire/PumpAction 클립(20fps), 섬광 6프레임 · 크리 시트 · 불티, 트레이서 6단계 분해 · 연기 · 크리 2px, 플레이 확인(셸터 씬 직접 재생). 결정 기록 10절 · 생성 도구 `Docs/tools/spritegen/weapon/shotgun_rework.py` · `vfx/magic_vfx.py` · `unity/shotgun_rework_apply.cs` · `muzzle_magic_import.cs` · `muzzle_magic_prefabs.cs`  
> **대상:** 샷건 스프라이트(손 포함) · 총구 섬광 `MuzzleFlash` · 펠릿 궤적 `BulletTracer` · 착탄 불꽃  
> **관련 문서:** [grenade-explosion-vfx-plan.md](grenade-explosion-vfx-plan.md) (연출 관례 · 불씨 파티클) / 연출 툴킷 `Docs/tools/spritegen/` (`vfx/pellet_vfx.py`)

---

## 1. 목표

- 무기 컨셉(던전 유물)에 맞게 샷건에 **신비한 마력 느낌**을 더한다. 외형은 레퍼런스(청회색 금속판 · 나무 개머리판 · 시안 룬 선 · 두꺼운 검은 외곽선)를 참고한다.
- **색은 기존 펠릿 팔레트(따뜻한 금색 계열) 하나로 통일한다.** 총구 섬광 · 불티 · 펠릿 선 · 착탄 불꽃이 같은 팔레트(3.1)를 쓴다. 총의 룬 색은 시안 C에서 금색 · 시안 두 안을 비교해 정한다. (2026-10-13 푸른 팔레트 시안 v01 → 기존 팔레트로 재작업 결정)
- 총구 섬광의 마법 느낌은 모양과 움직임으로 낸다.
  - **채택 ②**: 원뿔 불꽃 대신 기하학적인 모양(룬 고리 · 마름모 조각)
  - **채택 ③**: 위로 천천히 떠오르며 깜빡이는 불티
- 펠릿 궤적을 **도트 픽셀 선**으로 바꾼다. 판정 로직은 그대로 둔다.
- **크리티컬**은 채도를 높이고 탄 머리를 키워서 구분한다(3.2).

**제외:** 연기 → 반짝임 교체(①, ②에 흡수), ✦ 펠릿 머리(⑤), 보라 테두리(⑥), 펠릿 꼬리가 줄어드는 연출, 사격 판정 · 피해 · 탄 퍼짐 변경, 적 착탄의 피 색.

---

## 2. 현재 상태 (조사 결과)

| 대상 | 현재 | 파일 |
|---|---|---|
| 샷건 | 64×32 칸 시트, 총 길이 약 30px, **손이 프레임마다 그려져 있음**. 대기 · 사격 반동 · 펌프 · 장전 프레임 + 손 없는 `shotgun_15` | [shotgun.png](../Assets/04.Sprites/Weapon/shotgun.png) |
| 같은 총이 그려진 그림 | 튜토리얼 진열대, 대시 스프라이트 | `TutorialShotgunDisplay.png` · `Swordsman_shotgun1.png` |
| 총구 섬광 | 16×16 3프레임, 30fps(약 0.1초): 원뿔 불꽃 → 작은 불꽃 → **회색 연기**. 가산 부채꼴 빛 `Glow`(주황, 0.08초, 크리티컬은 색 변경 + 1.5배). 재생이 끝나면 **루트 오브젝트를 끈다** | [MuzzleFlash.cs](../Assets/02.Scripts/Weapon/Presentation/MuzzleFlash.cs) · `09.VFX/Weapon/Pellet/MuzzleFlash.prefab` |
| 섬광 생성 | 총구(`_muzzle`) 아래에 하나만 만들어 두고 총과 같이 회전, 발사마다 다시 재생 | [WeaponVFXFeedback.cs](../Assets/02.Scripts/Weapon/Presentation/WeaponVFXFeedback.cs) |
| 펠릿 판정 | 레이캐스트 히트스캔 → `pool.SpawnTracer(origin, endPoint, impactKind)`. 크리티컬 여부는 `ShotContext.IsCritical`에 있지만 연출까지 전달되지 않음 | [HitscanPellet.cs](../Assets/02.Scripts/Weapon/ProjectTile/HitscanPellet.cs) · [ProjectTilePool.cs](../Assets/02.Scripts/Weapon/ProjectTile/ProjectTilePool.cs) |
| 펠릿 연출 | `LineRenderer`(흰색, 폭 0.08→0.24) + 머리 알갱이 `PelletHead.png`(5×5). 머리 100u/s, 꼬리 1.04u, 도착 뒤 0.08초 페이드 | [BulletTracer.cs](../Assets/02.Scripts/Weapon/ProjectTile/BulletTracer.cs) |
| 도트 선 | `PixelLineRenderer`: 점 목록을 1/16u 격자 Bresenham 선으로 텍스처에 그림(흰색, 색은 렌더러 틴트). **`Draw()`마다 텍스처 전체를 지우고 다시 올림** | [PixelLineRenderer.cs](../Assets/02.Scripts/Consumable/Presentation/PixelLineRenderer.cs) (`Consumable` 어셈블리, 사용처: `ExplosionRangeIndicator` · `ExplosionIndicator` · `ThrowGrenade`) |
| 펠릿 팔레트 | `FL_W #fff0a0` · `FL_Y #ffc319` · `FL_O #e58a00` · `FL_R #b84206` · 흰색 | `Docs/tools/spritegen/vfx/pellet_vfx.py` |
| 불씨 파티클 선례 | 수류탄 `Embers`: `GrenadeSpark.png`(3×3) + `M_GrenadeSpark`(가산) `ParticleSystem` | `grenade_vfx_setup.cs` |

연출 관례(PPU 16 · Point · 가산 셰이더 `ShotgunAndRun/Sprite Additive` · Python으로 시트 생성 → Unity C#으로 임포트)는 [grenade-explosion-vfx-plan.md 2.3](grenade-explosion-vfx-plan.md)을 그대로 따른다.

---

## 3. 공통 결정

### 3.1 팔레트 — 기존 펠릿 팔레트 유지

| 이름 | 색 | 용도 |
|---|---|---|
| `WHITE` | `#ffffff` | 핵 · 가장 밝은 점 |
| `FL_W` | `#fff0a0` | 연노랑, 핵 둘레 |
| `FL_Y` | `#ffc319` | 금색, 주 색 (고리 · 선) |
| `FL_O` | `#e58a00` | 주황, 바깥 · 흩어지는 점 |
| `FL_R` | `#b84206` | 짙은 적갈색, 마지막 잔점 |

- `pellet_vfx.py`의 `FL_*` 상수를 그대로 쓴다. 새 색을 만들지 않는다.
- 바닥 빛 `Glow`의 색 · 착탄 효과 색은 지금 그대로다.
- (기록) 푸른 마력 팔레트(`#4cd8ff` 계열)로 만든 시안 v01은 기각했다.

### 3.2 크리티컬 연출

| 요소 | 일반 | 크리티컬 |
|---|---|---|
| 채도 | 위 팔레트 그대로 | 채도를 올림. 섬광은 전용 시트 또는 덜 어두운 틴트(시안 v02에서 비교), 불티 · 펠릿 선은 틴트 |
| 탄 머리 | `PelletHead` 5×5 | **7×7 크리티컬 머리 스프라이트** (스케일이 아니라 따로 그려서 도트 격자 유지) |
| 바닥 빛 | 기존 | 기존 크리티컬 처리(색 · 1.5배) 유지 |

- 틴트 곱셈으로 채도가 충분히 오르지 않으면 크리티컬 전용 시트로 바꾼다 (시안에서 비교).
- 크리티컬 여부는 사격 단위로 정해지므로(`ShotContext.IsCritical`), 같은 사격의 펠릿은 모두 같은 연출이다.

---

## 4. 작업 단위 · 병렬 진행

| # | 단위 | 담당 | 시안 승인 | 비고 |
|---|---|---|---|---|
| A | 총구 섬광 · 불티 · 탄 머리 · 착탄 불꽃 시안 | 하위 에이전트 (그림) | 예 | 결과물: `Docs/drafts/props/muzzle-magic-draft.png` |
| B | 코드: 도트 선 트레이서 · 크리티컬 전달 · 섬광 불티 방출 | 하위 에이전트 (구현) | 아니오 | 스크립트만. 프리팹 · 씬은 메인이 처리 |
| C | 샷건 외형 시안 (손 포함) | 하위 에이전트 (그림) | 예 | 결과물: `Docs/drafts/props/shotgun-rework-draft.png` |
| D | 프리팹 연결 · 임포트 · 플레이 확인 | 메인 | — | A · C 승인과 B 완료 뒤 순차 진행 |

- A · B · C는 건드리는 파일이 겹치지 않아 동시에 진행한다. 에디터 상태를 바꾸는 작업(프리팹 · 임포트 설정)은 D에서 메인이 순서대로 한다.
- 커밋 · PR은 단위별로 나눈다 (섬광 연출 / 펠릿 도트 선 / 샷건 외형).

---

## 5. A — 총구 섬광 마력화 (시안)

### 5.1 ② 기하학적 모양 (프레임 시트)

총구는 총과 같이 회전하므로, 시트는 지금처럼 **총구가 왼쪽 가운데(피벗), +x로 뻗는 방향**으로 그린다.

| 프레임 | 내용 |
|---|---|
| 0 | 총구 앞 밝은 마름모 핵(`WHITE` · `FL_W`) + 세로로 납작한 작은 룬 고리(약 3×9, `FL_Y`)가 나타남 |
| 1 | 고리가 커짐(약 5×12). 고리 위아래 · 앞쪽에 마름모 조각 3개(`FL_Y` · `FL_O`) |
| 2 | 고리가 끊어진 점선으로 흩어짐(`FL_O` · `FL_R`), 핵은 사라짐 |
| 3 | 남은 점 몇 개만 (`FL_R`) → 끝 |

- 칸 크기 16×16 유지, 3 → 4프레임(30fps 기준 약 0.13초). **회색 연기 프레임은 없앤다.**
- `pellet_vfx.py`의 `star()` · `put_sym()` 등 기존 함수를 재사용한다.

### 5.2 ③ 떠오르는 불티

- **월드 좌표로 시뮬레이션**한다 (총구 회전과 무관하게 항상 화면 위로 떠오름).
- 발사당 3~4개, 수명 0.4~0.7초, 초기 속도 거의 0, 월드 y +0.6~1.0u/s, 약한 `Noise`로 좌우 흔들림.
- 색: `FL_W` → `FL_Y` → `FL_O`, 알파 키를 2~3번 오르내려 **깜빡임**.
- 스프라이트: `GrenadeSpark.png`(3×3) 형태 + 1px 점 (기존 팔레트). 머티리얼은 가산(`M_GrenadeSpark`와 같은 방식).

### 5.3 탄 머리 · 착탄 불꽃

- `PelletHead` 5×5(일반)는 그대로 두고, 7×7(크리티컬)만 같은 팔레트로 새로 그린다.
- 착탄 효과(`ImpactWall` · `ImpactEnemy`)는 이미 기존 팔레트라 바꾸지 않는다.

### 5.4 시안 구성

`Docs/drafts/props/muzzle-magic-draft.png` 한 파일 (버전은 이미지 안에 표기, 커밋하지 않음):
섬광 4프레임 확대본(일반 · 크리티컬 틴트) / 탄 머리 일반 · 크리티컬 / 착탄 불꽃 / 어두운 바닥 위에 총 · 섬광 · 선 · 불티를 합성한 장면.

---

## 6. B — 코드 변경

### 6.1 `PixelLineRenderer` 이동

- `Assets/02.Scripts/Consumable/Presentation/` → `Assets/02.Scripts/Combat/Presentation/`, 네임스페이스 `ShotgunAndRun.Combat`. Weapon · Consumable · Player가 모두 참조하는 바닥 어셈블리라 순환이 없다.
- `.cs`와 `.meta`를 함께 옮겨 GUID를 유지한다 (수류탄 · 플레이어 프리팹 참조 유지). 사용처 세 곳의 `using`만 고친다.

### 6.2 `BulletTracer` 직선 도트 선 + 탄 머리

- `LineRenderer` 대신 자식 `PixelLineRenderer`를 쓴다. `Play()`에서 루트를 시작점에 두고 **직선(총구 → 착탄점)을 한 번만 그린다**.
- 탄 머리(`_head`)는 지금처럼 선을 따라 이동한다(`_headSpeed` · `_speedJitter`). 도착하면 착탄 효과를 내고, 선 · 머리 알파를 `_fadeDuration` 동안 줄인 뒤 풀로 돌려보낸다.
- 꼬리 길이 · 폭 관련 필드는 없앤다.
- 크리티컬: 머리 스프라이트를 7×7 크리티컬용으로 바꾸고, 선 · 머리에 크리티컬 틴트를 곱한다.
- (기록) 탄 머리를 없애고 번개 지그재그로 바꾸는 안은 시험 뒤 폐기하고 이 방식으로 되돌림 (2026-10-13).

### 6.3 크리티컬 전달

- `HitscanPellet.Fire` → `pool.SpawnTracer(origin, endPoint, impactKind, context.IsCritical)` → `BulletTracer.Play(...)`.

### 6.4 `MuzzleFlash` 불티 · 크리티컬

- `ParticleSystem` 참조를 추가하고 `Play()`에서 `Emit`. 크리티컬이면 방출 색에 크리티컬 틴트를 곱한다.
- 프레임 재생이 끝나면 루트를 끄지 않고 섬광 렌더러만 숨기고 `Update`를 멈춘다 (불티가 같이 사라지지 않도록).
- 크리티컬이면 섬광 프레임 렌더러에도 크리티컬 틴트를 곱한다.

---

## 7. C — 샷건 외형 (시안)

- **v02 방향 (2026-10-13)**: v01 형태는 확정. 색 배치만 두 번째 레퍼런스(유물 총: 바랜 황동 · 청동 금속과 금색 조각, 짙은 고목 개머리판, 총열 앞쪽이 갈라진 용암처럼 달아오름)를 따른다. 총열 발광은 기존 펠릿 팔레트(`FL_R` → `FL_Y` → `WHITE`)를 써서 섬광 · 펠릿과 이어지게 하고, 이것이 룬을 대신한다. 실루엣 높이는 플레이어 눈을 가리지 않도록 v01보다 1px 낮춘다.
- 레퍼런스의 형태 언어를 **지금 크기와 비슷하게(약 30px, ±2px)** 옮긴다: 두꺼운 검은 외곽선, 청회색 금속판 마디, 나무 개머리판, **룬 선** (금색 `FL_Y` 계열 안과 시안 안을 시안에서 비교).
- 시트 배치 · 칸 크기 · 슬라이스 이름(`shotgun_0`…`shotgun_15`)은 그대로 두고 픽셀만 바꾼다 (Animator 클립 참조 유지).
- 손은 모든 프레임에 다시 그린다. 기존 프레임의 손 픽셀 위치를 따른다. 손 없는 `shotgun_15`도 같은 외형으로.
- 시안 `Docs/drafts/props/shotgun-rework-draft.png`: 대기 · 사격 · 펌프 3프레임(손 포함) 확대본 + 현재 총과 나란히 비교 + 어두운 바닥 위 캐릭터와 겹친 합성.
- 승인 뒤 전체 프레임 → 같은 PNG에 덮어쓰기 → 총구 위치(`Muzzle`) 확인. 튜토리얼 진열대 · 대시 스프라이트 반영은 승인 뒤 정한다.
- 어두운 홀에서 룬이 묻히면 작은 시안 `Light2D`나 가산 글로우를 붙인다 (플레이 확인 뒤 판단).

---

## 8. 변경 범위 요약

| 파일 | A | B | C | D |
|---|---|---|---|---|
| `Docs/tools/spritegen/vfx/` 시안 · 시트 생성 | ✔ | | ✔ | |
| `PixelLineRenderer.cs` (Consumable → Combat) · 사용처 `using` 3곳 | | ✔ | | |
| `BulletTracer.cs` · `ProjectTilePool.cs` · `HitscanPellet.cs` | | ✔ | | |
| `MuzzleFlash.cs` | | ✔ | | |
| `MuzzleFlash.png` · `PelletHead.png` · `ImpactWall.png` · `ImpactEnemy.png` 덮어쓰기 | | | | ✔ |
| `MuzzleFlash.prefab` (불티 자식 · 색 값) · `BulletTracer.prefab` (선 자식 교체) | | | | ✔ |
| `shotgun.png` 덮어쓰기 · 무기 `Muzzle` 위치 | | | | ✔ |

**건드리지 않는 것:** 판정 · 피해 로직, `ProjectTilePool`의 풀 구조, 무기 Animator 클립.

---

## 9. 검증

- **A · D**: 8방향 조준에서 불티가 항상 화면 위로 떠오르는지, 연사 중 섬광 재시작과 이전 불티가 자연스럽게 겹치는지, 어두운 홀과 밝은 방 모두에서 보이는지, 크리티컬이 한눈에 구분되는지 확인한다.
- **B**: 컴파일 오류 없음. 사거리 끝 · 벽 · 적 · 관통 명중에서 선 끝과 착탄 위치가 맞는지, 연사 중 프레임 저하가 없는지(`Texture2D.Apply` 확인), 수류탄 범위 원 · 투척 궤적이 이동 뒤에도 그대로 그려지는지 확인한다.
- **C · D**: 모든 애니에서 손이 캐릭터 손 위치와 맞는지, 좌우 반전 시 총구와 섬광 위치가 맞는지 확인한다.

---

## 10. 결정 기록 (2026-10-13)

1. 총 크기: 지금 크기와 비슷하게.
2. 색: 최대한 통합. 처음엔 푸른 빛으로 정했으나 시안 v01을 보고 **기존 펠릿 팔레트로 재작업**하기로 변경.
3. 펠릿 꼬리가 줄어드는 연출: 포기.
4. 크리티컬: 채도를 높이고 탄 머리를 키움 
5. 진행: 하위 에이전트로 A · B · C 병렬 진행, 에디터 작업은 메인.
6. 펠릿: 탄 머리 삭제 + 번개 지그재그 안을 시험했으나 **폐기, 직선 선 + 탄 머리로 롤백**.
7. (2026-10-13) 탄 머리(구체) 이펙트 **삭제**. 첫 레퍼런스 gif의 발사 섬광처럼 펠릿 선이 여러 갈래로 뻗었다가 **대시 조각으로 부서지며**(색 단계 변화 + 어두운 연기 대시) 사라지는 연출로 바꾼다. 시안 v04 승인 뒤 6.2 · 3.2를 다시 쓴다.
8. (2026-10-13) 시안 v04 확정: 총구 섬광은 갈래 선 없이 링 + 핵, 크리티컬은 (a+) 섬광 + 펠릿 선 t0 · t1 **2px 두께**, 연기 대시는 어두운 단색 `#0e0b12`. 분해 · 연기 규칙은 `Docs/tools/spritegen/vfx/magic_vfx.py`의 `LINE_STAGES` · `SMOKE_STAGES`. GIF 목업 확인 뒤 6.2를 다시 쓰고 코드에 옮긴다.
9. (2026-10-13) 샷건: 사용자가 v03 굴곡 약 안(`Docs/drafts/props/shotgun-v03w-*.png`)을 직접 손으로 수정해 줄 예정.
