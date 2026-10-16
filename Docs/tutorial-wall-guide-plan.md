# 튜토리얼 조작 안내 벽 플로팅 전환 계획서

> **작성 일자:** 2026-10-13  
> **상태:** 구현 완료(2026-10-13, 미커밋) · 결과와 남은 확인은 9절  
> **대상:** `TutorialScene`의 조작 안내(이동 · 대시 · 줍기 · 발사 · 장전 · 상자 · 회복 · 남은 적 수)  
> **관련 문서:** [tutorial-review-supplement-plan.md](tutorial-review-supplement-plan.md) (안내가 보조 안내로 분리된 경위) / 연출 툴킷 `Docs/tools/spritegen/` (README · STYLE · check)

---

## 1. 목표

레퍼런스처럼 조작 안내가 **화면 위 팝업이 아니라 배경 벽 위에 떠 있는 월드 오브젝트**로 보이게 한다.

레퍼런스에서 따올 점

| 요소 | 레퍼런스 | 지금 |
|---|---|---|
| 위치 | 해당 지점의 **배경 벽**에 고정. 카메라가 움직이면 같이 스크롤됨 | 화면 상단 중앙 고정 (오버레이 캔버스) |
| 배경 | 없음. 글자만 벽 위에 얹힘 | 검은 반투명 판(알파 0.72, 1100×110) |
| 글자 | 도트 폰트 + 어두운 외곽선, 작은 크기 | 42pt 흰 글자, 외곽선 없음 |
| 키 표시 | 문구 `점프 :` 옆에 **키캡 그림**(`SPACE`) | 글자로만 (`W A S D 로 이동`) |

**하지 않는 것:** 안내가 뜨는 시점과 사라지는 조건(진행 흐름), 안내 문구의 내용, 튜토리얼 외 씬의 UI. 키 재설정 기능은 프로젝트에 없으므로(조사 결과) 키캡은 고정 그림으로 둔다.

---

## 2. 현재 상태 (조사 결과)

| 대상 | 내용 | 파일 |
|---|---|---|
| 안내 표시 | `TutorialPopup`: `_panel`(GameObject) · `_text`(`TMP_Text`) · `_blackout`(Image). `Show(string)` / `Hide()` / `SetBlackout(bool)`. 한 번에 문구 하나만 | [TutorialPopup.cs](../Assets/02.Scripts/Tutorial/TutorialPopup.cs) |
| 프리팹 | 루트가 오버레이 캔버스(1920×1080 기준, 정렬 100). 자식 `Panel`(상단 −120, 검은 판) → `Text`(Mulmaru SDF 42pt), 자식 `Blackout`(전체 검정) | [TutorialPopup.prefab](../Assets/03.Prefabs/Tutorial/TutorialPopup.prefab) |
| 호출처 | `TutorialFlow`가 `_popup.Show/Hide`를 약 12곳에서 직접 호출, 보조 안내용 `SetGuide(message)`가 한 번 더 감쌈 | [TutorialFlow.cs](../Assets/02.Scripts/Tutorial/TutorialFlow.cs) |
| 문구 | `_moveGuide` 등 9개가 **직렬화 문자열**. 값이 `TutorialScene`에 저장돼 있어 코드 기본값만 바꿔서는 안 바뀜 | `TutorialFlow` · `TutorialScene.unity` |
| 키 등록 방식 | `TutorialFlow.Resolve()`가 `CutsceneMarker` · `TutorialZone`을 키로 모아 딕셔너리에 담고, 하나라도 없으면 에러로 시작을 막음. 새 안내 오브젝트도 이 방식을 따른다 | `TutorialFlow.Resolve` |
| 구역 | `TutorialZone`(키 · 트리거). Move · Dash(이번 작업에서 삭제) · OctagonEntry · ChestSight · Loot · ArenaEntry | [TutorialZone.cs](../Assets/02.Scripts/Tutorial/TutorialZone.cs) |
| 시간 정지 | 발사 안내(`ShootGuide`)는 `Time.timeScale = 0` 동안 표시됨 → 안내 쪽 연출이 있다면 `unscaledTime`을 써야 함(`FloatingDisplay` 선례) | `TutorialFlow.ShootGuide` |
| 카메라 | 정사영 크기 8.4375 → 세로 16.875유닛. PPU 16이면 270픽셀 → 1080p에서 4배 확대로 보임 | `Main Camera` |
| 폰트 | `Mulmaru SDF`(Static, 48pt). 공용 머티리얼이라 **외곽선을 건드리면 다른 UI도 바뀜** | `TextMesh Pro/Resources/Fonts & Materials` |
| 정렬 레이어 | Default · Background · Wall · GroundProps · Environment · Blood · … · Characters … (TagManager는 에디터 저장 사고 전력이 있어 **레이어를 새로 추가하지 않는다**) | `ProjectSettings/TagManager.asset` |
| 안내가 필요한 지점 | 아래 3절 표 | |

---

## 3. 안내 분류와 위치

모든 안내를 하나의 월드 표시기로 보여 주되, 위치는 두 가지로 나눈다.

| 안내 | 표시 시점 | 위치 | 마커 키(새로 둠) |
|---|---|---|---|
| 이동 `W A S D` | **상시 표시**(씬에 놓인 오브젝트) | **복도 1** 벽, 시작 쪽 | 없음(흐름이 다루지 않음) |
| 대시 `우클릭` | **상시 표시** | **복도 1** 벽, 이동 문구보다 안쪽(오른쪽) | 없음(흐름이 다루지 않음) |
| 샷건 줍기 `E` | ShotgunPickup 단계 | 진열대 뒤 벽 | `Pickup` |
| 발사 `좌클릭` | 시간 정지 동안 | **플레이어 머리 위 공중**(12시 방향) | `Player` |
| 장전 `R` | 장전 필요 시 | 플레이어 머리 위 | `Player` 공용 |
| 상자 열기 `E 길게` · 담기 `더블 클릭` | Loot 구역 안 | 상자 뒤 벽 | `Loot` |
| 회복 `1~4` + `좌클릭` | HealGuide 단계 | 플레이어 머리 위 | `Player` 공용 |
| 남은 적 n마리 | 보라 방 25초 경과 | 플레이어 머리 위(방 안 어디서든 보여야 함) | `Player` 공용 |

- **상시 표시 안내(이동 · 대시):** 벽에 붙은 장식 오브젝트처럼 **계속 떠 있는다.** 흐름이 켜고 끄지 않으므로 `TutorialFlow`와 무관하다. 문구는 씬 오브젝트의 텍스트에 직접 쓴다.
- **흐름이 켜고 끄는 벽 안내(줍기 · 상자):** 위치마다 `TutorialWallGuide`를 씬에 하나씩 둔다. `TutorialFlow`는 `TutorialZone`처럼 키로 찾아 쓰고, `Resolve()`의 누락 검사 목록에 키를 추가한다.
- **플레이어 따라가기 안내(발사 · 장전 · 회복 · 남은 적 수):** 위치가 정해지지 않고 전투 중 플레이어가 움직이므로 **플레이어 머리 위 12시 방향 공중에 띄운다.** 같은 모양의 표시기 하나를 공용으로 쓴다. 이 넷은 동시에 뜨는 일이 없다. (결정 7-1. 사용자가 지정한 것은 발사 · 회복이며, 같은 성격인 장전 · 남은 적 수도 같은 방식으로 묶었다. 7-1 참고)

### 3.1 복도 1: 이동 · 대시를 벽에 상시 표시

조사 결과 지금은 두 안내가 서로 다른 복도에 있다.

| 구분 | 위치(월드) | 설명 |
|---|---|---|
| 복도 1 (가로) | x 7 ~ 36, y 약 6 (탈출 컷씬 종료 지점 `EscapeStop` = (7, 6.5)) | 이동 안내 `Zone_Move`가 (16.5, 6)에 있음. 길이 약 29유닛으로 카메라 가로 폭(약 30유닛)과 비슷 |
| 복도 2 (세로) | x 36 부근, y 6 ~ 23 | 대시 안내 `Zone_Dash`가 (36, 11)에 있음 |

변경 내용 (결정됨: 상시 표시, `Zone_Dash` 삭제)

- 이동 · 대시 문구를 **복도 1 벽에 상시 표시되는 오브젝트**로 둔다. 구역 진입이나 이동 거리, 대시 사용으로 켜고 끄지 않는다. 문구 위치는 이동이 시작 쪽, 대시가 그보다 오른쪽(대략 x 12와 x 24 부근을 출발점으로 잡는다). 구현 때 타일맵과 카메라 범위를 보고, 플레이어가 복도 시작에 있을 때 두 문구가 한 화면 안에 겹치지 않고 들어오는지 확정한다.
- 흐름 코드에서 이동 · 대시 안내를 걷어 낸다.
  - `Step.MoveDashGuide` 단계, `MoveDashGuide()` · `GuideUntilMoved()` · `DashGuide()`, 필드 `_moveGuide` · `_dashGuide` · `_moveGuideDistance`를 삭제한다. `Run`이 이어 가는 순서는 `Escape → ShotgunSpot → …`가 된다.
  - 디버그의 `NextStep(Escape)`를 `ShotgunSpot`으로 고친다.
  - 단계 사이에서 이어지던 구역 초기화는 `Escape` 단계의 `ResetZones(모든 구역)`이 이미 하므로 따로 옮기지 않는다.
- **`Zone_Dash` 삭제.** `Resolve()`의 `"Dash"` 키 검사도 뺀다.
- **`Zone_Move`도 쓰는 곳이 `MoveDashGuide`뿐이라 같이 쓸모가 없어진다.** 삭제하고 `"Move"` 키 검사도 뺀다. (결정 7-5)
- 대시를 안 써도 진행에는 영향이 없다. 지금도 그랬고 바뀌지 않는다.


---

## 4. 설계

### 4.1 표시기: `TutorialWallGuide` (새 컴포넌트 + 프리팹)

- 월드 공간 `TextMeshPro`(3D 텍스트, 캔버스 없음) 하나가 붙은 오브젝트. **벽 위치마다 씬에 하나씩** 둔다. 이동과 대시처럼 둘 이상이 동시에 떠야 하는 경우가 있기 때문이다.
- 키(`_key`) 하나를 직렬화한다. `TutorialFlow`가 `TutorialZone`처럼 `FindObjectsByType`으로 모아 키로 찾는다.
- 공개 API는 `Show(string message)` / `Hide()` 두 개. 위치는 씬에서 정한 오브젝트 자리다.
- 플레이어 따라가기 안내는 같은 프리팹을 쓰되, `Show(message, Transform follow)`로 대상을 받아 켜져 있는 동안 `LateUpdate`에서 대상 위(`+ Vector3.up * 높이`)로 이동한다. 높이는 직렬화 값 하나(플레이어 머리에서 12시 방향으로 띄우는 거리). 플레이어는 좌우 반전 때 스케일이 바뀔 수 있어 **플레이어의 자식으로 붙이지 않고** 위치만 따라간다. 시간이 멈춰도(`timeScale 0`) 따라가야 하므로 `LateUpdate`에서 위치만 갱신하고 `Time.deltaTime`을 쓰지 않는다.
- 문구 안에 키캡을 **TMP 인라인 스프라이트**로 넣는다. 예: `점프 : <sprite name="Key_Space">`.
  - 장점: 문구와 키캡이 한 줄로 자동 정렬되고, 키캡이 여러 개여도(`W A S D`) 코드 없이 처리된다.
  - 문구는 지금처럼 `TutorialFlow`의 문자열 하나로 유지된다.
- 레퍼런스처럼 판은 두지 않는다. 글자 외곽선만으로 벽 위에서 읽히게 한다.
- 정렬은 기존 레이어 중 **배경 벽 타일맵 바로 위, Characters 아래**로 `MeshRenderer.sortingLayerID`와 `sortingOrder`를 코드가 아닌 프리팹에서 지정한다. 구체 레이어는 `TutorialScene` 타일맵 확인 후 정한다.
- 켜고 끌 때 짧은 페이드는 **하지 않는다**(레퍼런스는 고정 표시). 필요하면 후속으로 `unscaledTime` 기반으로 추가한다.

### 4.2 키캡 에셋

- 도트 키캡 시트를 `Docs/tools/spritegen/ui/`의 방식(Python 생성 → `check.py` 검증 → Unity 임포트 C#)으로 만든다. STYLE · README의 PPU 16 · Point 필터 · 팔레트 관례를 따른다.
- 필요한 키: `W` `A` `S` `D` `E` `R` `1` `2` `3` `4`, 마우스 `좌클릭` · `우클릭`(마우스 몸체에 해당 버튼만 강조). 글자는 키캡에 구워 넣는다.
- 임포트 도구가 TMP **스프라이트 에셋**을 만들고, 이름을 `Key_W` … `Mouse_L` 식으로 맞춘다.
- 키캡 높이는 글자 높이와 맞추고, 가로는 `SPACE`처럼 넓은 키도 비율을 유지한다. (TMP가 스프라이트 비율을 보존한다.)

### 4.3 글자 머티리얼

- `Mulmaru SDF`의 **머티리얼 프리셋을 복제**해 외곽선(어두운 색)을 켜고 월드 안내 전용으로 쓴다. 공용 머티리얼과 UI는 건드리지 않는다.
- 글자 크기는 월드 단위 기준으로 정한다. 도트 한 칸이 1/16유닛이 되도록 크기를 맞추고, 레퍼런스와 같은 장면(1080p 캡처)에서 나란히 비교해 확정한다.

### 4.4 코드 변경 범위

| 파일 | 변경 |
|---|---|
| `TutorialWallGuide.cs` (신규) | 4.1의 표시기. 코드 한 장 안에서 끝낸다 |
| `TutorialFlow.cs` | 안내 호출 `_popup.Show/Hide` → `ShowGuide(key, message)` / `HideGuide(key)`(키로 찾은 `TutorialWallGuide` 호출). 발사 · 장전 · 회복 · 남은 적 수는 `Player` 키로 `ShowGuide`를 부르되 플레이어 `Transform`을 함께 넘긴다. `Resolve()`가 안내를 모아 두고 누락 키를 검사. `_popup`은 `SetBlackout`용으로 남음 |
| `TutorialFlow.cs` 보조 안내 | `SetGuide(message)`가 단일 `_guideMessage`를 비교하던 방식을 `SetGuide(key, message)`로 바꾼다. 보조 안내가 닫힐 때(`StopSideGuide`)는 마지막으로 연 키를 닫는다 |
| `TutorialFlow` 이동 · 대시 | 3.1 참고. `MoveDashGuide` 단계와 관련 메서드 · 필드 삭제, `Resolve()`의 `"Move"` · `"Dash"` 키 검사 삭제, `NextStep(Escape)` → `ShotgunSpot` |
| `TutorialPopup.cs` | 안내 표시 코드(`_panel` · `_text` · `Show` · `Hide` · `IsShowing`)는 내가 만든 변경으로 쓰이지 않게 되므로 제거하고 암전만 남긴다. 클래스 이름은 바꾸지 않는다(씬 참조 보존) |
| `ApplyStageState`(디버그) | 모든 안내를 숨긴다(`_popup.Hide()` → 키별 `Hide`) |

`TutorialPopup.IsShowing`을 쓰는 곳은 없다(`Assets/02.Scripts` 전체 검색으로 확인. 다이얼로그의 `IsShowing`은 다른 클래스).

---

## 5. 씬 · 프리팹 작업

> **주의:** 현재 `TutorialScene.unity`가 작업 트리에서 이미 수정 상태(`M`)이다. 사용자 손 변경이 섞여 있을 수 있으므로 **씬을 만지기 전에 diff를 확인하고, 사용자에게 알린 뒤 진행한다.** 씬 수정은 위임하지 않고 메인이 에디터 상태를 직접 다룬다. 임시 오브젝트로 확인할 때는 try/finally로 정리한다.

1. `TutorialWallGuide.prefab` 생성(월드 TMP + 외곽선 머티리얼 + 스프라이트 에셋).
2. `TutorialScene`에 프리팹 인스턴스를 3절 표의 키별로 둔다(벽 고정 `Pickup` · `Loot` 2개 + 플레이어 따라가기 `Player` 1개. 이동 · 대시는 3.1의 상시 오브젝트 2개). 벽 위치는 구역 콜라이더와 타일맵을 보고 정한다. `TutorialFlow`에는 연결할 칸이 없다(키로 찾음). 이동 · 대시 상시 오브젝트는 같은 프리팹에서 `TutorialWallGuide` 컴포넌트를 뺀 것(텍스트만)을 쓴다.
3. 복도 1 정리: `Zone_Dash` 삭제, `Zone_Move`도 삭제(결정 7-5). 3.1 참고.
4. 직렬화된 문구 7개(이동 · 대시 제외)의 **씬 저장 값**을 키캡 태그가 들어간 새 문구로 교체한다. (코드 기본값만 바꾸면 반영되지 않는다.) 에디터 스크립트로 `SerializedObject`를 통해 바꾸고, 기본값도 같이 맞춘다.
5. `TutorialPopup.prefab`에서 안내용 `Panel`(및 `Text`)을 제거하고 `Blackout`만 남긴다.

문구 변경안(초안)

| 필드 | 지금 | 변경 |
|---|---|---|
| (이동, 상시 오브젝트) | 필드 삭제 | `이동 : <sprite name="Key_W"><sprite name="Key_A"><sprite name="Key_S"><sprite name="Key_D">` 를 씬 오브젝트 텍스트에 직접 기입 |
| (대시, 상시 오브젝트) | 필드 삭제 | `대시 : <sprite name="Mouse_R">` 를 씬 오브젝트 텍스트에 직접 기입 |
| `_pickupGuide` | E 키로 샷건 줍기 | `줍기 : <sprite name="Key_E">` |
| `_shootGuide` | 마우스 좌클릭으로 발사 | `발사 : <sprite name="Mouse_L">` |
| `_reloadGuide` | R 키로 장전 | `장전 : <sprite name="Key_R">` |
| `_chestOpenGuide` | E 키를 길게 눌러 상자 열기 | `상자 열기 : <sprite name="Key_E"> 길게` |
| `_chestTakeGuide` | 아이템을 더블 클릭해 인벤토리에 담기 | `담기 : <sprite name="Mouse_L"> 더블 클릭` |
| `_healGuide` | 퀵슬롯 번호 키로 … 좌클릭으로 사용 | `회복 아이템 : <sprite name="Key_1">~<sprite name="Key_4"> → <sprite name="Mouse_L">` (두 줄 가능) |
| `_arenaHint` | 남은 적을 모두 처치하세요 | 키캡 없음. 문구만 유지 |

---

## 6. 작업 순서 · 검증

| 단계 | 내용 | 확인 |
|---|---|---|
| 1 | 키캡 시트 생성 · 임포트 · TMP 스프라이트 에셋 | `check.py` 통과, 시트 미리보기 이미지를 사용자가 승인(시안 저장 위치는 구현 때 기존 UI 시안 관례를 확인) |
| 2 | 프리팹 · 머티리얼 · `TutorialWallGuide` | 빈 씬이 아닌 `TutorialScene`에 임시 오브젝트로 문구 하나 띄워 1080p 캡처를 레퍼런스와 비교 |
| 3 | `TutorialFlow` 연결 · 마커 배치 · 문구 교체 | 컷씬 디버거로 단계를 하나씩 점프해 각 안내가 제 위치에 뜨는지 확인 |
| 4 | 전체 플레이 | 이동 → 대시 → 줍기 → 발사(시간 정지 중에도 보임) → 장전 → 상자(안 열고 지나가기 포함) → 보라 방 25초 힌트 → 회복 |
| 5 | 정리 | `TutorialPopup` 안내 코드 제거, 컴파일 오류 없음 확인 |

- 플레이 확인 전에 세이브를 백업하고 비운 뒤 끝나면 복원한다. 에디터에 저장 안 된 씬이 있으면 테스트가 저장 창을 띄우므로 먼저 확인한다. 사용자가 플레이 중이면 recompile · 씬 열기를 하지 않는다.
- 시간 정지 중 표시(플레이어를 따라 위치가 유지되는지), 카메라 가장자리 근처 잘림, 플레이어 머리 위 문구가 조준 · 총구 연출을 가리지 않는지, 글자 도트 선명도는 **사람 눈으로 확인**한다.

---

## 7. 결정이 필요한 질문

모두 결정됨(2026-10-13).

1. **회복 · 사격 안내는 플레이어 위치에서 12시 방향 공중에 띄운다.** 카메라 상단 고정 방식은 쓰지 않는다. 같은 성격인 **장전 · 남은 적 수**도 같은 방식으로 묶었다(발사 → 장전이 같은 전투이고, 남은 적 수는 방 안 어디서든 보여야 한다). 다르게 하길 원하면 알려 달라.
2. **키캡은 도트로 새로 제작한다.**
3. **문구 형식을 `행동 : [키캡]`으로 바꾼다.**
4. **외곽선은 어두운 남색, 키캡 팔레트는 제안대로.** 시안을 보고 확정한다.
5. **이동 · 대시 문구는 복도 1에 상시 표시, `Zone_Dash` · `Zone_Move` 삭제.**

---

## 8. 위험

- **SDF 글자의 도트 선명도:** Mulmaru는 SDF라 확대 비율이 도트와 안 맞으면 가장자리가 흐려 보일 수 있다. 4.3에서 크기를 맞춰 비교하고, 부족하면 외곽선 · 임계값을 조정한다.
- **씬 충돌:** 씬이 이미 수정 상태이고 문구 값 9개와 마커 5개가 씬에 들어간다. 병합 충돌 가능성이 있으므로 작업 단위 브랜치로 분리한다.
- **정렬 레이어:** 새 레이어 없이 기존 레이어 안의 `sortingOrder`로 해결해야 한다. 배경 벽 타일맵보다 위인지 구현 때 확인한다.
- **안내 누락:** `Resolve()`가 키를 검사하므로 안내 오브젝트를 하나라도 안 두면 튜토리얼이 시작되지 않는다. 의도된 동작이다(구역 · 마커 검사와 같은 방식).
- **복도 1 상시 문구의 노출 범위:** 탈출 컷씬 끝(`EscapeStop` = (7, 6.5))에서 카메라에 문구가 이미 비칠 수 있다. 컷씬 중에 보여도 괜찮은지 구현 때 확인한다. 탈출 단계 동안 HUD가 숨겨져 있으니 어색하면 문구를 더 안쪽으로 둔다.
- **플레이어 따라가기 안내와 조준선:** 문구가 머리 위 12시 방향에 있으므로 위쪽으로 쏠 때 총구 섬광이나 펠릿 선과 겹칠 수 있다. 높이를 캡처로 확인해 정한다.
- **복도 1 가독성:** 이동과 대시 문구가 한 화면에 함께 들어와도 서로 겹치지 않아야 한다. 카메라 범위는 플레이어 위치에 따라 달라지므로 시작 위치와 중간 위치 둘 다에서 캡처로 확인한다.
- **문서 동기화:** `Docs/quests/CS_TUTORIAL.md` §3-2는 "복도 1: 이동, 복도 2: 대시"로 적혀 있다. 구현할 때 "복도 1 벽에 이동 · 대시 문구 상시 표시"로 고친다.

---

## 9. 구현 결과 (2026-10-13)

| 항목 | 상태 | 비고 |
|---|---|---|
| 도트 키캡 시트 | 완료 | `Docs/tools/spritegen/ui/keycaps.py` → `Assets/04.Sprites/UI/TutorialKeycaps.png`(192×16, 12칸). 임포트 `unity/keycaps_import.cs`가 슬라이스와 TMP 스프라이트 에셋(`TutorialKeycaps SpriteAsset.asset`)까지 만든다 |
| 월드 글자 머티리얼 · 프리팹 | 완료 | `M_TutorialGuideText.mat`(Mulmaru 복제 + 남색 외곽선), `TutorialWallText.prefab`(글자만), `TutorialWallGuide.prefab`(변형, 컴포넌트 추가) |
| `TutorialWallGuide` · `TutorialFlow` · `TutorialPopup` | 완료 | 3.1 · 4.4 그대로. 설치 도구 `unity/tutorial_wall_guide_setup.cs` (다시 돌려도 중복 생성 없음) |
| 씬 | 완료 | 복도 1 벽에 `Guide_Move`(x 19) · `Guide_Dash`(x 28), 흐름 안내 `Guide_Pickup` · `Guide_Loot` · `Guide_Player`. `Zone_Move` · `Zone_Dash` 삭제, 저장된 안내 문구 7개를 코드 기본값으로 교체 |
| `TutorialPopup.prefab` | 완료 | 안내용 `Panel` 제거(암전만 남음) |
| `CS_TUTORIAL.md` §3-2 · 표 · 구역 목록 | 완료 | |

확인한 것
- 컴파일 오류 없음. 편집기 렌더링 캡처로 복도 1의 이동 · 대시 문구가 벽돌 벽 위에 겹치지 않고 보이는 것을 확인했다.
- 플레이 모드에서 `Resolve()`가 안내 3개를 모두 찾고 튜토리얼이 정상 시작됐다. 안내를 직접 호출해 플레이어 머리 위 안내(회복)가 플레이어를 따라 2.5유닛 위에 뜨는 것과 줍기 안내가 진열대 위에 뜨는 것을 게임 화면 캡처로 확인했다.

**아직 확인하지 못한 것 (사람이 플레이로 확인 필요)**
- 처음부터 끝까지 이어 하는 흐름: 탈출 컷씬의 비명 대사가 입력을 기다려 무인 플레이로는 넘기지 못했다. 컷씬 중 정지한 원인을 대사 입력 대기로 추정할 뿐 확정하지 못했다.
- 시간 정지 중 발사 안내 표시, 상자 안내(구역 안에서만), 장전 · 남은 적 수 안내.
- 플레이어 머리 위 안내가 적에 가려지지 않는지: `Guide_Player`를 `Foreground/10` 레이어에 두었다. 편집기 캡처에서는 글자 하위 메시가 이전 레이어로 남아 가려졌지만, 플레이 중에는 가려지지 않았다.
- 마우스 아이콘이 작아(칸 14px) 마우스로 읽히는지 눈으로 확인.
- 탈출 컷씬 끝(`EscapeStop` 부근)에서 상시 문구가 화면에 어색하게 비치는지.

### 9.1 변경 (2026-10-13, 사용자 요청)

1. **이동 · 대시 문구는 탈출 컷씬이 끝난 뒤에 한꺼번에 보인다.** 3.1의 "상시 표시, 흐름과 무관"을 뒤집는다.
   - `Guide_Move` · `Guide_Dash`를 글자만 있던 프리팹에서 `TutorialWallGuide`(키 `Move` · `Dash`)로 바꿨다. 시작할 때는 숨어 있다.
   - `TutorialFlow`가 `Step.ShotgunSpot` 시작에서 둘을 함께 켠다(탈출 컷씬 다음 단계이므로 컷씬이 끝난 직후). 문구는 씬 오브젝트에 적힌 그대로 쓴다(`ShowGuide(key, null)`), 그래서 `_moveGuide` · `_dashGuide` 필드는 되살리지 않았다. `Resolve()`의 필수 키에 `Move` · `Dash`를 추가했다.
   - 디버거로 Escape 외의 컷씬으로 점프하면 켜진 상태로 시작한다(`ApplyStageState`).
2. **글자와 아이콘 아래에 배경 판을 깐다.** (사용자 말씀의 "레터박스"를 글자 뒤 배경 판으로 해석했다.)
   - `TutorialGuidePanel.png`(8×8, 9슬라이스 3px, 어두운 남색 알파 약 0.78 + 1px 테두리 + 둥근 모서리). 생성 도구 `ui/guide_panel.py`, 적용 도구 `unity/tutorial_wall_guide_panel.cs`.
   - 판은 글자 아래 자식 `Panel`(SpriteRenderer, Sliced)이고, `TutorialWallGuide.Show`가 글자 크기(`textBounds`)에 여백(가로 12px · 세로 6px)을 더해 도트 격자에 맞춰 크기를 정한다. 판은 글자보다 한 칸 아래 순서에 그린다.
   - 키캡 스프라이트 하위 메시가 만들어질 때의 레이어를 유지하는 문제도 `Show`가 글자 레이어에 맞춰 해결한다(플레이어 머리 위 안내가 `Foreground`에서 보이게).
3. 확인: 편집기 렌더에서 판이 글자 · 키캡 아래에 맞게 그려지는 것, 플레이 모드에서 탈출 컷씬 동안 이동 · 대시 문구가 꺼져 있고 `ShotgunSpot` 단계에 들어가면 함께 켜지는 것을 확인했다.

