# [퀘스트 대본집] CS_PROLOGUE_SHELTER: 절망 속의 불씨 (Sparks in the Dark)

> **문서 버전:** v1.1  
> **작성자:** 내러티브 & 레벨 디자인 (수정: 컷씬 연출 개편)  
> **관련 기획서:** [quest-story-concept.md](../quest-story-concept.md) 3장 ([쉘터 진입] 프롤로그 완료), [cutscene-system-design.md](../cutscene-system-design.md)  
> **적용 대상 씬:** `ShelterScene`, `ShelterCutsceneTestScene`  
> **연계 에셋 경로:**  
> - 대화/컷신 데이터:  
>   - 도입부: `Assets/_Sandbox/Yechann/Cutscene/Datas/Shelter/Dialogues/CS_PROLOGUE_SHELTER_Intro.asset`  
>   - 조우 외침: `Assets/_Sandbox/Yechann/Cutscene/Datas/Shelter/Dialogues/CS_PROLOGUE_SHELTER_GuardSpotted.asset`  
>   - 메인 회담: `Assets/_Sandbox/Yechann/Cutscene/Datas/Shelter/Dialogues/CS_PROLOGUE_SHELTER_Main.asset`  
>   - 승강기 설명(선택지 2 후속): `.../Dialogues/CS_PROLOGUE_SHELTER_Elevator.asset`  
>   - 합류 후 한마디: `.../Dialogues/CS_PROLOGUE_SHELTER_Rest.asset`  
>   - 시설 소개: `.../Dialogues/CS_PROLOGUE_SHELTER_Tour.asset`  
> - 컷씬 SO: `Assets/_Sandbox/Yechann/Cutscene/Datas/Shelter/CS_PROLOGUE_SHELTER.asset`  
> - 후속 퀘스트 데이터: `Assets/07.Datas/Quests/SQ_MAIN_001.asset`  

---

## 1. 퀘스트 기본 정보 (시스템 명세)

| 항목 | 데이터 타입 | 설정 내용 / 식별자 | 비고 |
| :--- | :--- | :--- | :--- |
| **컷신 고유 ID** | `string` | `CS_PROLOGUE_SHELTER` | 컷신 관리 및 재생 식별자 |
| **연계 퀘스트** | `string` | `SQ_PROLOGUE_END` | 완료 즉시 `SQ_MAIN_001` 자동 수주 |
| **이벤트 분류** | `enum` | `PrologueCompletion` / `HubUnlock` | 쉘터 거점 최초 개방 연출 |
| **발동 조건** | 씬 & 플래그 | `FLAG_PROLOGUE_COMPLETED` 미보유 상태로 `ShelterScene` 진입 | 1회 한정 자동 재생 |
| **등장 액터** | 프리팹/트랜스폼 | `Player` (샷건 소지), `Lona` (길드장 로나), `Guard 1` (중앙 경비병), `Guard 2` (중앙 경비병), `Guard 3` (문 앞 보초 경비병) | 씬 내 배치 액터 |
| **해금 시설** | 명세서 10장 | 창고(`ShelterStorageUI`), 무기 개조대(`WeaponUpgradeUI`), 의뢰 게시판(`QuestCatalogSO`) | 연출 종료 직후 개방 |
| **영구 플래그** | `ProgressData` | `FLAG_PROLOGUE_COMPLETED`, `FLAG_SHELTER_UNLOCKED` | 중복 재생 방지 및 시설 상태 저장 |

---

## 2. 연출 흐름 및 사전 비트 (Staging Beats)

```
[1비트: 페이드 인 및 의문의 충격음 (Intro)]
- 화면 페이드 인 (ScreenFader 1.0초).
- 카메라 시작 구도: 쉘터 중앙 작전 구역 부감 (CenterCamera).
- 쉘터 중앙에 Guard 1과 Guard 2가 모여 있음.
- Lona가 상단 집무 구역(LonaStart)에서 중앙(LonaMeet)으로 걸어 내려옴.
- 좌측 먼 곳(외부 게이트 방향)에서 둔탁한 샷건 발포음이 2~3회 울림 (SFX_Shotgun_Distant_Far, 스테레오 좌측 패닝).
- Guard 1: "길드장님. 충격음이 왼쪽 입구에서 계속 들리는 것 같습니다."
- Lona: "최근엔 이런 소리가 들린 적 없었는데..."
       ↓
[2비트: 접근하는 굉음과 요격 태세 (Escalation & Formation)]
- 샷건 총성이 한층 커지며 3발 연속으로 격발 (SFX_Shotgun_Distant_Mid, 볼륨 증폭 및 접근감).
- Guard 2: "점점 가까워지는 것 같습니다!"
- Guard 3 (서쪽 게이트 문 앞 보초, 대사만 출력): "무언가 접근 중입니다!"
- 바깥에서 샷건 격발음이 연속적으로 울려 퍼짐 (SFX_Shotgun_Continuous).
- Lona: "바리케이트 뒤로 대형 갖춰! 전원 전투 준비!"
- Guard 1과 Guard 2가 서쪽 바리케이트 뒤(Barricade_Guard1, Barricade_Guard2)로 이동하여 전투 대형을 갖춤.
       ↓
[3비트: 방벽 진입 및 탈진 (Player Entrance - 기존 1비트 연동)]
- 카메라가 서쪽 복도의 플레이어에게 부드럽게 옮겨 가 따라감 (1.0초 블렌드).
- 서쪽 복도는 튜토리얼 탈출 복도처럼 길고 왼쪽 끝이 뚫려 있음 (x -66까지 바닥, 화면 밖 x -45.5에 보이지 않는 차단벽).
- 플레이어(무기를 든 상태)가 복도 저편(PlayerStart)에서 뒤(서쪽)를 겨누고 일정 간격으로 쏘며 뒷걸음질로 게이트를 지나 지정 위치(PlayerStop)까지 들어옴.
- 바리케이트 뒤에 Guard 1과 Guard 2가 무기를 겨눈 채 전투 대형을 유지하고 있음. 도착하면 카메라가 대면 구도(MeetCamera)로 옮겨 감.
       ↓
[4비트: 경비병의 경계와 확인 (Identification - 기존 2비트 연동)]
- 서쪽 문 앞의 Guard 3가 석궁을 겨누며 극도로 경계 (Alert 감정 말풍선). 플레이어가 돌아서서 경비병과 마주침.
- 다가온 실루엣이 괴물이 아닌 사람임을 확인하고 무기를 내림: "괴물이 아니야... 사람이잖아?! 대장님! 생존자입니다!"
- Guard 3가 옆으로 비켜섬 (GuardAside).
       ↓
[5비트: 길드장 접근 및 대면 (Confrontation - 기존 3비트 연동)]
- 바리케이트 뒤에서 로나가 앞으로 걸어 나와 플레이어 두 걸음 앞에 멈춰 선다 (LonaMeet, FaceEachOther).
- 카메라는 로나와 플레이어를 함께 잡는 대면 구도(MeetCamera)로 집중.
- 바리케이트 뒤의 Guard 1, Guard 2는 전투 대형을 유지한 채 배경에 배치됨.
       ↓
[6비트: 대화 시퀀스 및 동맹 제안 (Main Dialogue)]
- 메인 다이얼로그 UI 출력.
- 로나: "다른 생존자가 또 있었을 줄이야..."
- 로나: "꼴을 보니 혼자인 모양이군. 환영해, 비록 무덤 같은 곳이지만."
- 플레이어: "여기는 어디죠...?"
- 로나의 상황 설명(고대 군사 요새, 차단된 거대 승강기, 식량과 보급품 고갈).
- 로나의 아티팩트 질문: "...잠깐. 손에 쥔 그 흉흉한 쇳덩이는 뭐지? 이질적이게 생긴 것을 보니 던전의 아티팩트인가본데?"
- 플레이어: "길을 잃고 헤매다 눈앞에서 발견했어요. 어떻게 동작하는지는 잘 모르겠네요."
- 로나의 직관: "당신이 쥔 그 이질적인 물건이 진짜라면... 어쩌면 여길 뚫고 나갈 유일한 돌파구가 될지도 모르겠어."
- 로나의 제안: "서로 돕지. 당신에겐 거점과 정비가 필요하고, 우리에겐 여기서 나갈 방법을 찾을 사람이 필요하니까."
- 선택지 1차 분기:
  • [선택지 1: ALLIANCE_ACCEPT] "서로 돕도록 하죠." -> 로나: "피부터 닦고 천막에서 좀 쉬어." -> 7비트로 진행
  • [선택지 2: ASK_ELEVATOR] "승강기가 닫혔다는 건 무슨 뜻입니까?"
    -> 로나: "Z4 구역 중앙 승강기의 동력이 끊겼거든. 작동시킬 열쇠와 화력이 있어야만 지상으로 올라갈 수 있어."
    -> 로나: "그건 그렇고... 합류할 의사는 있나?"
    -> 2차 분기 [선택지 2-1: JOIN_ACCEPT] "돕겠습니다." -> 로나: "피부터 닦고 천막에서 좀 쉬어." -> 7비트로 진행
       ↓
[7비트: 거점 구역 안내 및 마무리 (Facility Tour & Outro)]
- 로나의 안내 대사와 함께 카메라가 시설들을 순차적으로 포커스 (기존 무언 몽타주 대체):
  1) (대장간으로 카메라 이동 - Workbench)  
     로나: "아티팩트는 북동쪽의 그룬에게 한 번 물어봐. 유능한 대장장이니까 믿고 맡겨도 좋을 거야."
  2) (게시판으로 카메라 이동 - QuestBoard)  
     로나: "숨 좀 돌리면 계단 옆의 게시판으로 와. 앞으로 어떻게 할 지 이야기해보자고."
  3) (창고로 카메라 이동 - Storage)  
     로나: "혹시나 챙긴 짐들이 있다면 창고에 둬도 돼. 안전하게 맡아줄게."
  4) (Lona에게 카메라 복귀 - MeetCamera)  
     로나: "그러면 조금 있다가 보자고."
- 플레이어가 천막에서 휴식한다: 화면 페이드 아웃(1.0초) → 검은 화면 1.0초 유지(`_restBlackSeconds`) → 페이드 인. 검은 화면 동안 로나와 경비병들(기사 · 도끼병 · 병사)을 컷씬 전에 서 있던 자리와 방향으로 되돌리고, 플레이어를 오른쪽 천막 앞 `PlayerRest` 마커(x 9, y -5.8)로 옮겨 쉘터 맨손 상태로 되돌린다.
- 페이드 인 뒤 플레이어 혼잣말 2줄(`CS_PROLOGUE_SHELTER_AfterRest`): "드디어 한 숨 돌렸어." (땀 말풍선) / "엘레나는 위쪽 게시판 앞에 있다고 했었지?" (물음표 말풍선). 쉘터 BGM은 페이드 인과 함께 시작한다.
- 컷씬 종료 및 정리: 플레이어를 쉘터 맨손 상태로 되돌림, 카메라 플레이어 복귀, 조작권 복구, HUD 표시 복구, 화면 페이드 인.
- 완료 플래그 저장 (FLAG_PROLOGUE_COMPLETED, FLAG_SHELTER_UNLOCKED) 및 후속 퀘스트(SQ_MAIN_001) 수주.
```

- **발동 방식:** 씬 로드 시 `CutsceneDirector`가 `FLAG_PROLOGUE_COMPLETED` 미보유 상태를 확인하고 자동 재생 (`AutoPlay = true`).
- **카메라 연출:**
  - 시작 시점: 화면 페이드 인과 함께 쉘터 중앙 작전 구역 부감 (`MeetCamera` 또는 `CenterCamera`).
  - 교전 태세: Lona의 명령 후 서쪽 게이트 입구 및 대면 위치(`MeetCamera`)로 부드럽게 전환.
  - 거점 소개: 대장간(`Workbench`) -> 게시판(`QuestBoard`) -> 창고(`Storage`) -> 로나(`MeetCamera`) 순으로 순회 후 페이드 아웃.
- **사운드 및 앰비언스:**
  - 시작: 쉘터 지하 앰비언스 페이드 인.
  - 1차 총성: 좌측 원거리 샷건 격발음 2~3회 (`SFX_Shotgun_Distant_Far`, Stereo Pan Left).
  - 2차 총성: 음량이 커진 샷건 격발음 3회 (`SFX_Shotgun_Distant_Mid`).
  - 3차 총성: 연속되는 샷건 격발음 (`SFX_Shotgun_Continuous`).
  - 플레이어 도착: 샷건 약실 냉각음 및 잔향 (`SFX_Shotgun_Cooled`).
  - 테마곡: 대면 및 시설 안내 구간에서 잔잔한 첼로/희망 테마 선율(`BGM_Haven_Melancholy` / `BGM_Hope_Swell`) 연결.

---

## 3. 대사 및 지문 테이블

> **PauseGame 설정:** `false` (배경의 횃불 불꽃과 환경 파티클 애니메이션 유지)  
> **수정 안내:** 대사 내용을 변경하거나 행을 추가/삭제하여 대본을 편집할 수 있습니다.

| # | 화자 | 초상화 / 감정 | 지문 및 카메라 연출 | 출력 대사 본문 | 시스템 이벤트 / SFX |
| :-: | :--- | :--- | :--- | :--- | :--- |
| 01 | 경비병 1 | `Guard_Alert` | 화면 페이드 인. 쉘터 중앙에 경비병 1과 2가 모여 있다. 상단에서 걸어 내려오는 로나를 향해 보고한다. | "길드장님. 충격음이 왼쪽 입구에서 계속 들리는 것 같습니다." | `SFX_FadeIn`, `SFX_Shotgun_Distant_Far` (좌측 원거리 총성 2~3회) |
| 02 | 로나 | `Lona_Serious` | 중앙으로 다가온 로나가 서쪽 통로를 날카롭게 응시한다. | "최근엔 이런 소리가 들린 적 없었는데..." | - |
| 03 | 경비병 2 | `Guard_Startled` | 총성이 한층 커지자 무기를 고쳐 잡으며 다급하게 외친다. | "점점 가까워지는 것 같습니다!" | `SFX_Shotgun_Distant_Mid` (볼륨 증폭 총성 3회) |
| 04 | 경비병 3 | `Guard_Alert` | (서쪽 문 앞 보초) 게이트 방벽 쪽에서 다급하게 외친다. (대사만 출력) | "무언가 접근 중입니다!" | `Emotion_Alert` |
| 05 | 로나 | `Lona_Determined` | 서쪽 바리케이트를 가리키며 단호하게 명령을 내린다. 경비병 1과 2가 바리케이트 뒤로 이동하여 전투 대형을 갖춘다. | "바리케이트 뒤로 대형 갖춰! 전원 전투 준비!" | `SFX_Shotgun_Continuous`, 경비병 1·2 바리케이트 대형 이동 |
| 06 | 경비병 3 | `Guard_Startled` | (서쪽 문 앞) 플레이어가 연기를 내뿜으며 진입하자 석궁을 겨누다 사람임을 확인하고 무기를 내린다. | "괴물이 아니야... 사람이잖아?! 대장님! 생존자입니다!" | `SFX_Weapon_Rattle`, `Emotion_Shock` |
| 07 | 로나 | `Lona_Default` | 바리케이트 뒤에서 걸어 나온 로나가 플레이어를 보며 놀란 기색을 드러낸다. | "다른 생존자가 또 있었을 줄이야..." | `WP_Camera_Glide` (`MeetCamera`) |
| 08 | 로나 | `Lona_Default` | 플레이어 두 걸음 앞에 멈춰 서서 지친 행색을 바라본다. (경비병 1·2는 바리케이트 뒤 전투 대형 유지) | "꼴을 보니 혼자인 모양이군. 환영해, 비록 무덤 같은 곳이지만." | - |
| 09 | 플레이어 | `None` | 가쁜 숨을 몰아쉬며 주변을 둘러본다. | "여기는 어디죠...?" | - |
| 10 | 로나 | `Lona_Serious` | 팔짱을 끼며 요새의 상황을 차분하게 설명한다. | "이곳은 지하 깊은 곳에 세워진 고대 군사 요새야. 밖으로 통하는 거대 승강기가 굳게 닫혔고, 우린 고립됐지." | - |
| 11 | 로나 | `Lona_Serious` | 닫힌 바깥 통로를 턱짓으로 가리킨다. | "식량과 보급품은 바닥났고 밖엔 괴물들이 득실대. 다들 서서히 말라죽어가던 참이었어." | - |
| 12 | 로나 | `Lona_Surprised` | 시선이 플레이어의 손에 쥔 샷건에 꽂히며 눈을 가늘게 뜬다. | "...잠깐. 손에 쥔 그 흉흉한 쇳덩이는 뭐지? 이질적이게 생긴 것을 보니 던전의 아티팩트인가본데?" | `SFX_Heartbeat_Notice`, `Emotion_Question` |
| 13 | 플레이어 | `None` | 무거운 총열을 내려다보며 덤덤하게 대답한다. | "길을 잃고 헤매다 눈앞에서 발견했어요. 어떻게 동작하는지는 잘 모르겠네요." | - |
| 14 | 로나 | `Lona_Determined` | 로나의 눈빛에 옅은 희망이 스친다. 카메라가 조금 더 집중된다. | "당신이 쥔 그 이질적인 물건이 진짜라면... 어쩌면 여길 뚫고 나갈 유일한 돌파구가 될지도 모르겠어." | `BGM_Hope_Swell` |
| 15 | 로나 | `Lona_Default` | 안쪽 안식처 시설들을 향해 손짓하며 제안한다. | "서로 돕지. 당신에겐 거점과 정비가 필요하고, 우리에겐 여기서 나갈 방법을 찾을 사람이 필요하니까." | 선택지 UI 활성화 (1차) |
| 16 | 로나 | `Lona_Default` | (대장간으로 카메라 이동) 카메라가 북동쪽 대장장이 그룬의 작업대를 비춘다. | "아티팩트는 북동쪽의 그룬에게 한 번 물어봐. 유능한 대장장이니까 믿고 맡겨도 좋을 거야." | 카메라 `Workbench` 포커스 |
| 17 | 로나 | `Lona_Default` | (게시판으로 카메라 이동) 카메라가 중앙 계단 옆의 의뢰 게시판을 비춘다. | "숨 좀 돌리면 계단 옆의 게시판으로 와. 앞으로 어떻게 할 지 이야기해보자고." | 카메라 `QuestBoard` 포커스 |
| 18 | 로나 | `Lona_Default` | (창고로 카메라 이동) 카메라가 남동쪽 창고 보관함을 비춘다. | "혹시나 챙긴 짐들이 있다면 창고에 둬도 돼. 안전하게 맡아줄게." | 카메라 `Storage` 포커스 |
| 19 | 로나 | `Lona_Default` | (Lona에게 카메라 복귀) 카메라가 다시 로나에게 돌아온다. 로나가 엷은 미소를 짓는다. | "그러면 조금 있다가 보자고." | 카메라 `MeetCamera` 복귀, 페이드 아웃 (`SFX_FadeOut`) |

---

## 4. 선택지 및 분기 처리

### 선택지 1차 구성
- **[선택지 1]** `"서로 돕도록 하죠."` (Choice ID: `ALLIANCE_ACCEPT`)
  - **후속 대사:** *"피부터 닦고 천막에서 좀 쉬어."*
  - **결과:** 거점 시설 소개 몽타주(16~19번 대사)로 직행.
- **[선택지 2]** `"승강기가 닫혔다는 건 무슨 뜻입니까?"` (Choice ID: `ASK_ELEVATOR`)
  - **후속 대사 1:** *"Z4 구역 중앙 승강기의 동력이 끊겼거든. 작동시킬 열쇠와 화력이 있어야만 지상으로 올라갈 수 있어."*
  - **후속 대사 2:** *"그건 그렇고... 합류할 의사는 있나?"*
  - **결과:** 2차 선택지 UI 활성화.

### 선택지 2차 구성 (선택지 2 선택 시)
- **[선택지 2-1]** `"돕겠습니다."` (Choice ID: `JOIN_ACCEPT`)
  - **후속 대사:** *"피부터 닦고 천막에서 좀 쉬어."* (선택지 1 후속 대사와 동일)
  - **결과:** 거점 시설 소개 몽타주(16~19번 대사)로 직행.

### 대화 종료 후 거점 소개 및 해금 연출 시퀀스 (16~19번 연계)
선택지 완료 후 카메라가 시설을 순차적으로 비추며 로나의 대사가 출력됩니다:
1. 카메라가 **대장간/무기 개조대**로 이동: 로나 대사 16번 출력 (`[무기 개조대 개방] 샷건 5대 파츠 확인 가능`).
2. 카메라가 **의뢰 게시판**으로 이동: 로나 대사 17번 출력 (`[의뢰 게시판 해금] 메인 스토리 및 서브 의뢰 활성화`).
3. 카메라가 **창고 보관함**으로 이동: 로나 대사 18번 출력 (`[창고 활성화] 파밍 자원 보관 가능`).
4. 카메라가 **로나**에게 복귀: 로나 대사 19번 출력 후 화면 페이드 아웃 (1.0초).
5. 카메라가 플레이어에게 복귀하고 화면 페이드 인되며 조작권 복구.

### 영구 플래그 저장 및 후속 퀘스트 연계
```csharp
var progress = GameManager.Instance.Progress;
progress.AddPermanentFlag("FLAG_PROLOGUE_COMPLETED");
progress.AddPermanentFlag("FLAG_SHELTER_UNLOCKED");

// 다음 메인 스토리 퀘스트 자동 수주 (대장간의 숫돌)
QuestService.AcceptQuest("SQ_MAIN_001");
```

---

## 5. Unity 에셋 변환 및 시스템 연동 가이드

1. **등장 액터 구성:**
   - `Lona`: 상단 집무 위치(`LonaStart`)에서 중앙(`LonaMeet`)으로 이동. 이후 플레이어와 대면.
   - `Guard 1`: 쉘터 중앙(`GuardCenter1`)에서 대기, 로나의 명령 후 서쪽 바리케이트(`BarricadeGuard1`)로 이동하여 대형 유지.
   - `Guard 2`: 쉘터 중앙(`GuardCenter2`)에서 대기, 로나의 명령 후 서쪽 바리케이트(`BarricadeGuard2`)로 이동하여 대형 유지.
   - `Guard 3`: 서쪽 게이트 문 앞(`GuardPost`) 보초. 대사 외침 후 플레이어 진입 시 물러섬(`GuardAside`).
2. **DialogueDataSO 분할/구성:**
   - `CS_PROLOGUE_SHELTER_Intro`: 표의 01~05번 대사 (경비병 1, 로나, 경비병 2, 경비병 3, 로나).
   - `CS_PROLOGUE_SHELTER_GuardSpotted`: 표의 06번 대사 (경비병 3의 생존자 확인).
   - `CS_PROLOGUE_SHELTER_Main`: 표의 07~15번 대사 및 선택지 1·2 (선택지 2 후속 대화 및 2-1 포함).
   - `CS_PROLOGUE_SHELTER_Elevator`: 선택지 2 후속 대사 2줄 + 2차 선택지 `JOIN_ACCEPT`.
   - `CS_PROLOGUE_SHELTER_Rest`: 선택지 1 · 2-1 공통 후속 대사 ("피부터 닦고 천막에서 좀 쉬어.").
   - `CS_PROLOGUE_SHELTER_Tour`: 표의 16~19번 시설 소개 대사 (줄마다 카메라 이동과 연동: `Workbench` → `QuestBoard` → `Storage` → `MeetCamera`).
   - 인물 프로필: `CHR_Knight`(기사, 경비병 1 자리) · `CHR_Axeman`(도끼병, 경비병 2 자리) · `CHR_Soldier`(병사, 경비병 3 자리 · 게이트 보초), `CHR_Lona`. 컷씬은 프로필로 인물을 찾으므로 인물마다 프로필이 따로 필요하다. 2026-10-14에 모두 같은 외형이던 경비병 3명을 전투 NPC 프리팹(`NPC_Knight` · `NPC_Axeman` · `NPC_Soldier`)으로 교체했고, 상호작용 인사 대사도 `Knight_Greeting` · `Axeman_Greeting` · `Soldier_Greeting`으로 각자 다르게 했다(컷씬 대사의 말하는 사람 참조도 같은 프로필로 바뀜).
3. **씬 배치 및 마커 키:**
   - 기존 마커: `PlayerStart`, `PlayerStop`, `GuardPost`, `GuardAside`, `LonaStart`, `LonaMeet`, `LonaHome`, `MeetCamera`, `Storage`, `Workbench`, `QuestBoard`.
   - 신규 마커: `GuardCenter1`, `GuardCenter2` (중앙 집결 위치), `BarricadeGuard1`, `BarricadeGuard2` (바리케이트 전투 대형 위치).
   - 연출 보조 마커: `CenterCamera` (중앙 작전 구역 부감), `LonaCenter` (1비트에서 로나가 멈추는 중앙 위치), `LonaBarricade` (2비트 명령 후 로나가 서는 바리케이트 뒤 위치).
   - `LonaStart`는 상단 집무 구역(중앙 도로 북쪽)에 둔다. 총성은 `CS_PROLOGUE_SHELTER` 에셋의 `Gunshot` 칸 하나로 재생하며(거리감은 볼륨으로), 칸이 비어 있으면 소리 없이 진행한다.

