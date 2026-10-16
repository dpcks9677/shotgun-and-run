# [퀘스트 대본집] Q1 대장간의 숫돌 (Forging the Lead)

> **문서 버전:** v1.0 (2026-10-10)  
> **관련 기획서:** [quest-story-concept.md](../quest-story-concept.md) 3장 ([탐사 퀘스트 1] 대장간의 숫돌), [cutscene-system-design.md](../cutscene-system-design.md), [CS_Q2_MAGE_RESCUE.md](CS_Q2_MAGE_RESCUE.md) (후속)  
> **적용 대상 씬:** `ShelterScene`  
> **에셋:**  
> - 퀘스트: `Assets/07.Datas/Quests/SQ_MAIN_001.asset`  
> - 퀘스트 아이템: `Assets/07.Datas/QuestItems/PrecisionWhetstone.asset` (`quest.precision_whetstone`, 아이콘 `Assets/04.Sprites/Quest/quest-icons.png`, 생성기 `Docs/tools/spritegen/items/quest_icons.py`)  
> - 컷씬 SO: `Assets/07.Datas/Cutscenes/Shelter/CS_Q1_INTRO.asset`, `CS_Q1_REQUEST.asset`, `CS_SHELTER_COLLAPSE.asset`, `CS_Q1_DEBRIEF.asset`  
> - 대사 데이터: `Assets/07.Datas/Dialogues/Shelter/CS_Q1_*.asset`, `CS_SHELTER_COLLAPSE_*.asset`, 그룬 평상시 대사 `Assets/07.Datas/Dialogues/Grun_BeforeIntro.asset`, `Grun_Q1Waiting.asset`  
> - 인물 프로필: `CHR_Lona`, `CHR_Grun`, `CHR_Guard1` ~ `CHR_Guard3`

---

## 1. 전체 흐름

```
[프롤로그 끝 (FLAG_PROLOGUE_COMPLETED)] — 로나 머리 위 ! 마커
  ① CS_Q1_INTRO    로나에게 E → 제한 시간 조언, 카메라가 그룬을 비춤        → FLAG_Q1_INTRO_DONE
        ↓ 그룬 머리 위 ! 마커
  ② CS_Q1_REQUEST  그룬에게 E → 샷건 감정, 정밀 숫돌 의뢰, SQ_MAIN_001 수주 → FLAG_Q1_ACCEPTED
        ↓
[출격: 쉘터 계단 (FLAG_Q1_ACCEPTED 전에는 잠김) → E1 고정 스폰 → Z1 대장간 존 상자에서 정밀 숫돌]
        ↓ 숫돌을 들고 탈출 → SQ_MAIN_001 완료 (300 XP, 철괴 1 · 납덩이 1 · 치유 약초 3)  → FLAG_Q1_CLEARED
[쉘터 귀환]
  ③ CS_SHELTER_COLLAPSE  씬 시작 자동 → 로나 · 경비병 3명이 입구 붕괴를 지켜봄  → FLAG_SHELTER_COLLAPSE_DONE
        ↓ 그룬 머리 위 ! 마커
  ④ CS_Q1_DEBRIEF  그룬에게 E → 숫돌 감사, 무기 강화 해금, "로나가 할 말이 있다더군" → FLAG_Q1_DEBRIEF_DONE
        ↓ 로나 머리 위 ! 마커
  CS_Q2_BRIEFING   로나에게 E → SQ_MAIN_002 수주 (CS_Q2_MAGE_RESCUE.md 4장)
```

- 사망하거나 숫돌 없이 탈출하면 퀘스트는 수주 상태로 남고, 다음 런에 숫돌이 다시 배치된다. 사망 시 숫돌은 잃는다.
- 숫돌은 탈출 정산 때 퀘스트 완료와 함께 소모된다. 그룬 보고 컷씬은 숫돌을 건넨 것으로 연출한다.

## 2. 플래그 및 재생 조건

| 컷씬 | 계기 | Required | Blocked | Completion |
| :--- | :--- | :--- | :--- | :--- |
| `CS_Q1_INTRO` | 로나 상호작용 | `FLAG_PROLOGUE_COMPLETED` | `FLAG_Q1_INTRO_DONE` | `FLAG_Q1_INTRO_DONE` |
| `CS_Q1_REQUEST` | 그룬 상호작용 | `FLAG_Q1_INTRO_DONE` | `FLAG_Q1_ACCEPTED` | `FLAG_Q1_ACCEPTED` + `SQ_MAIN_001` 수주 |
| `CS_SHELTER_COLLAPSE` | 씬 시작 자동 (우선순위 10) | `FLAG_Q1_CLEARED` | `FLAG_SHELTER_COLLAPSE_DONE` | `FLAG_SHELTER_COLLAPSE_DONE` |
| `CS_Q1_DEBRIEF` | 그룬 상호작용 | `FLAG_SHELTER_COLLAPSE_DONE` | `FLAG_Q1_DEBRIEF_DONE` | `FLAG_Q1_DEBRIEF_DONE` |
| `CS_Q2_BRIEFING` | 로나 상호작용 | `FLAG_Q1_DEBRIEF_DONE` | `FLAG_Q2_BRIEFING_DONE` | `FLAG_Q2_BRIEFING_DONE`, `FLAG_Q2_ACCEPTED` + `SQ_MAIN_002` 수주 |

- `FLAG_Q1_CLEARED`는 컷씬이 아니라 `SQ_MAIN_001`의 해금 플래그(`UnlockFlags`)다. 탈출 정산에서 켜진다.
- 쉘터 컷씬은 런 밖이므로 완료 플래그가 바로 저장된다.

## 3. 씬 연결

| 대상 | 구성 |
| :--- | :--- |
| 로나 (`NPC_Lona`) | `CutsceneNpcDialogue` 후보 `[CS_Q1_INTRO, CS_Q2_BRIEFING]`, 대체 대화 = 기존 `NpcDialogueBridge` (`Lona_Greeting`) |
| 그룬 (`NPC_Grun`) | `CutsceneNpcDialogue` 후보 `[CS_Q1_REQUEST, CS_Q1_DEBRIEF]`, 대체 대화 = `FlagDialogueSelector` |
| 그룬 평상시 대사 (`FlagDialogueSelector`) | `FLAG_Q1_INTRO_DONE` 없음 → `Grun_BeforeIntro` (UI 안 열림) / `FLAG_Q1_DEBRIEF_DONE` 없음 → `Grun_Q1Waiting` (UI 안 열림) / 그 밖 → `Grun_Greeting` + 강화 · 창고 UI |
| 로나 평상시 대사 (`FlagDialogueSelector`) | `FLAG_Q1_CLEARED` 없음 → `Lona_Q1Waiting` ("꼭 살아서 돌아오도록 해.", UI 안 열림) / 그 밖 → 기존 `Lona_Greeting`("돌아왔구나" + 퀘스트 보기). 컷씬 후보가 재생 가능할 때는 컷씬이 우선 |
| 퀘스트 마커 | 각 NPC 자식 `QuestMarker` (감정 말풍선 `Alert` 스프라이트). 재생할 수 있는 후보 컷씬이 있을 때만 켜짐 |
| 무기 강화 | `WeaponUpgradeUI._unlockFlag = FLAG_Q1_DEBRIEF_DONE`. 플래그 전에는 창이 열리지 않음 |
| 창고 | `NPC/StorageInteraction`: 창고 구역 가운데 상자(`StorageChest` 타일, 셀 -10,7) 앞. E → "상자다. 이때까지 모은 아이템을 담아두었다." → "상자를 연다"로 `ShelterStorageUI`. 그룬과 별도로 항상 열림 |
| 작업대 | `NPC/WorkbenchInteraction`: 그룬 위 `Workbench_Large` 타일 앞. E → "작업대다. 던전에서 쓸만한 도구를 만들 수 있을 것 같다." → "소모품 제작"으로 `ConsumableCraftingUI` |
| 계단 출격 구역 | `RunEntranceZone._requiredFlags = [FLAG_Q1_ACCEPTED]`. 잠겨 있으면 `PlayerSpeechTrigger`가 플레이어 말풍선 "아직은 아니야. 그룬에게 먼저 가 보자." |
| 붕괴 연출 | `[Cutscene]/CS_SHELTER_COLLAPSE`: 마커 6개, 신호 `ShelterCollapse` (`Collapsed + RightSortie (2)` 켜기, `Stairs (1)` 끄기), 잔해 `CollapseDebris` (`CutsceneShakeDebris`, 튜토리얼 설정 복사, 추격 낙하 · 막음 더미 없음) |

---

## 4. ① CS_Q1_INTRO: 로나의 안내

```
[1비트] 로나와 마주 선다. 인사 · 제한 시간 조언.
[2비트] 카메라가 대장간의 그룬을 비추고(GrunCamera), 로나가 그룬에게 보낸다. 끝나면 카메라가 플레이어에게 돌아온다.
```

| # | 화자 | 말풍선 | 대사 |
| :-: | :--- | :--- | :--- |
| 01 | 로나 | - | "왔구나. 숨은 좀 돌렸어?" |
| 02 | 로나 | `Alert` | "던전에 오래 있지 마. 3분이 지나면 탈출로가 열리고, 10분을 넘기면 놈들 포위망에 갇혀." |
| 03 | 로나 | `Idea` | "나가기 전에 대장간의 그룬부터 만나 봐. 네 무기를 봐 줄 거야." |

## 5. ② CS_Q1_REQUEST: 그룬의 의뢰

```
[1비트] 그룬과 마주 선다. 샷건 감정과 정밀 숫돌 의뢰.
[마무리] SQ_MAIN_001 수주.
```

| # | 화자 | 말풍선 | 대사 |
| :-: | :--- | :--- | :--- |
| 01 | 그룬 | `Question` | "그게 그 아티팩트요? 어디 좀 봅시다." |
| 02 | 그룬 | `Shock` | "놀라운 물건이군! 내부 기계 장치가 정교하게 맞물려 있소." |
| 03 | 그룬 | `Sweat` | "하지만 강선은 찌꺼기로 막혔고 노리쇠 결합부도 닳았소. 이대로면 언제 터져도 이상하지 않소." |
| 04 | 그룬 | - | "제대로 손보려면 연마 도구가 있어야 하오. 북서쪽 Z1에 고블린 놈들이 군용 도검을 찍어내던 대장간이 있소." |
| 05 | 그룬 | `Idea` | "거기 공구함에서 정밀 숫돌을 찾아오시오! 숫돌만 있으면 확실하게 벼려 드리겠소." |

## 6. ③ CS_SHELTER_COLLAPSE: 주 진입로 붕괴

| 항목 | 내용 |
| :--- | :--- |
| **등장 액터** | `Lona`, `Guard 1~3`. 플레이어는 연출 내내 숨김 |
| **배치** | 무너지는 입구 주변 (`CollapseLona`, `CollapseGuard1~3`), 모두 입구(`CollapseEntrance`)를 본다. 카메라 `CollapseCamera` |
| **귀환** | 한탄이 끝나면 페이드 없이 카메라를 스폰 지점으로 옮기고, 플레이어가 순간이동 도착 연출(`CS_TELEPORT_ARRIVE`)로 나타난다. 이어 카메라가 로나에게 돌아가는 동안 무리가 속도 8로 플레이어에게 달려와 마주 선다. 기둥을 피하도록 인물마다 경유지 마커 2개(`<인물 마커 키>Route1` · `Route2`)를 지난다 |
| **마무리** | 페이드 아웃 동안 로나 · 경비병을 원래 자리 · 방향으로 되돌리고 플레이어를 보이게 한다 |

```
[1비트] 페이드 인. 낮은 울림과 약한 진동, 경비병 2 · 3 경계(Alert). 경비병 1의 경고.
[2비트] 굉음과 큰 진동. 신호 ShelterCollapse로 계단이 무너진 계단으로 바뀌고, 입구 주변에 잔해 · 먼지가 떨어진다.
[3비트] 로나의 한탄.
[4비트] 카메라를 스폰 지점으로 → 플레이어 순간이동 등장.
[5비트] 카메라가 로나에게 돌아가며 무리가 플레이어에게 달려옴 (속도 8, 경유지 경로).
[6비트] 로나의 맞이 대사 3줄.
[마무리] 페이드 아웃 → 원위치 → 페이드 인.
```

| # | 화자 | 말풍선 | 대사 |
| :-: | :--- | :--- | :--- |
| 01 | 경비병 1 | `Shock` | "입구 쪽이다! 천장이 내려앉는다, 다들 물러서!" |
| 02 | 로나 | `Anger` | "빌어먹을... 고블린 놈들이 주 진입로를 폭파시켰어!" |
| 03 | 로나 | `Sweat` | "통로가 완전히 잔해로 막혔네. 이대로면 던전에 발도 못 들이겠어." |
| 04 | 로나 | `Joy` | "돌아왔구나! 정말 다행이야." (`CS_SHELTER_COLLAPSE_Reunion`) |
| 05 | 로나 | `Sweat` | "이쪽에서도 습격이 있었어서 쉽사리 구하러 갈 수 없었거든." |
| 06 | 로나 | - | "우선 재정비를 먼저 하도록 해." |

- 엘레나 언급은 넣지 않는다. 엘레나와 우회로 설명은 `CS_Q2_BRIEFING`이 맡는다.

## 7. ④ CS_Q1_DEBRIEF: 그룬의 보고

| # | 화자 | 말풍선 | 대사 |
| :-: | :--- | :--- | :--- |
| 01 | 그룬 | `Joy` | "숫돌을 가져왔구려! 그 난리 속에서 용케 살아 돌아왔소." |
| 02 | 그룬 | - | "이거면 됐소. 이제 그 총, 제대로 벼려 드리리다. 재료만 챙겨 오시오." |
| 03 | 그룬 | `Silence` | "...그리고 로나가 할 말이 있다더군. 가 보시오." |

- 컷씬이 끝나도 강화 창은 열지 않는다. 다음 상호작용부터 기존 인사(`Grun_Greeting`)와 강화 · 창고 UI가 열린다.

## 8. 그룬 평상시 대사

| 상태 | 대사 | UI |
| :--- | :--- | :--- |
| 로나 안내 전 | "로나부터 만나 보시오. 쉘터 일은 길드장이 정하오." | 안 열림 |
| Q1 진행 중 (보고 전) | "정밀 숫돌은 Z1 대장간 공구함에 있소. 북서쪽 진입로로 나가면 바로 앞이오." | 안 열림 |
| 보고 후 | 기존 `Grun_Greeting` | "무기 강화" 선택 시 강화 · 창고 |
