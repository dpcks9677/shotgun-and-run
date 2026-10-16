# 퀘스트 시스템 구현 접근 계획서

> **작성 일자:** 2026-10-06  
> **상태:** 초안 (Draft) — 구조 설계 단계  
> **관련 문서:** [game-spec.md](game-spec.md) (9.3 쉘터 퀘스트 · 4.3 퀘스트 디펜스 · 9.1 레벨) / [quest-story-concept.md](quest-story-concept.md) (스토리 퀘스트 Q1 · Q2) / [quest-db.md](quest-db.md) (퀘스트 13종 목록)

---

## 1. 핵심 원칙: "런 중에 기록하고, 탈출할 때 판정한다"

명세 9.3의 규칙은 다음과 같습니다.

- 조건 달성 후 **탈출해야 클리어**
- 사망하면 **이번 런의 진행은 무효**, 퀘스트 수주 상태는 유지
- 반복 퀘스트 없음 (모두 1회성)

그래서 진행 상태를 두 층으로 나눕니다.

| 층 | 수명 | 위치 | 내용 |
|---|---|---|---|
| 확정 진행 | 영구 저장 | `ProgressData` | 수주 · 완료 목록, 영구 해금 플래그 |
| 런 진행 | 런 1회 | `RunSession` 옆의 런 단위 객체 | 이번 런에 달성한 플래그 (상호작용, 웨이브 클리어 등) |

- **탈출:** 런 진행과 가방 내용을 함께 보고 판정 → 통과하면 완료 처리 + 보상 지급
- **사망:** 런 진행만 버림. 수주 상태는 그대로

---

## 2. 구조

### 2.1 데이터: `QuestDefinitionSO` (ScriptableObject)

- ID, 슬롯(스토리 / 의뢰), 타입, 관련 존, 고정 스폰, 선행 조건
- 완료 조건 3가지
  - **들고 탈출해야 하는 아이템**
  - **런 중 달성해야 하는 플래그**
  - **탈출 필수 여부**
- 보상: XP, 재료, 해금 플래그

완료 조건을 이 3가지로 묶으면 타입별 클래스를 따로 만들 필요가 거의 없습니다.

| 퀘스트 | 판정 방식 |
|---|---|
| 초반 탐사 · 보물찾기 · 회수 | "아이템 X를 들고 탈출" (지정 엘리트 처치형 회수도 드랍 아이템을 들고 나오는 것이라 같은 판정) |
| 구역 정화 | 플래그 `Z1_PURIFY_1_CLEARED` + 탈출 |
| Q2 엘레나 구출 | 플래그 `H2_ELENA_FREED` + 탈출 |

### 2.2 상태: `ProgressData`

- 수주 중인 퀘스트 ID, 완료한 퀘스트 ID
- 영구 플래그 (`RandomSpawnUnlocked`, `ElenaInShelter`, 기믹 가동 상태 등)
- JsonUtility로 저장하므로 Dictionary 대신 `List<string>` 사용

### 2.3 로직: 순수 C# `QuestService`

- GameManager가 소유. `RunSession`처럼 MonoBehaviour가 아니게 두어 EditMode 테스트가 쉽도록 함
- 하는 일
  - 수주 · 포기
  - 게시판 목록 계산 (해금 조건)
  - 런 시작 때 퀘스트 아이템 요청 생성
  - 탈출 때 판정 + 보상 지급

### 2.4 런 중 이벤트

- 씬 오브젝트(엘레나, 정화 오브젝트, 웨이브 매니저)는 `QuestService.RaiseFlag("H2_ELENA_FREED")` 정도만 호출
- 씬 오브젝트가 퀘스트 구조를 몰라도 되게 함

### 2.5 흐름 요약

```
[쉘터] 게시판에서 수주 → ProgressData에 수주 ID 저장
   ↓
[출격] 고정 스폰 결정 · LootSpawner에 퀘스트 아이템 요청 전달
   ↓
[런 중] 아이템 획득(가방) · 오브젝트가 RaiseFlag 호출(런 진행)
   ↓
[탈출] EndRun(Extracted) → 가방 + 런 플래그로 판정 → 완료 · 보상 · 해금 플래그 저장
[사망] EndRun(Died) → 런 진행 폐기, 수주 유지
```

---

## 3. 기존 코드와 연결할 지점

1. **[ProgressData.cs](../Assets/02.Scripts/Core/Progress/ProgressData.cs)**
   - `QuestIndex` / `QuestConditionMet`는 폐기된 v1.5 고정 체인의 잔재
   - 새 구조로 바꾸면서 `CurrentSaveVersion`을 3으로 올리고 이전 세이브 변환 처리 추가
2. **[LootSpawner.cs](../Assets/02.Scripts/Inventory/Chest/LootSpawner.cs) `BuildQuestRequests()`**
   - "퀘스트 시스템이 생기면 여기서 만든다"는 TODO가 이미 있음
   - 수주한 퀘스트에서 `QuestLootRequest`를 만들어 넘기면 됨. 상자에 넣는 로직은 [LootGenerator.cs](../Assets/02.Scripts/Loot/LootGenerator.cs)에 구현 완료
3. **[RunSpawnPoint.cs](../Assets/02.Scripts/Core/Run/RunSpawnPoint.cs)**
   - 현재 `PickRandom()`만 있음
   - Q1 · Q2의 E1 고정 스폰을 위해 ID로 고르는 기능 추가
   - 랜덤 스폰은 `RandomSpawnUnlocked` 플래그로 분기
4. **[GameManager.cs](../Assets/02.Scripts/Core/GameManager.cs) `EndRun` — 가장 중요**
   - 현재는 탈출 시 가방 전체를 `AddToStorage`로 창고에 넣음
   - 그 **전에** 퀘스트 판정을 하고, 퀘스트 아이템은 창고에 넣지 말고 소비
   - 결과창 표시를 위해 `RunResult`에 완료한 퀘스트 목록 추가
5. **상호작용**
   - E키 2초 홀드는 현재 [LootChest.cs](../Assets/02.Scripts/Inventory/Chest/LootChest.cs) 안에만 구현됨
   - 엘레나 · 정화 오브젝트도 같은 조작을 쓰므로 공용 홀드 상호작용 컴포넌트로 분리 권장
6. **대화**
   - 대화 시스템 없음. 로나 · 그룬 · 엘레나용 최소 대화 UI(대사 SO + 대화 박스) 필요

---

## 4. 작업 순서 (작업 단위 PR 기준)

| 순서 | 작업 | 확인 방법 |
|:-:|---|---|
| 1 | 데이터 + 저장 + `QuestService` ✅ (2026-10-06) | EditMode 테스트로 수주 → 판정 → 보상 흐름 검증 (씬 없이) |
| 2 | 런 연결: LootSpawner 요청 · 고정 스폰 · EndRun 판정 | `GameDebugTools`에 퀘스트 강제 수주 / 완료 버튼 |
| 3 | **Q1 숫돌 처음부터 끝까지** | 디버그 수주 → 숫돌 드랍 → 들고 탈출 → 보상 |
| 4 | 쉘터 게시판 UI (스토리 1 + 의뢰 3 슬롯, 포기) | 쉘터에서 수주 · 포기 |
| 5 | 공용 상호작용 + 대화 + 이벤트 웨이브 → Q2 엘레나 구출 | 구출 후 탈출 → 랜덤 스폰 해금 |
| 6 | 구역 정화 · 보물찾기 등 의뢰 타입 확장 | 타입별 1개씩 플레이 확인 |

Q1을 먼저 끝까지 완성하면 구조의 문제를 일찍 발견할 수 있습니다.

### 4.1 1단계 결과 (2026-10-06)

| 파일 | 역할 |
|---|---|
| `02.Scripts/Quest/ShotgunAndRun.Quest.asmdef` | 참조: Inventory.Data · Loot. Core가 이 어셈블리를 참조 |
| `QuestTypes.cs` | `QuestType` · `QuestSlot`(타입에서 결정) · `QuestState` · `QuestItemSource` · `QuestRewardItem` |
| `QuestDefinitionSO.cs` | 정의 + `FindProblems()` 설정 검사. 메뉴 `ShotgunAndRun/Quest/Quest Definition` |
| `QuestCatalogSO.cs` | ID 검색 · `Validate Catalog`. 메뉴 `ShotgunAndRun/Quest/Quest Catalog` |
| `QuestProgress.cs` | 세이브 상태 (수주 · 완료 · 존 방문 확인 · 영구 플래그). **2026-10-12 세이브 버전 4:** 상태별 ID 목록 → 퀘스트별 기록 `Records`(`Id` · `Status` 문자열 `NotAccepted`/`Accepted`/`ReadyToTurnIn`/`Completed` · `AcceptedOrder` · `StampPending` · `ZoneVisited`). `QuestService` 생성 시 카탈로그 순서로 전 퀘스트 기록을 채움, 버전 3 목록은 `SaveSystem`이 변환 |
| `QuestFlags.cs` | 플래그 이름 상수 모음 (2026-10-12). 코드는 상수 사용, 퀘스트 SO의 플래그는 `Validate Catalog`가 등록 여부 검사. 새 플래그는 여기에 먼저 추가 |
| `QuestService.cs` | 수주 · 포기 · 해금 판정 · 런 플래그 · 탈출 판정 · 상자 요청 · 고정 스폰 |
| `QuestRunReport.cs` | 탈출 판정 결과 (완료 퀘스트 · XP · 보상 재료 · 소비할 퀘스트 아이템) |
| `ProgressData.Quests` | 세이브 버전 3. 기존 `QuestIndex` · `QuestConditionMet` 삭제 (이전 체인은 옮기지 않음) |

- 에셋 위치: `07.Datas/Quests/` (정의), 카탈로그는 `07.Datas/` 루트
- 보상(XP · 재료) 지급은 `QuestService`가 하지 않고 `QuestRunReport`를 받은 `GameManager`가 반영 (2단계)
- 구역 정화 해금의 "방문 후 탈출"은 탈출 판정 때 **완료 반영 전에** 확인 → 초반 탐사를 완료한 그 런의 방문은 인정하지 않음
- 아직 없는 것: 지정 엘리트 처치형 회수 (적 쪽 연결 후), 런 중 존 방문 기록 (2단계에서 `CompleteRun`에 넘김)

---

## 5. 구현 전 기획 확인 사항

두 기획 문서([game-spec.md](game-spec.md) / [quest-story-concept.md](quest-story-concept.md))가 어긋나는 부분입니다.

- [ ] **XP:** 컨셉 문서는 Q1 300 XP로 Lv2 달성이지만, 명세 9.1은 Lv1→2에 1,200 XP 필요. 퀘스트 XP는 `(미정)`
- [ ] **메인 스토리의 성격:** 명세는 "기믹으로 유도만", 컨셉 문서 Q2는 구출 + 웨이브 이벤트형. 어느 쪽을 따를지
- [x] **무기 강화 해금:** Q1 뒤 그룬 보고(`CS_Q1_DEBRIEF`, `FLAG_Q1_DEBRIEF_DONE`)로 해금. 그 전에는 `WeaponUpgradeUI`가 열리지 않음 (2026-10-10)
- [ ] **튜토리얼 · 프롤로그 · 폭파 붕괴 이벤트:** 퀘스트 시스템 안에서 처리할지, 별도 연출 시퀀스로 뺄지 (영구 플래그로만 연결하는 방식 권장)
- [x] **스토리 퀘스트 수주 방식:** NPC 상호작용 컷씬에서 수주 (Q1 그룬, Q2 로나). [quests/CS_Q1_WHETSTONE.md](quests/CS_Q1_WHETSTONE.md) (2026-10-10)
