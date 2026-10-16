# 컷씬 디버거 상태 복원 변경 계획서

작성일: 2026-10-09 · 상태: **구현 완료, 컴파일 확인. Play 모드 검증(6절) 대기** · 대상: `Assets/_Sandbox/Yechann/Cutscene` (CutsceneDebuggerWindow · CutsceneDirector) 와 `Tutorial/Scripts/TutorialFlow.cs`

## 1. 문제

Cutscene Debugger에서 보고 싶은 컷씬의 "재생"을 누르면, 그 컷씬이 원래 재생되는 시점의 게임 상태(플레이어 · HUD · 적 · 씬 오브젝트)로 맞춰진 뒤 재생되어야 한다. 현재는 그렇게 되지 않는다. 컷씬 안의 연출은 나오지만 앞뒤 상태가 어긋나 있어, 컷씬을 단독으로 검수할 수 없다.

## 2. 원인 분석 (코드 확인 결과)

### 2-1. 디버거가 하는 일은 "위치 이동"뿐이다
`CutsceneDirector.TeleportPlayerToCutscene`([CutsceneDirector.cs:331](../Assets/_Sandbox/Yechann/Cutscene/Core/CutsceneDirector.cs#L331))은 다음만 한다.

- 플레이어 위치 이동 · 속도 0
- 화면 덮개 걷기
- 카메라 스냅
- `TutorialFlow.PrepareForDebugCutscene` 호출

그 외 상태는 건드리지 않는다. 컷씬이 시작 상태를 전제로 하는 항목이 이렇게 빠져 있다.

| 상태 | 정해 주는 곳 (정상 진행 시) | 디버그 재생 시 |
|---|---|---|
| 무기 든 상태 (armed) | Escape가 false, ShotgunGet이 true | 직전 값 그대로 |
| 체력 · 탄창 · 퀵슬롯 소모품 | Escape(체력 3, 소모품 비움), ShotgunGet(탄창 3), Faint(체력 1, 구급상자 지급) | 직전 값 그대로 |
| HUD 열림 (HudGate) | Run 시작 `HudGate.ResetAll`, 컷씬 끝의 `ShowHud` | 직전 값 그대로 |
| 사망 방지 (DeathBlocked) | `SkyBattle` · `ArenaBattle`이 켜고 끔 | 직전 값 그대로 |
| 팔각방 문 · 샷건 픽업 · 상자 | Run 안에서 `SetActive` | 직전 값 그대로 |
| 무너진 잔해 · 샷건 숨김 · 적 정리 (CutsceneSignal) | 컷씬이 `c.Signal`로 한 방향으로만 켬 | 한 번 일어나면 되돌릴 수 없음 |
| 적 배치 | Run이 컷씬 전에 미리 소환 | `PrepareForDebugCutscene`이 SkyIntro · ArenaIntro만 처리 |
| Time.timeScale · 팝업 · 암전 | ShootGuide 등이 직접 조작 | 직전 값 그대로 |

### 2-2. TutorialFlow가 동시에 계속 돌고 있다
`TutorialFlow.Run`은 씬 시작과 함께 코루틴으로 돌고([TutorialFlow.cs:77](../Assets/_Sandbox/Yechann/Tutorial/Scripts/TutorialFlow.cs#L77)), 디버거는 이를 모른다. 결과는 다음과 같다.

- 디버그 재생 시점에 Run은 임의의 단계(예: Escape 직후의 Move 구역 대기)에 있다. 다른 컷씬을 틀면 `Director.Play`가 이미 재생 중이면 대기열에 넣어 Run이 요청한 컷씬과 순서가 뒤섞인다.
- 디버그로 텔레포트한 위치가 우연히 TutorialZone이면 Run이 그 구역을 통과한 것으로 보고 의도치 않게 다음 단계를 시작한다.
- 디버그 컷씬이 끝나도 Run은 그 단계 다음으로 이어지지 않는다. 즉 "여기서부터 이어서 진행"이 불가능하다.
- `Time.timeScale = 0`(ShootGuide)이나 팝업이 떠 있는 상태로 디버그 재생이 시작될 수 있다.

### 2-3. 디버그 재생이 실제 세이브를 오염시킨다
`Finish(completed: true)`는 항상 `SaveCompletionFlags`를 호출하고, Chase 컷씬의 끝 신호는 `TutorialFlow.Complete`를 불러 `FLAG_TUTORIAL_COMPLETED`를 저장하고 다음 씬을 로드한다. 디버그로 Chase를 보면 튜토리얼이 완료 처리되고 씬이 넘어간다.

### 2-4. 컷씬이 바꾼 상태가 쌓인다
같은 컷씬을 반복 재생하면 컷씬이 한 일(소모품 지급, 적 소환, 잔해 켜기)이 누적되거나 되돌아가지 않는다. 앞 단계로 되돌아가 다시 보는 것은 지금 구조로는 불가능하다.

## 3. 목표와 범위

- 컷씬을 고르면 그 컷씬의 "시작 직전 상태"를 만든 뒤 재생한다. 앞 단계 · 뒤 단계 어느 쪽으로 점프해도 같아야 한다.
- 디버그 재생은 세이브(완료 플래그 · 튜토리얼 완료 · 씬 이동)에 영향을 주지 않는다. 필요할 때만 켜는 옵션으로 둔다.
- 선택 사항으로, 재생이 끝난 뒤 그 지점부터 튜토리얼을 실제 흐름대로 이어서 진행한다.
- 범위는 튜토리얼 씬(TutorialMapScene)이다. 쉘터 프롤로그 등 다른 씬은 동일한 확장점을 쓰도록만 열어 두고, 이번에 상태 표는 만들지 않는다.

## 4. 설계

### 4-1. 단계(Stage)를 한곳에 정의한다
`TutorialFlow`에 단계 목록(컷씬 10개 + 사이 구간)을 명시하고, 각 단계의 "시작 직전 상태"를 `ApplyStageState(stage)` 한 함수에 둔다. Run의 진행 순서와 같은 파일에 두어 둘이 어긋나지 않게 한다. 상태는 누적 규칙이 아니라 단계마다 완전한 값으로 적는다(앞 단계를 실제로 돌리지 않으므로).

단계별 시작 직전 상태 초안 (Run 코드에서 도출. 장면 연결 부분은 구현 시 씬에서 확인한다):

| 단계 | 무기 | HUD | 사망 방지 | 적 | 씬 오브젝트 |
|---|---|---|---|---|---|
| Escape | 맨손 | 전부 닫힘 | 꺼짐 | 없음 | 잔해 안 무너짐, 샷건 숨김 |
| ShotgunSpot | 맨손 | Health · Dash | 꺼짐 | 없음 | 잔해 무너짐, 샷건 픽업 (확인 필요: 컷씬 중 보이는지) |
| ShotgunGet | 맨손 | Health · Dash | 꺼짐 | 없음 | 샷건 픽업 켜짐 → 컷씬이 숨김 |
| SkyIntro | 총 · 탄창 3 | + Ammo | 켜짐 | 하늘색 3마리 hold | 팔각방 문 닫힘 |
| SkyClear | 총 · 탄창 ≥1 | + Ammo | 꺼짐 | 하늘색 전멸 | 팔각방 문 열림 |
| ChestSpot | 총 | + Ammo | 꺼짐 | 보라 방 8마리 hold | 상자 닫힘 |
| ArenaIntro | 총 | + Ammo | 켜짐 | 보라 방 8마리 hold | 상자 비어 있음 |
| FaintDefeat / Cleared | 총 | + Ammo | 켜짐 | 보라 방 (Defeat: 생존 / Cleared: 전멸) | 체력 1 (Defeat) |
| Chase | 총 | + QuickSlot | 꺼짐 | 없음 | 소형 구급상자 사용 후, 체력 > 1 |

공통 초기화: `Time.timeScale = 1`, 팝업 숨김, 화면 덮개 제거, 대사 닫기, 진행 중 컷씬 중단, 기존 적 정리.

### 4-2. 되돌릴 수 없는 신호를 되돌릴 수 있게 한다
`CutsceneSignal`은 한 방향 이벤트(잔해 켜기, 샷건 숨기기, 적 정리)라 상태를 되감을 수 없다. 단계 상태에서 직접 지정할 수 있도록 해당 씬 오브젝트(`CutsceneShakeDebris`, `TutorialPickup`, 팔각방 문 등)에 `SetState(bool)` 류 공개 메서드를 쓴다. 이미 있는 것(`TutorialPickup.SetActive`)은 그대로 쓰고, 없는 것만 추가한다. 신호에 연결된 UnityEvent가 실제로 무엇을 켜는지는 TutorialMapScene에서 확인해야 한다(이번 조사에서는 씬 파일을 열어 보지 않았다).

### 4-3. Director와 Tutorial의 결합을 끊는다
`CutsceneDirector`는 지금 `ShotgunAndRun.Sandbox.Tutorial`을 직접 참조해 `PrepareTutorialEnvironmentForDebug`를 부른다. 이를 인터페이스로 바꾼다.

```csharp
public interface ICutsceneDebugHost
{
    // 디버그 재생 직전. 진행 중이던 흐름을 멈추고 asset의 시작 직전 상태를 만든다.
    void PrepareForDebug(CutsceneAsset asset);
    // 디버그 재생이 끝난 뒤. resume이 true면 그 지점부터 실제 흐름을 이어서 진행한다.
    void OnDebugFinished(CutsceneAsset asset, bool resume);
}
```

TutorialFlow가 구현하고, Director는 씬의 구현체를 찾아 호출한다. 다른 씬도 같은 인터페이스만 구현하면 된다.

### 4-4. TutorialFlow를 점프 가능하게 만든다
- `Run()`을 `RunFrom(Stage start)`로 나누고 기존 호출은 `RunFrom(Stage.Escape)`로 둔다. 단계 사이의 코드는 그대로다.
- `PrepareForDebug`에서 `StopCoroutine`으로 Run을 멈추고 구독한 이벤트(대시 · 발사 · 탄창 · 체력)를 해제한 뒤 4-1의 공통 초기화와 `ApplyStageState`를 실행한다.
- `OnDebugFinished(resume: true)`면 해당 컷씬 다음 단계부터 `RunFrom`으로 이어간다. false면 멈춘 채로 둔다.
- Run 코루틴이 잡고 있는 이벤트 해제는 `try/finally`나 단계별 정리 함수로 한곳에 모아 중단에도 새지 않게 한다.

### 4-5. 디버그 재생은 세이브를 건드리지 않는다
- `CutsceneDirector.Play`에 디버그 플래그를 두거나 `PlayDebug(asset, options)`를 추가한다. 디버그 재생이면 `SaveCompletionFlags`를 건너뛴다.
- Chase의 `TutorialComplete` 신호는 디버그 재생일 때 `Complete`가 완료 플래그 저장과 씬 이동을 하지 않도록 `TutorialFlow`가 디버그 상태를 확인한다.
- 디버거 UI에 "완료 플래그 저장" 체크를 두고 기본은 끈다.

### 4-6. 디버거 창 변경
- 컷씬 목록 각 항목의 "재생 (조건 무시)" 버튼이 위 흐름(PrepareForDebug → 재생 → OnDebugFinished)을 호출한다. 따로 "위치로 이동"은 유지하되 상태 복원도 함께 한다.
- 옵션 세 개: 연출 위치로 텔레포트(기존), 끝난 뒤 이어서 진행, 완료 플래그 저장.
- 현재 단계, 마지막으로 적용한 상태(무기 · HUD · 사망 방지 · 남은 적 수)를 창 위쪽에 보여 주어 상태가 맞는지 바로 확인할 수 있게 한다.
- 컷씬 재생 중이면 "중단" 버튼을 둔다(Director에 `Abort` 공개).

### 4-7. 대기열과 이어서 진행 (보강)
- 디버그 재생은 `CutsceneDirector.PlayDebug`가 맡는다. 재생 중인 컷씬은 `Abort`로 중단하고 대기열은 비운 뒤 시작한다. 일반 `Play`처럼 대기열에 넣으면 `Finish`가 꺼낼 때 `CanPlay`를 다시 검사해, 이미 완료한 컷씬(자기 완료 플래그가 BlockedFlags)이 조용히 버려지기 때문이다.
- "이어서 진행"은 `RunFrom(다음 단계)`를 바로 시작하므로 `FLAG_TUTORIAL_COMPLETED`로 튜토리얼을 건너뛰는 검사(Run 첫머리)를 거치지 않는다. 완료한 세이브에서도 이어진다.

## 구현 결과 (2026-10-09)

| 파일 | 변경 |
|---|---|
| `Core/ICutsceneDebugHost.cs` (신규) | 인터페이스 + `CutsceneDebugOptions` |
| `Core/CutsceneDirector.cs` | `PlayDebug` · `PrepareDebug` · `Abort` · `IsDebugRun`. 디버그 재생은 옵션을 켜지 않으면 완료 플래그를 저장하지 않음. Tutorial 직접 호출 제거 |
| `Tutorial/Scripts/TutorialFlow.cs` | `Step` 목록으로 분리(`RunFrom`), `DebugStage`별 `StateFor` 표, 이벤트 구독 기록(`Hook`)으로 중단 시 해제, `ICutsceneDebugHost` 구현, 디버그 중 `Complete` 무시 |
| `Cutscene/Scene/CutsceneShakeDebris.cs` | `ResetState` · `SetCollapsed(bool)` (잔해 · 입구 더미 · 추격 더미를 즉시 붕괴 전/후로) |
| `Cutscene/Scene/CutsceneEnemySpawner.cs` | `Unregister` |
| `Cutscene/Editor/CutsceneDebuggerWindow.cs` | 옵션 3개(텔레포트 · 이어서 진행 · 플래그 저장), 중단 · "상태 · 위치만 맞추기" 버튼, 진행 상태 표시 |

씬 파일(TutorialMapScene)은 건드리지 않았다. 문 · 잔해의 처음 모습은 `Resolve` 시점에 기록하고, 잔해는 씬에서 `FindFirstObjectByType`으로 찾는다.

계획과 달라진 점
- 상자(`LootChest`)는 열림 · 내용물을 되돌리는 공개 API가 없어 복원하지 않았다. ChestSpot 이후를 디버그로 보면 상자는 직전 상태 그대로다.
- 샷건 픽업 오브젝트는 씬 시작부터 보이고 `HideShotgunPickup` 신호로 숨겨지는 것을 씬 파일에서 확인했다. ShotgunSpot · ShotgunGet 시작 상태는 "보임 · 상호작용 꺼짐"이다.
- `StateFor`의 체력 값(Escape~ArenaIntro 3, FaintDefeat 1, FaintCleared 3, Chase 2)은 정상 진행 중 값이 아니라 대표값이다. 실제 최대 체력에 따라 클램프된다.

## 5. 작업 순서

1. TutorialFlow에서 Run을 단계로 분리하고 `PrepareForDebug` · 이벤트 정리를 추가한다. 이것만으로 2-2의 충돌이 사라진다.
2. `ApplyStageState` 단계별 상태 표를 구현한다. 필요한 씬 오브젝트에 상태 지정 메서드를 추가한다.
3. 디버그 재생 시 세이브 · 씬 이동 억제를 넣는다. Chase 디버그 재생이 튜토리얼을 완료 처리하는 문제를 막는 항목이라 2번과 같이 들어가야 안전하다.
4. `ICutsceneDebugHost`로 Director를 분리하고 디버거 창 UI를 바꾼다.
5. 문서(`Docs/cutscene-system-design.md`의 도구 절)와 `Docs/quests/CS_TUTORIAL.md`에 디버그 규칙을 갱신한다.

## 6. 검증

각 컷씬에 대해 아래를 에디터 Play 모드에서 확인한다.

- 씬 시작 직후(Escape 재생 중) · 다른 단계 중간 · 컷씬 재생 중 세 가지 상황에서 점프한다.
- 시작 직전 상태 표와 실제 값이 일치하는지 디버거 창의 상태 표시와 눈으로 확인한다.
- 앞 단계 → 뒤 단계 → 다시 앞 단계(예: Chase → Escape → SkyIntro)로 이동해도 상태가 누적되지 않는지 본다.
- 같은 컷씬을 두 번 연속 재생해도 결과가 같은지 본다 (소모품 중복 지급 · 적 이중 소환 없음).
- 디버그 재생 뒤 세이브 파일의 플래그가 그대로이고 Chase에서 씬이 넘어가지 않는다.
- "이어서 진행"을 켜고 각 컷씬 후 실제 흐름(안내 팝업 · 전투 · 다음 컷씬)이 정상으로 이어진다.
- 콘솔에 `CutsceneDirector` 사전 검사 오류와 이벤트 해제 누락 경고가 없다.

## 7. 위험과 열린 질문

- 상태 표가 Run 코드와 따로 늙을 수 있다. Run을 고치면 표도 고치도록 같은 파일에 가까이 두고, 단계 추가 시 컴파일 오류가 나도록 `switch`로 모든 단계를 다루게 한다.
- 적 소환 상태(`hold`, `EnemyStateMode`)와 `CutsceneEnemyGroup` 정리는 이미 Destroy 후 비동기라서, 점프 직후 같은 프레임에 새로 소환하면 이전 적과 겹칠 수 있다. 점프 시 이전 적 정리를 먼저 확정한다.
- 샷건 줍기 전의 `ShotgunSpot` 중 픽업이 보여야 하는지는 컷씬 코드를 더 확인해야 한다. 현재 `PrepareForDebugCutscene`은 ShotgunSpot에서도 픽업을 켠다.
- 쉘터 프롤로그 등 다른 씬의 시작 직전 상태는 이번 범위가 아니다. 필요하면 같은 인터페이스로 후속 작업한다.
- 작업 트리에 이미 디버거 · Director · 튜토리얼 씬의 미커밋 수정이 있다(텔레포트 기능 추가분). 이 계획은 그 위에서 진행한다.
