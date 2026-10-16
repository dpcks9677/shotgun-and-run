# 게임 흐름 연동 작업 계획서 (로딩 · 탈출 연출 · 던전 컷씬)

> **작성 일자:** 2026-10-08  
> **상태:** 계획 — 미착수  
> **범위:** 씬 전환 로딩 화면, 탈출 컷씬 → 결과창 순서, 던전 안 퀘스트 컷씬 재생 기반  
> **관련 문서:** [cutscene-system-design.md](cutscene-system-design.md) (5장 재생 조건과 저장 · 7.7 씬 전환 · 7.8 탈출 연출 · 11장 6단계) / [quest-system-implementation-plan.md](quest-system-implementation-plan.md) (1장 "런 중에 기록하고, 탈출할 때 판정한다") / [sandbox-rules.md](sandbox-rules.md) (3장 참조 방향)

## 목차
1. 목표
2. 현재 구조와 문제
3. 바뀐 뒤 구조
4. 흐름
5. 작업 단계
6. 다른 담당자 코드 변경
7. 결정 사항 · 남은 일

---

## 1. 목표

| # | 요구사항 | 완료 기준 |
|---|---|---|
| 1 | 쉘터 → 던전 진입 시 로딩 시간을 두어, 몬스터 · 상자 배치가 끝난 뒤 렉 없이 시작한다 | 로딩 화면이 걷힌 뒤에는 생성 렉이 없다(프로파일러). 런 타이머는 화면이 걷힐 때부터 흐른다 |
| 2 | 탈출하면 탈출 컷씬을 재생한 뒤 결과창을 띄운다 | 탈출 → 약 2초 컷씬 → 결과창. 컷씬이 실패해도 결과창은 뜬다 |
| 3 | 던전 안에서 퀘스트 컷씬을 재생한다 | 던전 씬에서 컷씬이 재생되고, 완료 플래그는 탈출해야 저장된다 |

---

## 2. 현재 구조와 문제

### 2.1 씬 구성

- 빌드 씬은 TitleScene · ShelterScene · DungeonStageScene 3개다. [SceneRouter.cs](../Assets/02.Scripts/Core/Scenes/SceneRouter.cs)가 씬 이름을 게임 상태(Title · Shelter · Run)로 바꾼다.
- 씬을 넘어 유지되는 것은 `GameManager` 하나다. [GameBootstrap.cs](../Assets/02.Scripts/Core/GameBootstrap.cs)가 첫 씬 전에 `Resources/GameManager.prefab`을 찾아 만들지만, **이 프리팹이 없어서** 코드 기본값으로 만들어진다. 인스펙터에서 설정을 연결할 곳이 없다.
- 플레이어 · 카메라 · UI는 씬마다 따로 배치되어 있다. 소지품은 `ProgressData`를 거쳐 넘어간다.

| 씬 | 상태 | 관련 오브젝트 |
|---|---|---|
| TitleScene | Title | Canvas, Main Camera |
| ShelterScene | Shelter | Player, Main Camera, DialogueCanvas(대화), NPC, RunEntranceZone(출격) |
| DungeonStageScene | Run | Player, Main Camera, GameHUD(GameResultUI · RunTimerUI), LootSpawner, StandardSpawner, ExtractionZones |

- 정식 씬에는 `CutsceneDirector`가 없다. 샌드박스의 ShelterCutsceneTestScene에만 있다.
- 던전 씬에는 `DialogueCanvas`가 없다. 대사가 나오는 던전 컷씬을 넣으려면 배치해야 한다.

### 2.2 문제

| 문제 | 위치 |
|---|---|
| **동기 로드:** `SceneManager.LoadScene`을 써서 출격 순간 화면이 멈춘다 | [GameManager.cs](../Assets/02.Scripts/Core/GameManager.cs) `LoadSceneFor` |
| **생성 렉이 플레이 중에 생김:** 던전 씬이 활성화되면 `sceneLoaded`에서 `BeginRun`이 런(타이머)을 시작하고, 같은 프레임에 `RunStarted`로 LootSpawner가 상자를, 다음 프레임 `Start`에서 StandardSpawner가 몬스터를 만든다. 모두 화면에 보이는 상태에서 일어난다 | `GameManager.HandleScene` · [LootSpawner.cs](../Assets/02.Scripts/Inventory/Chest/LootSpawner.cs) · [EnemyStandardSpawner.cs](../Assets/02.Scripts/Enemy/Spawn/EnemyStandardSpawner.cs) |
| **결과창이 바로 뜸:** 탈출 대기가 끝나면 `EndRun` → `RunEnded` → 결과창. 컷씬을 끼울 자리가 없다 | [GameResultUI.cs](../Assets/02.Scripts/UI/GameResultUI.cs) `HandleRunEnded` |
| **완료 플래그 즉시 저장:** 컷씬이 끝나면 완료 플래그를 바로 저장한다. 던전에서 컷씬을 보고 죽어도 저장된다 | `CutsceneDirector.SaveCompletionFlags` |
| **탈출 대기가 정지를 무시:** `IsPaused`를 보지 않아 컷씬 중에도 탈출 대기가 진행된다 | [RunSession.cs](../Assets/02.Scripts/Core/Run/RunSession.cs) `AdvanceExtractionHold` |
| **적이 멈추지 않음:** 컷씬 중에도 적 AI · 스포너가 움직인다 | 설계 문서 7.3 |

---

## 3. 바뀐 뒤 구조

```
┌ 항상 유지 (DontDestroyOnLoad) ─────────────────────────────┐
│ GameManager      게임 상태 · 진행 데이터 · 런 시작/종료     │
│ SceneTransition  로딩 화면(검은 화면 + 좌하단 애니메이션)   │
│   (새로)         비동기 씬 로드, "전환 끝" 신호             │
└──────────────────────────▲─────────────────────────────────┘
                           │ 아래층이 위층 이벤트를 구독 (반대 방향 없음)
┌ 씬마다 ──────────────────┴─────────────────────────────────┐
│ ShelterScene      CutsceneDirector (프롤로그 등)            │
│                   DialogueCanvas, Player, NPC ...           │
│ DungeonStageScene CutsceneDirector (퀘스트 · 탈출 컷씬)     │
│                   DialogueCanvas (추가)                     │
│                   RunEndSequence (새로) → GameHUD 결과창    │
│                   스포너 · 상자 · 탈출 구역 ...             │
└────────────────────────────────────────────────────────────┘
```

**원칙**
- 씬이 바뀌는 동안에도 살아 있어야 하는 것(로딩 화면)만 위층에 둔다. 씬의 물건(마커 · NPC · 카메라 · HUD)을 만지는 것은 아래층에 둔다.
- 아래층이 위층 이벤트(`RunEnded`, "전환 끝")를 구독하고, 위층은 아래층을 모른다. 컷씬 코드가 Core를 참조하므로 Core에서 컷씬을 부르면 어셈블리가 순환한다.
- 로딩 · 탈출 순서를 컷씬 관리자 하나에 합치지 않는다.

**새로 만드는 것 (이름은 가칭)**

| 이름 | 위치 | 역할 |
|---|---|---|
| `SceneTransition` | `02.Scripts/Core/Scenes/`, GameManager 오브젝트에 붙임 | 로딩 화면, 비동기 로드, 준비 대기, "전환 끝" 신호 |
| `GameManager.prefab` | `Assets/Resources/` | GameBootstrap이 이미 읽는 경로. SceneTransition 설정(로딩 애니메이션 프레임 등)을 인스펙터에서 연결 |
| `RunEndSequence` | `02.Scripts/UI/` (Assembly-CSharp) | `RunEnded` → 탈출 컷씬 → 결과창. `GameResultUI`가 asmdef 밖에 있어 asmdef 어셈블리(정식 컷씬)에서는 참조할 수 없으므로 같은 쪽에 둔다 |
| `CS_EXTRACTION` | 컷씬 에셋 · 코드 | 탈출 연출 |

---

## 4. 흐름

### 4.1 쉘터 → 던전 (출격)

```
RunEntranceZone → GameManager.StartRun() → SceneTransition.Load(던전)
 ① 페이드 아웃 → 검은 화면, 좌하단 애니메이션 시작, 조작 잠금
 ② timeScale = 0, LoadSceneAsync (쉘터는 화면 뒤에 남아 있음)
 ─── 던전 씬이 활성화되는 프레임 ───
 ③ 던전 오브젝트 Awake · OnEnable (LootSpawner가 RunStarted 구독)
 ④ sceneLoaded → GameManager.BeginRun
      플레이어를 스폰 지점에 배치 → RunStarted → LootSpawner가 상자 생성
 ─── 다음 프레임 ───
 ⑤ Start: StandardSpawner가 몬스터 생성, Director 자동 재생 대기 시작
 ⑥ 1~2프레임 대기 + Resources.UnloadUnusedAssets · GC.Collect
 ⑦ timeScale = 1, 페이드 인 → 조작 해제
 ⑧ "전환 끝" → Director가 자동 재생할 컷씬이 있는지 검사
```

- ③~⑤는 지금도 같은 순서로 일어난다. 새로 생기는 것은 ①②⑥⑦⑧이다.
- 런 타이머는 GameManager가 `Time.deltaTime`으로 돌리므로, ②~⑦ 동안 timeScale이 0이면 타이머와 적이 함께 멈춘다. 런 시작 코드는 고치지 않는다.
- 로딩은 `RunSession.SetPaused`를 쓰지 않는다. 값이 하나라서 컷씬과 함께 쓰면 먼저 끝난 쪽이 다른 쪽의 정지를 풀어 버린다.
- 씬이 활성화되는 프레임에는 애니메이션도 잠깐 멈추지만, 검은 화면 뒤라 보이지 않는다.

### 4.2 던전 안 퀘스트 컷씬

```
CutsceneTrigger(구역 진입) / NPC 상호작용 / 씬 신호 → Director.Play
 → 재생 중: 런 타이머 정지 · 무적 · 조작 잠금 · HUD 숨김 (+ 5단계 후 적 정지)
 → 끝: 완료 플래그를 보류 목록에 넣음 (저장 안 함)
```

### 4.3 탈출 → 결과창 → 쉘터

```
ExtractionZone 10초 → GameManager.EndRun(Extracted)
   창고에 아이템 저장 · SaveProgress      ← 정산은 여기서 끝 (지금과 같음)
 → RunEnded
    ├ Director:       보류 플래그 → 영구 플래그 추가 · 저장
    └ RunEndSequence: 탈출 컷씬(약 2초) → GameResultUI.ShowResult
 → 결과창 복귀 버튼 → GoToShelter → SceneTransition (4.1과 같은 순서, 런 시작만 없음)
```

- 정산을 컷씬보다 먼저 하므로, 컷씬 도중 게임을 꺼도 아이템이 보존된다.

### 4.4 사망

```
GameManager.EndRun(Died)
 → RunEnded
    ├ Director:       보류 플래그 버림
    └ RunEndSequence: 컷씬 없이 바로 ShowResult
```

---

## 5. 작업 단계

단계마다 PR 하나로 올린다. base는 `dev`.

| 단계 | 내용 | 먼저 필요한 단계 |
|:-:|---|---|
| 1 | 씬 전환 · 로딩 | — |
| 2 | 컷씬 정식 통합 + 자동 재생 대기 | 1 |
| 3 | 던전 컷씬 보류 플래그 | 2 |
| 4 | 탈출 연출 | 1, 2 (3과는 독립) |
| 5 | 게임 정지 보강 (담당자 협의) | — (3 · 4 전에 끝나면 좋음) |

### 1단계: 씬 전환 · 로딩

**작업**
- `SceneTransition` 컴포넌트
  - 화면: 자체 캔버스(가장 위 정렬)에 검은 덮개와 좌하단 로딩 애니메이션을 둔다. 애니메이션은 스프라이트 프레임 배열을 unscaled time으로 넘긴다.
  - `Load(sceneName)`: 4.1의 ①~⑧ 순서로 진행한다. 진행 중에 다시 요청하면 무시한다(출격 구역 연속 진입 방지).
  - 조작 잠금: `PlayerInputReader.SetGameplayEnabled(false)`. timeScale과 관계없이 입력은 들어오므로 막아 둔다. 씬이 바뀌면 새 씬의 플레이어를 다시 잠근다.
  - "전환 끝": `IsBusy` 속성과 `Finished` 이벤트. 전환 없이 씬에서 바로 Play한 경우 `IsBusy`는 false다.
- `GameManager.LoadSceneFor`가 `SceneManager.LoadScene` 대신 `SceneTransition`을 쓴다. 타이틀 → 쉘터, 쉘터 → 던전, 던전 → 쉘터 모두 같은 로딩을 거친다.
- `Assets/Resources/GameManager.prefab`을 만든다. 지금 코드 기본값 그대로에 `SceneTransition`만 붙인다.
- 로딩 애니메이션은 임시 이미지로 먼저 넣고, 스프라이트가 나오면 교체한다.

**확인 방법**
- 쉘터에서 출격 → 검은 화면 + 좌하단 애니메이션 → 던전. 프로파일러에서 페이드 인 이후 프레임에 생성 스파이크가 없다.
- 화면이 걷힐 때 런 타이머가 시작 값 그대로다. 로딩 중 적이 움직이지 않는다.
- 로딩 중 이동 · 사격 키가 무시된다.
- 결과창 → 쉘터, 타이틀 → 쉘터도 같은 로딩 화면을 거친다.
- 던전 씬에서 바로 Play해도 지금처럼 시작한다.
- 출격 구역에 연속으로 들어가도 로드는 한 번만 일어난다.

### 2단계: 컷씬 정식 통합 + 자동 재생 대기

**작업**
- 설계 문서 11장 5단계 그대로 진행한다: `02.Scripts/Cutscene`으로 이관(Unity 에디터 안에서 옮겨 GUID 유지), 대화 시스템과 함께 asmdef, NPC 프리팹에 `NpcCutsceneActor` 기본 부착, Director 프리팹 정식 폴더 이동, ShelterScene 적용.
- 추가: `CutsceneDirector.AutoPlayWhenReady`가 `SceneTransition.IsBusy`가 풀릴 때까지 기다린다. 지금은 플레이어가 생기고 1프레임 뒤에 재생하므로, 로딩 화면 뒤에서 컷씬이 먼저 재생된다.
- 이 단계가 먼저인 이유: 정식 씬은 샌드박스 스크립트를 참조할 수 없다(샌드박스 규칙 3장). 던전 씬에 Director를 놓으려면 이관이 먼저다.

**확인 방법**
- 새 게임으로 쉘터에 들어가면 로딩 화면이 걷힌 뒤에 프롤로그가 재생된다.
- ShelterCutsceneTestScene에서도 프롤로그가 끝까지 재생된다(GUID 유지 확인).

### 3단계: 던전 컷씬 보류 플래그

**작업** (Director 안에서만 처리, Core는 고치지 않음)
- 런 진행 중(`GameManager.CurrentRun`이 있고 끝나지 않음)에 끝난 컷씬의 `CompletionFlags`와 연출 중 `AddFlag`는 보류 목록에 모으고 저장하지 않는다.
- Director가 `GameManager.RunEnded`를 구독한다. 탈출(`RunOutcome.Extracted`)이면 보류 플래그를 영구 플래그에 추가하고 저장, 사망이면 버린다.
- `CanPlay`와 `HasFlag`는 영구 플래그와 보류 플래그를 함께 본다.

**확인 방법**
- 완료 플래그가 있는 테스트 컷씬을 던전에 놓고 재생한다 → 같은 런에서는 다시 재생되지 않는다.
- 사망 → 세이브에 플래그가 없다 → 다음 런에서 다시 재생된다.
- 탈출 → 세이브에 플래그가 있다 → 다음 런에서 재생되지 않는다.
- 쉘터 컷씬(프롤로그)은 지금처럼 끝나자마자 저장된다.

### 4단계: 탈출 연출

**작업**
- `CS_EXTRACTION` 컷씬
  - 플래그 없음(매번 재생), 자동 재생 끔, 조작 잠금 · HUD 숨김 · 게임 정지 켬.
  - 약 2초, 스킵 불가.
  - 탈출 지점이 여러 곳이므로 마커 없이 플레이어 기준으로 연출한다: 레터박스 · 카메라 · 효과음. 플레이어 애니메이션은 클립이 생기면 추가한다.
- `RunEndSequence`
  - `RunEnded`를 구독한다.
  - 탈출이면 `CS_EXTRACTION`을 재생하고, `CutsceneFinished`에서 이 컷씬이 끝나면 `GameResultUI.ShowResult(result)`를 부른다.
  - 사망이면 바로 `ShowResult`를 부른다.
  - `Play`가 실패하거나(false) 컷씬이 중간에 멈춰도(completed = false) `ShowResult`를 부른다.
- `GameResultUI`: `RunEnded`로 자동 표시할지 정하는 옵션을 추가한다. 기본값은 지금처럼 켬이고, 던전 씬에서만 끈다. 다른 씬 · 테스트 씬의 동작은 그대로다.
- DungeonStageScene 배치: `CutsceneDirector`(`CS_EXTRACTION` 등록), `DialogueCanvas`, `RunEndSequence`, HUD 요소에 `HideDuringCutscene`. GameHUD 루트에는 붙이지 않는다(결과창이 같은 프리팹 안에 있음).
- DungeonStageScene은 여러 PR이 동시에 고치는 씬이라 씬 배치 변경은 이 단계 PR 하나에 모은다.

**확인 방법**
- 탈출 → 약 2초 컷씬(조작 불가 · 무적 · HUD 숨김) → 결과창.
- 컷씬 사전 검사를 일부러 실패시켜도(예: Director 부품 하나를 비움) 결과창이 뜬다.
- 사망하면 컷씬 없이 결과창이 뜬다.
- 결과창 → 쉘터 복귀가 로딩 화면을 거친다.

### 5단계: 게임 정지 보강 (담당자 협의)

**작업**
- `RunSession.AdvanceExtractionHold`: `IsPaused`면 진행하지 않는다(한 줄).
- 적 AI · 스포너 · 파괴 웨이브: `RunSession.IsPaused` 동안 멈춘다. Enemy 어셈블리는 이미 Core를 참조하므로 새 연결 없이 `GameManager.Instance.CurrentRun.IsPaused`를 볼 수 있다.

**확인 방법**
- 던전 컷씬 중 적이 다가오거나 공격하지 않는다. 새 웨이브가 시작되지 않는다.
- 탈출 구역 안에서 컷씬이 재생돼도 탈출 대기가 진행되지 않는다.

---

## 6. 다른 담당자 코드 변경

PR을 올리기 전에 각 담당자와 공유한다.

| 대상 | 변경 | 단계 |
|---|---|:-:|
| `GameManager.cs` | `LoadSceneFor`가 `SceneTransition` 사용 | 1 |
| `Resources/GameManager.prefab` (새로) | GameBootstrap이 읽는 프리팹 생성 | 1 |
| `GameResultUI.cs` | `RunEnded` 자동 표시 옵션 (기본값은 지금 동작) | 4 |
| `DungeonStageScene` | Director · DialogueCanvas · RunEndSequence 배치, HUD 요소에 `HideDuringCutscene` | 4 |
| `RunSession.cs` | 정지 중 탈출 대기 진행 안 함 | 5 |
| 적 AI · 스포너 | 정지 중 멈춤 | 5 |

---

## 7. 결정 사항 · 남은 일

**결정 (2026-10-08)**

| 항목 | 결정 |
|---|---|
| 던전 컷씬 플래그 저장 | 탈출에 성공해야 저장, 사망하면 버림 |
| 탈출 컷씬 | 약 2초, 스킵 불가 |
| 로딩 화면 | 검은 화면 + 좌하단 로딩 애니메이션 |
| 구조 | 로딩은 Core의 씬 전환 담당, 탈출 순서는 던전 씬의 런 종료 순서 컴포넌트. 컷씬 관리자에 합치지 않음 |
| 로딩 중 정지 | `Time.timeScale = 0` (`RunSession.SetPaused` 충돌 회피) |

**정할 것**
- 로딩 애니메이션 스프라이트: 프레임 수 · 크기.
- 페이드 아웃 · 페이드 인 시간과 로딩 화면 최소 표시 시간. 초안은 0.3초 · 0.3초 · 0.5초다.
- 탈출 컷씬 구성: 효과음, 플레이어 애니메이션 클립 유무.
- 탈출 컷씬 시간 계산: Director는 연출이 끝난 뒤 카메라를 플레이어에게 돌려보내는 시간(`Camera Return Duration`, 현재 0.6초)을 더 쓴다. 이 시간을 2초 안에 넣을지 정한다.
- 탈출 컷씬의 마지막 화면: 컷씬이 끝나면 Director가 화면 효과(페이드 · 레터박스)를 되돌린다. 결과창이 화면 전체를 덮는지에 따라 연출 끝을 페이드 아웃으로 할지 정한다.

**남은 일**
- 1단계 후 프로파일러에서 페이드 인 이후에도 처음 쓰는 에셋 로드(첫 사격 이펙트 등)로 인한 렉이 남으면, 로딩 중에 미리 불러오는 방법을 검토한다.
