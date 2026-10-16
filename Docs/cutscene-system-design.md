# 컷씬 시스템 설계

> **작성 일자:** 2026-10-07  
> **상태:** 정식 통합 완료 (2026-10-10) — `Assets/02.Scripts/Cutscene/` (`ShotgunAndRun.Cutscene`). 튜토리얼 컷씬도 `Content/Tutorial/`로 이관 (2026-10-12)  
> **범위:** 쉘터 · 던전 · 튜토리얼 등 모든 씬에서 쓰는 컷씬 재생 구조, 그리고 컷씬이 필요로 하는 대화 시스템 확장  
> **관련 문서:** [quests/CS_PROLOGUE_SHELTER.md](quests/CS_PROLOGUE_SHELTER.md) (첫 적용 대상) / [quest-story-concept.md](quest-story-concept.md) (튜토리얼 각성 · 폭파 붕괴 · 엘레나 구출 연출) / [quest-system-implementation-plan.md](quest-system-implementation-plan.md) 5장 (연출을 영구 플래그로 연결) / [sandbox-rules.md](sandbox-rules.md)

## 목차
1. 요구사항
2. 배경: 이전 프롤로그 구현의 문제
3. 전체 구조
4. 구성 요소
5. 재생 조건과 저장
6. 대화 시스템 확장
7. 다른 시스템과의 연결
8. 디렉토리 구조와 이름 규칙
9. 하드코딩 제거 대응표
10. 새 컷씬 만드는 절차
11. 진행 상황
12. 결정 사항 · 남은 일

---

## 1. 요구사항

| # | 요구사항 | 해결한 곳 |
|---|---|---|
| 1 | 타임라인을 쓰지 않는다 | 연출은 코루틴 코드로 쓴다 (4.1, 4.5) |
| 2 | 쉘터 · 던전 · 튜토리얼 등 어느 씬에서든 쓸 수 있다 | 컷씬은 씬에 묶이지 않는 SO로 만들고, 씬과는 인물 프로필 · 마커 키 · 신호 키로만 연결한다 (4.1, 4.3) |
| 3 | 한 씬에 컷씬이 여러 개 있을 수 있고, 이를 관리한다 | 씬마다 `CutsceneDirector` 하나가 등록 · 조건 검사 · 자동 재생 · 대기열 · 재생을 맡는다 (4.2, 5장) |
| 4 | 컷씬 하나에 코드 하나, 디렉토리로 관리한다 | 컷씬 ID = 대본 문서 = SO 에셋 = 코드 파일로 이름을 맞추고 구역별 폴더에 둔다 (8장) |
| 5 | 하드코딩을 없애고 SO에서 받는다. 없으면 에러를 낸다 | 데이터는 SO, 위치는 씬 마커. 재생 전에 전부 검사하고, 빠진 것이 있으면 모두 나열해 에러를 출력하고 재생하지 않는다 (4.6, 9장) |
| 6 | 대화 · 컷씬 시스템에 필요한 것을 추가한다 | 화자 프로필, 대사별 말풍선 · 효과음 · 신호, 화면 효과, 사운드, HUD 숨김, 런 타이머 정지 · 무적 (6장, 7장) |

---

## 2. 배경: 이전 프롤로그 구현의 문제

이전 구현(`PrologueCutsceneController` + 타임라인)은 삭제했다. 문제는 다음과 같았다.

- 타임라인 연출과 예비 코드 연출이 같은 장면을 따로 구현해, 한쪽만 고치면 어긋났다.
- 좌표(`new Vector3(-11.5f, 0.85f, 0f)`), 오브젝트 이름(`GameObject.Find("NPC_Guard")`), 에셋 경로(`AssetDatabase.LoadAssetAtPath(...)`)가 코드에 있었다.
- 에셋이 없으면 코드로 만든 대체 대사 등으로 조용히 진행해 실수가 드러나지 않았다.
- 말풍선을 대사 문구(`"쇳덩이"` 포함 여부)로 판단하고, 이를 위해 대화 UI 내부를 리플렉션으로 읽었다.
- 재생 조건 · 플래그 · 조작 잠금 · 복구가 한 클래스에 있어, 다음 컷씬에서 재사용할 수 없었다.

---

## 3. 전체 구조

```
[데이터: 프로젝트 에셋]                       [씬: 씬마다 배치]
CutsceneAsset (SO, 컷씬마다 1개 + 코드 1개) ──┐
  · 재생 조건 / 완료 플래그                   │ 등록      CutsceneDirector 프리팹 (씬당 1개)
  · 등장인물(CharacterProfileSO)             ├────────▶   · 재생 조건 검사 · 자동 재생 · 대기열
  · 대사(DialogueDataSO)                     │            · 사전 검사 → 재생 → 정리 → 완료 플래그 저장
  · 연출 수치                                │            · 조작 잠금 · 카메라 인계 · 말풍선 · 화면 효과 · 사운드
  · Play(CutsceneContext) 연출 코드          │
                                             │            CutsceneActor   (NPC에 부착, 플레이어는 자동)
CharacterProfileSO (인물마다 1개)            │            CutsceneMarker  (연출 위치 = 컷씬 + 키)
  · 표시 이름 · 표정별 초상화                 │            CutsceneTrigger (구역 진입 · 외부 호출로 재생)
                                             │            CutsceneSignal  (씬 고유 동작 연결)
DialogueDataSO (기존 + 대사별 연출 필드) ────┘            CutsceneNpcDialogue (NPC 상호작용으로 재생)
```

- **컷씬 = SO 에셋 + 그 SO의 클래스(연출 코드).** SO는 씬과 무관하므로 어느 씬에서든 재생할 수 있다.
- **씬 쪽은 "누가 어디에 있는가"만 제공한다.** 코드에는 좌표가 없다.
- **연출 코드는 `CutsceneContext`의 명령만 호출한다.** 명령 구현은 한곳에 모여 있어 모든 컷씬이 공유한다.

---

## 4. 구성 요소

### 4.1 `CutsceneAsset` — 컷씬 하나

추상 ScriptableObject. 컷씬마다 이를 상속한 클래스를 하나 만들고, 그 클래스로 SO 에셋을 하나 만든다.

**공통 필드 (부모 클래스)**

| 필드 | 설명 |
|---|---|
| `Id` | 컷씬 고유 ID. 대본 문서 ID와 같게 쓴다. 예: `CS_PROLOGUE_SHELTER` |
| `RequiredFlags` | 이 플래그가 모두 있어야 재생 가능 |
| `BlockedFlags` | 이 플래그 중 하나라도 있으면 재생 안 함 (보통 자기 완료 플래그) |
| `CompletionFlags` | 끝까지 재생했을 때 추가하고 저장할 영구 플래그 |
| `AutoPlay` | 씬 시작 시 조건이 맞으면 자동 재생 |
| `Priority` | 자동 재생 후보가 여럿이면 값이 큰 것 하나만 재생 |
| `LockPlayer` | 재생 중 플레이어 조작 잠금 |
| `HideHud` | 재생 중 `HideDuringCutscene`이 붙은 HUD 숨김 |
| `PauseGameplay` | 재생 중 런 타이머(경과 시간 · 습격) 정지 + 플레이어 무적 |

**컷씬 클래스가 직접 가지는 것**

- 등장인물(`CharacterProfileSO`), 대사(`DialogueDataSO`), 효과음 · 애니메이션 클립, 연출 수치를 `[SerializeField]`로 받는다.
- 필요한 마커 키를 `const string`으로 선언한다. 개수가 바뀌는 목록(예: 시설 몽타주)은 에셋에서 편집하는 문자열 배열로 받는다.
- `DeclareRequirements`에서 필요한 인물 · 마커 · 신호 · 대사 · 에셋을 선언한다. 선언하지 않은 것은 연출 코드에서 쓸 수 없다(쓰면 예외).
- `Play(CutsceneContext c)`에 연출 순서를 쓴다.

```csharp
[CreateAssetMenu(fileName = "CS_PROLOGUE_SHELTER", menuName = "ShotgunAndRun/Cutscene/Shelter/Prologue")]
public sealed class PrologueShelterCutscene : CutsceneAsset
{
    private const string PlayerStart = "PlayerStart";
    private const string PlayerStop = "PlayerStop";
    // ... GuardPost, GuardAside, LonaStart, LonaMeet, LonaHome, MeetCamera

    [SerializeField] private CharacterProfileSO _guard;
    [SerializeField] private CharacterProfileSO _lona;
    [SerializeField] private DialogueDataSO _guardSpotted;
    [SerializeField] private DialogueDataSO _main;
    [SerializeField] private string[] _facilityMarkers = { "Storage", "Workbench", "QuestBoard" };
    [SerializeField, Min(0.1f)] private float _playerWalkSpeed = 3f;
    // ... 연출 수치

    protected override void DeclareRequirements(CutsceneRequirements r)
    {
        r.Actor(_guard, nameof(_guard));
        r.Actor(_lona, nameof(_lona));
        r.Dialogue(_guardSpotted, nameof(_guardSpotted));
        r.Dialogue(_main, nameof(_main));
        r.Marker(PlayerStart, PlayerStop, GuardPost, GuardAside, LonaStart, LonaMeet, LonaHome, MeetCamera);
        r.Marker(_facilityMarkers);
        r.Check(_main == null || _main.Choices.Any(c => c.ChoiceId == AllianceChoice), "... 선택지가 없습니다.");
    }

    protected override IEnumerator Play(CutsceneContext c)
    {
        var guard = c.Actor(_guard);
        var lona = c.Actor(_lona);

        c.Teleport(c.Player, PlayerStart);
        c.Teleport(guard, GuardPost);
        c.CameraToPlayer(0f);
        yield return c.MoveTo(c.Player, PlayerStop, _playerWalkSpeed);

        yield return c.Bubble(guard, EmotionType.Alert, _guardAlertSeconds);
        yield return c.Say(_guardSpotted);            // Shock 말풍선은 대사 데이터에 있음

        var guardMove = c.MoveTo(guard, GuardAside, _guardStepSpeed);
        var lonaMove = c.MoveTo(lona, LonaMeet, _lonaWalkSpeed);
        c.CameraTo(MeetCamera, _meetCameraBlend);     // 기다리지 않음 → 동시 진행
        yield return c.WaitAll(guardMove, lonaMove);

        var talk = c.Say(_main);
        yield return talk;
        if (talk.ChoiceId == AllianceChoice) yield return c.Bubble(lona, EmotionType.Joy, 1.6f);
        // ... 시설 몽타주, 플레이어로 카메라 복귀
    }
}
```

**주의**
- SO는 모든 재생이 공유하고, 에디터에서는 Play 모드가 끝나도 값이 남는다. **재생 중 상태를 SO 필드에 저장하지 않는다.** 상태는 `Play` 안의 지역 변수에 둔다.
- 완료 플래그 추가, 조작 잠금 · 복구는 연출 코드에서 하지 않는다. `CutsceneDirector`가 공통으로 처리한다.

### 4.2 `CutsceneDirector` — 씬의 컷씬 관리자

씬마다 하나씩 둔다(프리팹 `Cutscene/Prefabs/CutsceneDirector`). 프리팹에 말풍선(`EmotionBubbleDisplay`), 화면 효과(`ScreenFader`), 효과음 · 배경음 `AudioSource`가 들어 있다.

- `SpeechBubbleDisplay`: 인물 머리 위에 대사 한 줄을 텍스트 말풍선(Overlay 캔버스의 템플릿 복제)으로 띄운다. `CutsceneDirector.ShowSpeech(target, text, seconds)`로 호출하며 컷씬 재생 중이 아니어도 쓸 수 있다(이미 본 컷씬 대신 재시도 회차에 한 줄 대사를 띄우는 용도). 연결되지 않아도 컷씬 검증에는 영향이 없다.

| 역할 | 내용 |
|---|---|
| 등록 | 이 씬에서 재생할 수 있는 `CutsceneAsset` 목록. 등록하지 않은 컷씬은 재생 요청 시 에러 |
| 자동 재생 | 플레이어가 준비되면 `AutoPlay` 컷씬 중 조건(5장)을 만족하는 것을 `Priority` 순으로 하나 재생 |
| 수동 재생 | `CanPlay(asset)`로 조건 확인, `Play(asset)` / `Play(id)`로 재생 |
| 대기열 | 재생 중에 요청이 오면 대기열에 넣고, 현재 컷씬이 끝나면 조건을 다시 확인해 재생 |
| 상태 공개 | 정적 `Current`, `IsPlaying`, `IsHudHidden`, 이벤트 `CutsceneStarted` · `CutsceneFinished(asset, completed)` |
| 공통 처리 | 사전 검사 → 잠금 · 정지 → 연출 실행 → 카메라 플레이어 복귀 → 정리 → 완료 플래그 저장 |

### 4.3 씬 연결 컴포넌트

**`CutsceneActor`** — 컷씬에 등장하는 인물.
- `Profile`(`CharacterProfileSO`)로 식별한다. 이름 문자열로 찾지 않는다. 같은 프로필의 인물이 씬에 둘 이상이면 사전 검사 에러.
- `DefaultMoveSpeed`: 이동 명령에 속도를 주지 않았을 때의 속도.
- `NpcCutsceneActor`: NPC에 붙인다. Transform 이동, Animator `IsMoving`으로 걷기, `SpriteRenderer.flipX`로 방향.
- `PlayerCutsceneActor`: Director가 재생 시작 때 플레이어에 자동으로 붙인다. 프로필은 Director의 `Player Profile`. 이동 중에는 모터를 끄고 위치를 직접 옮기며(콜라이더는 잠시 트리거로 바꿔 게이트 등에 끼지 않음), 걷기 애니메이션은 이동 입력으로, 방향은 조준점 고정으로 처리한다.

**`CutsceneMarker`** — 연출 위치.
- `Cutscene`(이 마커를 쓰는 `CutsceneAsset`) + `Key`로 식별한다. 컷씬이 다르면 같은 키를 써도 된다.
- `Cutscene`을 비우면 여러 컷씬이 함께 쓰는 공용 마커. 조회 순서: 해당 컷씬 전용 → 공용.
- 씬 뷰에 키 이름과 십자 표시가 그려진다. 컷씬 전용은 하늘색, 공용은 주황색.

**`CutsceneTrigger`** — 플레이어가 구역(트리거 콜라이더)에 들어오면, 또는 `Play()` 호출로 컷씬을 재생한다. 재생 조건은 Director가 검사한다.

**`CutsceneNpcDialogue`** — `NpcInteractable`의 Dialogue 칸에 연결하면, E 상호작용 때 대화 대신 컷씬을 재생한다. 재생할 수 없으면(이미 봤으면) 대체 대화(예: `NpcDialogueBridge`)를 재생한다. 컷씬 후 NPC UI를 열지 선택할 수 있다.

**`CutsceneSignal`** — `Key` → `UnityEvent`. 연출 코드는 `c.Signal("SpawnAmbush")`처럼 키만 부르고, 실제 동작(적 웨이브 생성, 오브젝트 켜기, UI 열기)은 씬 인스펙터에서 연결한다. 대사 데이터의 `Signal` 칸도 같은 신호를 부른다.

### 4.4 `CharacterProfileSO` — 인물 정보

인물마다 하나(`Assets/07.Datas/Characters/CHR_*.asset`). 대화와 컷씬이 함께 쓴다. 대화 시스템의 일부이므로 정식 코드(`02.Scripts/UI/Dialogue`)에 있다.

| 필드 | 용도 |
|---|---|
| `DisplayName` | 대화창 화자 이름 |
| `DefaultPortrait` | 표정을 지정하지 않은 대사의 초상화 |
| `Expressions` | 표정 키 → 초상화 (예: `Serious`, `Surprised`). 없는 키를 쓰면 경고 후 기본 초상화 |

**NPC 애니메이션 규칙**
- NPC는 공용 컨트롤러 `05.Animations/NPC/Base/NPC_Base.controller`(상태 Idle · Walk, Bool `IsMoving`) 하나를 쓰고, 인물별로 `<인물>.overrideController`에서 Idle · Walk 클립만 바꾼다. NPC 프리팹 4개(Guard · Lona · Grun · Elena)는 이 방식으로 교체했다.
- Cast · Reinforce처럼 컷씬에서만 쓰는 동작은 `c.PlayAnimation(actor, clip)`으로 재생한다. 컨트롤러에 상태를 추가하지 않고 Playables로 잠시 덮어쓴 뒤 원래 컨트롤러로 돌아간다. 클립은 컷씬 에셋의 필드로 받는다.

### 4.5 `CutsceneContext` — 연출 명령 모음

컷씬 한 번의 재생 상태이자, 연출 코드가 쓰는 함수 집합. 파일을 기능별로 나눈다(`partial class`).

**호출 규칙: 명령은 호출하는 순간 시작하고, 핸들을 돌려준다.**
- `yield return 핸들` → 그 명령이 끝날 때까지 기다린다.
- `yield return` 하지 않으면 기다리지 않고 다음 줄로 넘어간다. 동시 진행은 이렇게 만든다.
- 여러 개를 모두 기다리려면 `yield return c.WaitAll(a, b, …)`.
- 이유: 보통 `IEnumerator` 메서드는 `yield return` 하지 않으면 아예 실행되지 않고 에러도 나지 않는다. 명령이 즉시 시작하도록 해서 이 실수를 막는다.
- 연출 코드에서 `yield return`할 수 있는 값: `null`(다음 프레임), 명령 핸들, `IEnumerator`. `WaitForSeconds` 같은 Unity 대기 객체는 에러 → `c.Wait()`를 쓴다.
- 같은 인물에게 새 이동 명령을 내리면 이전 이동은 취소된다.

| 파일 | 명령 | 비고 |
|---|---|---|
| Flow | `Wait(sec)`, `WaitAll(…)`, `WaitUntil(cond)` | 게임 시간 기준 |
| Actor | `Actor(profile)`, `Player` | 선언한 인물만 |
| | `Teleport(actor, marker)`, `MoveTo(actor, marker, speed?)`, `MovePath(actor, speed, markers…)` | 이동 중 걷기 애니메이션 · 방향 자동 |
| | `Face(actor, left)`, `FaceTo(actor, actor \| marker)`, `FaceEachOther(a, b)` | |
| | `PlayAnimation(actor, clip)`, `StopAnimation(actor)`, `SetVisible(actor, bool)` | |
| Camera | `CameraTo(marker \| actor, blend)`, `CameraToPlayer(blend)`, `CameraShake(power, sec)` | 7.2 |
| Dialogue | `Say(dialogue)` → `DialogueHandle.ChoiceId` | 대사별 말풍선 · 효과음 · 신호는 대사 데이터가 처리 (6장) |
| | `Bubble(actor, emotion, sec?)`, `ClearBubble(actor)`, `ClearBubbles()` | 시간 생략 시 닫을 때까지 유지 |
| Screen | `FadeOut(sec, color?)`, `FadeIn(sec)`, `Flash(color, sec)`, `Letterbox(show, sec?)` | 덮개는 대화창 위, 레터박스는 대화창 아래 |
| Audio | `PlaySfx(clip)`, `PlayBgm(clip, fade)`, `StopBgm(fade)` | 7.5 |
| Game | `Signal(key)`, `AddFlag(flag)`, `HasFlag(flag)` | 완료 플래그는 Director가 자동 처리 |

새 명령이 필요하면 `CutsceneContext`에 추가한다. 개별 컷씬 클래스에서 다른 시스템을 직접 조작하지 않는다.

### 4.6 실행과 안전장치

**사전 검사 (재생 전)**
- 컷씬이 선언한 인물 · 마커 · 신호 · 대사와 비어 있는 에셋 칸, Director 자신의 부품(말풍선 세트 등), 카메라 · 대화 매니저 · 플레이어를 모두 확인한다.
- 대사 데이터도 검사한다: `Pause Game`이 켜져 있으면 에러(컷씬 연출이 멈춤), 말풍선이 있는 줄에 화자 프로필이 없으면 에러, 말풍선 화자와 `Signal` 키는 필요 목록에 자동 추가.
- 하나라도 없으면 **빠진 항목을 모두 나열해 `Debug.LogError`로 출력하고 재생하지 않는다.** 플레이어를 잠그지 않고 완료 플래그도 추가하지 않는다. 대체 값으로 진행하는 코드는 없다.

**정리 보장**
- Unity 코루틴은 `StopCoroutine`이나 오브젝트 비활성화로 멈추면 `finally`가 실행되지 않고, 예외가 나면 그 자리에서 끝나 뒤쪽 정리 코드가 실행되지 않는다. 그래서 Director가 자체 실행기(`CutsceneRoutine`)로 연출 코드를 한 프레임씩 실행하고, 정상 종료 · 예외 · 씬 전환(Director 비활성화) 어느 경우든 정리를 실행한다.
- 정리 항목: 진행 중 명령 취소, 대화창 닫기, 말풍선 · 화면 효과 · 컷씬 배경음 해제, 인물 상태 복구(걷기 · 클립 · 무적 · 모터 · 콜라이더), 카메라 인계, NPC 상호작용 · 런 타이머 · 플레이어 입력 복구.
- 예외로 멈춘 경우 완료 플래그를 추가하지 않는다. 다시 들어오면 다시 재생된다.
- 인물 위치 · 방향 · 보이기 상태는 연출 결과로 보고 되돌리지 않는다.

**스킵** — 구현하지 않는다 (12장 결정).

---

## 5. 재생 조건과 저장

- 플래그는 기존 영구 플래그(`ProgressData.Quests.Flags`)를 쓴다. 세이브 구조를 바꾸지 않는다.
- 재생 가능 = `RequiredFlags` 전부 보유 + `BlockedFlags` 하나도 없음.
- 끝까지 재생하면 `CompletionFlags`를 추가하고 `GameManager.SaveProgress()`를 호출한다.
- **런 중(던전) 컷씬은 탈출해야 저장한다** (2026-10-08 결정, 구현됨). 퀘스트의 "런 중에 기록하고, 탈출할 때 판정한다" 원칙(quest-system-implementation-plan 1장)과 맞춘다.
  - 런이 진행 중(`GameManager.CurrentRun`이 있고 끝나지 않음)일 때 끝난 컷씬의 `CompletionFlags`와 연출 중 `AddFlag`는 Director가 보류 목록에 모아 두고 저장하지 않는다.
  - Director가 `GameManager.RunEnded`를 구독해, 탈출(`RunOutcome.Extracted`)이면 보류 플래그를 영구 플래그에 추가하고 저장, 사망이면 버린다. Core(`GameManager`)는 고치지 않는다.
  - 재생 조건 검사(`CanPlay`)와 `HasFlag`는 영구 플래그와 보류 플래그를 함께 본다. 같은 런 안에서 다시 재생되지 않고, 사망하면 다음 런에서 다시 재생된다.
  - **예외: 완료 플래그 즉시 저장 옵션** (2026-10-10 결정, 구현됨). `CutsceneAsset`의 `Save Completion Flags Immediately`(`SaveCompletionFlagsImmediately`)를 켠 컷씬은 런 중이어도 `CompletionFlags`를 보류하지 않고 바로 저장한다. 처음 한 번만 강제로 보여 주고, 사망 후 재시도 런에서는 다시 재생하지 않기 위한 `_SEEN` 플래그용이다 (Q2 던전 컷씬).
- 진행 데이터(`GameManager`)가 없는 테스트 씬에서는 경고를 출력하고 조건 검사 없이 재생하며, 완료 플래그는 저장하지 못한다(경고).
- 플래그 이름 규칙: 컷씬 완료는 `FLAG_<컷씬 ID에서 CS_ 뺀 이름>_DONE`을 기본으로 한다. 대본에 이미 정한 플래그(`FLAG_PROLOGUE_COMPLETED` 등)가 있으면 그것을 쓴다.

**예시: 컷씬 흐름을 플래그로 잇기**

| 컷씬 | 씬 | 계기 | Required | Blocked | Completion |
|---|---|---|---|---|---|
| `CS_TUTORIAL_AWAKEN` | 튜토리얼 | 제단 상호작용 | — | `FLAG_TUTORIAL_AWAKEN_DONE` | `FLAG_TUTORIAL_AWAKEN_DONE` |
| `CS_PROLOGUE_SHELTER` | 쉘터 | 씬 시작 자동 | — | `FLAG_PROLOGUE_COMPLETED` | `FLAG_PROLOGUE_COMPLETED`, `FLAG_SHELTER_UNLOCKED` |
| `CS_Q1_INTRO` | 쉘터 | 로나 상호작용 | `FLAG_PROLOGUE_COMPLETED` | `FLAG_Q1_INTRO_DONE` | `FLAG_Q1_INTRO_DONE` |
| `CS_Q1_REQUEST` | 쉘터 | 그룬 상호작용 | `FLAG_Q1_INTRO_DONE` | `FLAG_Q1_ACCEPTED` | `FLAG_Q1_ACCEPTED` |
| `CS_SHELTER_COLLAPSE` | 쉘터 | 씬 시작 자동 | `FLAG_Q1_CLEARED` | `FLAG_SHELTER_COLLAPSE_DONE` | `FLAG_SHELTER_COLLAPSE_DONE` |
| `CS_Q1_DEBRIEF` | 쉘터 | 그룬 상호작용 | `FLAG_SHELTER_COLLAPSE_DONE` | `FLAG_Q1_DEBRIEF_DONE` | `FLAG_Q1_DEBRIEF_DONE` |
| `CS_Q2_BRIEFING` | 쉘터 | 로나 상호작용 | `FLAG_Q1_DEBRIEF_DONE` | `FLAG_Q2_BRIEFING_DONE` | `FLAG_Q2_BRIEFING_DONE`, `FLAG_Q2_ACCEPTED` |
| `CS_ELENA_AMBUSH` | 던전 | 엘레나 상호작용 | `FLAG_Q2_ACCEPTED` | `FLAG_ELENA_RESCUED`, `FLAG_ELENA_AMBUSH_SEEN` | `FLAG_ELENA_AMBUSH_SEEN` (즉시 저장) |
| `CS_ELENA_TELEPORT` | 던전 | 결박 해제 완료 | `FLAG_Q2_ACCEPTED` | `FLAG_ELENA_RESCUED`, `FLAG_ELENA_TELEPORT_SEEN` | `FLAG_ELENA_TELEPORT_SEEN` (즉시 저장) |
| `CS_Q2_PORTAL` | 쉘터 | 씬 시작 자동 | `FLAG_ELENA_RESCUED` | `FLAG_Q2_PORTAL_DONE` | `FLAG_Q2_PORTAL_DONE` |

Q1 컷씬의 대본과 재생 규칙은 [quests/CS_Q1_WHETSTONE.md](quests/CS_Q1_WHETSTONE.md), Q2는 [quests/CS_Q2_MAGE_RESCUE.md](quests/CS_Q2_MAGE_RESCUE.md). NPC 상호작용으로 재생하는 컷씬은 `CutsceneNpcDialogue`의 후보 목록에 넣고, 재생할 수 있는 후보가 있으면 NPC 머리 위 퀘스트 마커가 켜진다. `FLAG_Q1_CLEARED`는 `SQ_MAIN_001` 완료 시 퀘스트가 켠다. 구출 완료(`FLAG_ELENA_RESCUED`)는 퀘스트가 처리한다.

---

## 6. 대화 시스템 확장

`Assets/02.Scripts/UI/Dialogue`에 추가했다. 기존 대화 데이터와 호환되며, 새 칸이 비어 있으면 지금처럼 동작한다.

**`DialogueLine` 추가 필드**

| 필드 | 용도 |
|---|---|
| `Speaker` (`CharacterProfileSO`) | 화자. 지정하면 이름 · 초상화를 프로필에서 가져오며 `SpeakerName` · `SpeakerPortrait`보다 우선한다 |
| `Expression` | 프로필의 표정 키 (대본의 `Lona_Serious` 등에 대응) |
| `Bubble` (`EmotionType`) | 이 대사가 나오는 동안 화자 머리 위에 띄울 감정 말풍선. 다음 대사에서 닫힌다 |
| `Sfx` | 이 대사가 나올 때 재생할 효과음 (대본의 `SFX_Weapon_Rattle` 등) |
| `Signal` | 이 대사가 나올 때 부를 씬 신호(`CutsceneSignal`) 키 |

`Bubble` · `Sfx` · `Signal`은 컷씬 중에만 동작한다. 일반 NPC 대화에서는 무시된다.

**기타**
- `EmotionType`(None · Alert · Question · Shock · Silence · Sweat · Joy · Idea · Anger · Heart · Sad)을 대화 시스템으로 옮겼다. 컷씬 명령과 대사 데이터가 함께 쓴다.
- `IDialogueService.LineStarted(대화 데이터, 인덱스, 대사)` 이벤트를 추가했다. 컷씬이 대사별 연출을 처리할 때 쓴다.
- 컷씬 대사는 `Pause Game`을 꺼야 한다(사전 검사).

---

## 7. 다른 시스템과의 연결

### 7.1 플레이어
- 잠금: `PlayerInputReader.SetGameplayEnabled(false)`. 플레이어 코드는 바꾸지 않았다.
- 무적: `InvulnerabilityReason.Cutscene`을 추가했다. `PauseGameplay`가 켜진 컷씬 동안 켜진다.

### 7.2 카메라
- 컷씬 동안 Director가 카메라를 넘겨받는다. `PlayerCameraFollow`를 끄고 Main Camera를 직접 움직이며, 끝나면 플레이어 위치로 부드럽게 돌아온 뒤(`Camera Return Duration`) 다시 켠다.
- 플레이어 카메라 코드에는 컷씬 기능을 넣지 않는다. (이전에 `PlayerCameraFollow`에 넣었던 변경은 되돌렸다.)

### 7.3 던전 (게임 정지)
- `PauseGameplay` 컷씬 동안 `RunSession.SetPaused(true)`로 경과 시간과 습격 타이머를 멈춘다.
- 같은 조건에서 `CutsceneEnemyFreeze`가 적의 프레임 단위 동작(FSM · 이동 · 공격 판정 · 투사체 · 파괴 웨이브 스포너)을 끄고 속도를 0으로 만든다. 매 프레임 훑어 컷씬 중 소환된 적도 멈추고, 끝나면(예외 · 중단 포함) 끈 것만 다시 켠다. 애니메이터는 그대로 돈다. 코루틴으로 도는 공격 · 폭탄 · 습격 스포너는 멈추지 않는다(컷씬 중 플레이어 무적이라 피해 없음, 새로 나온 적은 다음 프레임에 멈춤).
  `MoveEnemies`는 위치를 직접 옮기므로 멈춘 적도 연출된다. Enemy 코드는 고치지 않았다.
- 탈출 대기(`RunSession.AdvanceExtractionHold`)는 `IsPaused`를 보지 않는다. 컷씬 중 플레이어가 탈출 구역 안에 있으면 컷씬 도중 탈출이 끝날 수 있다.
- `RunSession.SetPaused`는 값이 하나라서, 로딩(7.7)과 컷씬이 함께 쓰면 먼저 끝난 쪽이 다른 쪽의 정지를 풀어 버린다. 로딩은 `Time.timeScale`로 멈춰 겹치지 않게 한다.

### 7.4 HUD · NPC 상호작용
- HUD: HUD 루트에 `HideDuringCutscene`(CanvasGroup 필요)을 붙인다. 테스트 씬은 `QuickSlotBar`에 붙였다.
- NPC: `NpcInteractable`은 키보드를 직접 읽으므로, 컷씬 동안 Director가 씬의 `NpcInteractable`을 모두 끄고 끝나면 다시 켠다. NPC 코드는 바꾸지 않았다.

### 7.5 사운드
- 프로젝트에 공용 오디오 매니저가 없어서 Director의 `AudioSource` 2개(효과음 · 배경음)로 재생한다. 공용 오디오 매니저가 생기면 `CutsceneContext.Audio`의 구현만 바꾼다.

### 7.6 퀘스트
- 컷씬과 퀘스트는 플래그로만 연결한다(quest-system-implementation-plan 5장 권장안).
- 던전 컷씬의 플래그는 탈출해야 저장한다(5장).

### 7.7 씬 전환 · 로딩 (계획, 2026-10-08)

컷씬 관리자에 넣지 않고, 씬이 바뀌어도 유지되는 씬 전환 담당(가칭 `SceneTransition`, Core, `GameManager` 옆)을 따로 둔다. 컷씬 코드가 Core를 참조하므로 Core에서 컷씬을 부르면 어셈블리가 순환한다.

- 현재: `GameManager.LoadSceneFor`가 `SceneManager.LoadScene`(동기)을 써서 출격 순간 화면이 멈추고, 씬이 로드되자마자 런 타이머가 돌기 시작한 상태에서 상자(`RunStarted`) · 몬스터(스포너 `Start`)가 생성된다.
- 화면: 검은 화면 + 좌하단 로딩 애니메이션.
- 순서
  1. 페이드 아웃 → 로딩 화면 (unscaled time)
  2. `LoadSceneAsync`
  3. 씬 로드 → `BeginRun`은 지금처럼 실행(상자 생성). 로딩 중에는 `Time.timeScale = 0`, 조작 잠금
  4. 1~2프레임 대기(스포너 `Start` 생성 완료) + `Resources.UnloadUnusedAssets()` · `GC.Collect()`
  5. 페이드 인 → `timeScale` 복구 · 조작 해제 → "전환 끝" 신호
- 준비 완료 판정은 프레임 대기로 시작한다. 생성을 여러 프레임에 나누는 시스템이 생기면 그때 준비 작업 등록 방식을 추가한다.
- Director의 자동 재생(`AutoPlayWhenReady`)은 지금 플레이어가 생기고 1프레임 뒤에 시작하므로, 로딩 화면 뒤에서 재생되지 않게 "전환 끝" 신호를 기다리도록 바꾼다.

### 7.8 탈출 연출 (구현, 2026-10-10)

- 현재: 탈출 대기 완료 → `EndRun` → `RunEnded` → `GameResultUI`가 바로 결과창을 띄운다.
- 정산 · 저장은 지금처럼 `EndRun`에서 바로 하고, 결과창 표시만 늦춘다. 컷씬 도중 게임을 꺼도 아이템이 보존된다.
- 던전 씬에 `RunEndSequence`를 둔다: `RunEnded` 구독 → 탈출이면 `CS_TELEPORT_EXIT` 재생 → `CutsceneFinished` → `GameResultUI.ShowResult(result)`. 활성 `RunEndSequence`가 있으면 `GameResultUI.HandleRunEnded`는 탈출 결과를 건너뛴다(사망은 지금처럼 바로 표시).
- 탈출 컷씬 재생에 실패하거나 중간에 멈춰도 결과창은 반드시 띄운다.
- 탈출 컷씬 `CS_TELEPORT_EXIT`은 화이트아웃 1초 + 텔레포트 이펙트로, 약 2초 레터박스 · 카메라 연출 안을 대체한다. 탈출 지점이 여러 곳이므로 마커 없이 플레이어 기준으로 재생한다.

---

## 8. 디렉토리 구조와 이름 규칙

### 8.1 지금 위치 (샌드박스)

```
Assets/_Sandbox/Yechann/Cutscene/
├─ Core/          CutsceneAsset, CutsceneDirector, CutsceneRoutine, CutsceneRequirements, CutsceneHandle, CutsceneCamera
├─ Context/       CutsceneContext.cs, .Flow · .Actor · .Camera · .Dialogue · .Screen · .Audio · .Game
├─ Actors/        CutsceneActor, NpcCutsceneActor, PlayerCutsceneActor
├─ Scene/         CutsceneMarker, CutsceneTrigger, CutsceneSignal, CutsceneNpcDialogue
├─ Presentation/  EmotionBubbleDisplay, EmotionBubbleSetSO, ScreenFader, HideDuringCutscene
├─ Editor/        CutsceneAssetEditor (씬 검사 · 마커 생성), CutsceneDebuggerWindow
├─ Content/       <구역>/<컷씬 클래스>.cs           예: Shelter/PrologueShelterCutscene.cs
├─ Datas/         Common/EmotionBubbleSet.asset
│                 <구역>/CS_<ID>.asset, <구역>/Dialogues/CS_<ID>_<용도>.asset
└─ Prefabs/       CutsceneDirector.prefab

정식 코드에 추가한 것
Assets/02.Scripts/UI/Dialogue/   CharacterProfileSO, EmotionType, DialogueLine · DialogueDataSO · DialogueManager · IDialogueService 확장
Assets/02.Scripts/Player/Combat/ InvulnerabilityReason.Cutscene
Assets/02.Scripts/Core/Run/      RunSession.SetPaused
Assets/07.Datas/Characters/      CHR_Player · CHR_Guard · CHR_Lona · CHR_Grun · CHR_Elena
Assets/05.Animations/NPC/        Base/NPC_Base.controller, <인물>/<인물>.overrideController
```

- 네임스페이스: 샌드박스 단계는 `ShotgunAndRun.Sandbox.Cutscene`, 정식 통합 시 `ShotgunAndRun.Cutscene`.
- 정식 통합 시 위치: 시스템은 `02.Scripts/Cutscene/`, 컷씬별 코드는 `02.Scripts/Cutscene/Content/<구역>/`, 데이터는 `07.Datas/Cutscenes/<구역>/`, 대사는 `07.Datas/Dialogues/<구역>/`, 프리팹은 `03.Prefabs/Cutscene/`.

### 8.2 이름 규칙

| 대상 | 규칙 | 예 |
|---|---|---|
| 컷씬 ID · 대본 문서 · SO 에셋 | `CS_<내용>_<구역>` 대문자 | `CS_PROLOGUE_SHELTER` |
| 컷씬 클래스 | `<내용><구역>Cutscene` 파스칼 | `PrologueShelterCutscene` |
| 컷씬 대사 에셋 | `<컷씬 ID>_<용도>` | `CS_PROLOGUE_SHELTER_GuardSpotted` |
| 인물 프로필 | `CHR_<이름>` | `CHR_Lona` |
| 마커 오브젝트 | `CM_<컷씬 ID>_<키>` | `CM_CS_PROLOGUE_SHELTER_GuardAside` |
| 씬 계층 | 씬 루트에 `[Cutscene]` → 아래에 Director, 컷씬별 마커 그룹 | `[Cutscene]/CS_PROLOGUE_SHELTER/…` |

### 8.3 어셈블리

- 대화 시스템이 asmdef 없이 기본 어셈블리에 있어, 컷씬 시스템도 지금은 asmdef 없이 둔다.
- 정식 통합 때 대화 시스템에 `ShotgunAndRun.Dialogue`, 컷씬 시스템에 `ShotgunAndRun.Cutscene` asmdef를 함께 만든다.

---

## 9. 하드코딩 제거 대응표

| 이전 (PrologueCutsceneController) | 지금 | 없으면 |
|---|---|---|
| 기본 좌표 6개, `new Vector3(-11.5f, 0.85f, 0f)` | `CutsceneMarker` | 사전 검사 에러 |
| `GameObject.Find("NPC_Guard")` · `"NPC_Lona"` · `"NPC_Grun"` 등 | `CutsceneActor`(프로필) · 마커 | 사전 검사 에러 |
| `AssetDatabase.LoadAssetAtPath(...Lona_Walk.png / NPC_Guard.prefab / 대사 에셋)` | 컷씬 SO 필드 · NPC Override Controller | 사전 검사 에러 |
| 대사 에셋이 없을 때 코드로 만드는 대사 8줄 | 삭제 | 사전 검사 에러 |
| 대사 문구 비교로 말풍선 표시 + 리플렉션 | `DialogueLine.Bubble` | 말풍선 없이 진행 (선택 칸) |
| 말풍선 스프라이트 경로(`EmotionBubbles.png`) 자동 할당 | `EmotionBubbleSetSO` | 사전 검사 에러 |
| 이동 속도 · 대기 시간 · 카메라 시간 숫자 | 컷씬 SO의 연출 수치 필드 | 필드 기본값 |
| `"FLAG_PROLOGUE_COMPLETED"` 등 플래그 문자열 | `CutsceneAsset.CompletionFlags` · `BlockedFlags` | 플래그 없이 완료 |
| 게이트 차단벽(`Gate Block W`) 켜고 끄기 | 불필요 — 이동 중 플레이어 콜라이더를 트리거로 바꿈 | — |

---

## 10. 새 컷씬 만드는 절차

1. **대본 작성**: `Docs/quests/CS_<ID>.md`에 비트 · 대사 표 · 플래그 · 등장인물을 쓴다.
2. **대사 에셋**: 컷씬 데이터 폴더의 `Dialogues/`에 `DialogueDataSO`를 만든다. 줄마다 화자 프로필 · 표정 · 말풍선 · 효과음 · 신호를 대본 표에서 옮기고, `Pause Game`은 끈다.
3. **컷씬 코드**: `Content/<구역>/`에 `CutsceneAsset`을 상속한 클래스를 만들고, `DeclareRequirements`에 필요한 것을 선언한 뒤 `Play`에 연출 순서를 쓴다.
4. **컷씬 에셋**: 메뉴 Create → ShotgunAndRun → Cutscene → <구역>으로 `CS_<ID>.asset`을 만들고, ID · 플래그 · 인물 · 대사 · 수치를 채운다.
5. **씬 배치**:
   - 씬에 `CutsceneDirector` 프리팹이 없으면 놓는다.
   - 등장 NPC에 `NpcCutsceneActor`를 붙이고 프로필을 지정한다(Animator · SpriteRenderer는 자동으로 채워짐).
   - 컷씬 에셋 인스펙터 아래 **씬 검사**에서 "등록" 버튼으로 Director에 등록하고, **"빠진 마커 만들기"**로 마커를 만든 뒤 씬 뷰에서 위치를 잡는다.
   - 수동 재생이면 `CutsceneTrigger` 또는 `CutsceneNpcDialogue`를 놓는다. 씬 고유 동작은 `CutsceneSignal`로 연결한다.
6. **검증 · 재생**: 씬 검사 항목이 모두 ✔인지 확인하고, Play 모드에서 메뉴 ShotgunAndRun → Cutscene Debugger로 "재생 (조건 무시 · 상태 복원)" · "완료 플래그 지우기"를 쓰며 반복 확인한다. 디버그 재생은 씬의 진행 흐름(`ICutsceneDebugHost`, 튜토리얼은 `TutorialFlow`)이 그 컷씬의 시작 직전 상태를 만든 뒤 재생하며, 기본으로 완료 플래그를 저장하지 않는다. 자세한 설계는 `Docs/cutscene-debugger-state-plan.md`.

---

## 11. 진행 상황

| 단계 | 내용 | 상태 |
|---|---|---|
| 1. 핵심 | Asset · Director · 실행기 · 사전 검사 · 정리 · 인물(NPC · 플레이어) · 마커 · 흐름/인물/카메라/대사/말풍선 명령 · 플래그 | 완료. 프롤로그를 옮겨 ShelterCutsceneTestScene에서 끝까지 재생 확인 (2026-10-07) |
| 2. 대화 확장 | `CharacterProfileSO`, `DialogueLine` 연출 필드, `LineStarted` | 완료. 프롤로그 말풍선이 대사 데이터로 동작 |
| 3. 씬 연동 | Trigger · Signal · NpcDialogue, 페이드 · 점멸 · 레터박스, 사운드, HUD 숨김, 런 타이머 정지 · 무적 | 구현 완료. HUD 숨김 · 무적 · 정지 해제는 확인, 나머지(Trigger · Signal · NpcDialogue · 화면 효과 · 사운드)는 아직 실제 컷씬에서 쓰지 않아 미검증 |
| 4. 도구 | 컷씬 에셋 인스펙터(씬 검사 · 등록 · 마커 생성), Cutscene Debugger 창 | 구현 완료, 컴파일 확인 |
| 5. 정식 통합 | `02.Scripts/Cutscene`으로 이관, asmdef, NPC 프리팹에 `NpcCutsceneActor` 기본 부착, ShelterScene 적용 | 완료 (2026-10-10). 대화 · 컷씬 · 컷씬 에디터 asmdef 3개, 튜토리얼은 샌드박스 유지(Director는 `ICutsceneSpawnPoint`로 튜토리얼 구역을 찾음). ShelterScene에서 프롤로그 재생 확인 |
| 6. 게임 흐름 연동 | 씬 전환 · 로딩(7.7) → 탈출 연출(7.8) → 던전 컷씬 보류 플래그(5장) · 적 정지(7.3) | 일부 완료 (2026-10-10): 던전 컷씬 보류 플래그 · 적 정지 · 탈출 연출(CS_TELEPORT_EXIT). 씬 전환 · 로딩은 계획 (2026-10-08). 작업 계획: [game-flow-integration-plan.md](game-flow-integration-plan.md) |

---

## 12. 결정 사항 · 남은 일

**결정 (2026-10-07)**

| 항목 | 결정 |
|---|---|
| 작업 위치 | 샌드박스에서 시작 후 정식 이관 |
| 어셈블리 | 프로토타입은 asmdef 없이, 정식 통합 때 대화 시스템과 함께 asmdef |
| 대화 시스템 수정 | 담당자(김예찬) 허가로 6장 변경 반영 |
| 던전 컷씬 중 처리 | 플레이어 무적 + 런 타이머 정지 |
| 스킵 | 구현하지 않음 |
| NPC 애니메이션 | `NPC_Base` + Override 방식으로 교체 |
| `PlayerCameraFollow` 변경 | 되돌림 (7.2 방식으로 대체) |

**결정 (2026-10-08)**

| 항목 | 결정 |
|---|---|
| 던전 컷씬 플래그 저장 | 탈출에 성공해야 저장, 사망하면 버림 (5장) |
| 탈출 컷씬 | 약 2초, 스킵 불가 (7.8). 2026-10-10: 화이트아웃 1초 + 텔레포트 이펙트(CS_TELEPORT_EXIT)로 변경 |
| 로딩 화면 | 검은 화면 + 좌하단 로딩 애니메이션 (7.7) |
| 로딩 · 탈출 순서 담당 | 컷씬 관리자와 분리. 로딩은 Core의 씬 전환 담당, 탈출은 던전 씬의 런 종료 순서 컴포넌트 |

**결정 (2026-10-10)**

| 항목 | 결정 |
|---|---|
| 던전 컷씬 재시도 처리 | 스킵은 여전히 구현하지 않는다. 대신 `_SEEN` 플래그를 즉시 저장해 처음 한 번만 재생하고, 재시도 런에서는 말풍선 한 줄로 대체한다 (5장 예외) |
| Q2 컷씬 구성 | 브리핑 · 매복 · 텔레포트 · 포탈 점등 4개. 대본은 [quests/CS_Q2_MAGE_RESCUE.md](quests/CS_Q2_MAGE_RESCUE.md) |

**남은 일**
- ShelterCutsceneTestScene의 `Storage` · `QuestBoard` 마커는 씬에 해당 시설 오브젝트가 없어 임시 위치에 두었다. 실제 위치로 옮겨야 한다.
- 플레이어 코드(`InvulnerabilityReason`, `PlayerInvulnerability`)와 런 코드(`RunSession`) 변경은 각 담당자에게 공유.
- 6단계(게임 흐름 연동)에서 고칠 남의 코드: `GameManager`(`LoadSceneFor`를 씬 전환 담당으로 연결. 로딩 중 `timeScale = 0`이면 런 타이머도 멈추므로 런 시작 코드는 그대로), `GameResultUI`(`RunEnded` 직접 구독 제거), `RunSession.AdvanceExtractionHold`(정지 중 진행 안 함). 각 담당자와 협의.
- 좌하단 로딩 애니메이션 스프라이트가 아직 없다.
