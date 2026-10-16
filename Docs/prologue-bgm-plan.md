# 프롤로그 쉘터 컷씬 BGM 제작 계획서 (긴장 → 경계 → 희망)

> **작성 일자:** 2026-10-14  
> **상태:** 시안 적용(청취 확인 대기). 프롤로그 컷씬 에셋에 연결됨  
> **범위:** `CS_PROLOGUE_SHELTER`(쉘터 프롤로그 컷씬)의 BGM 3곡과 재생 규칙  
> **관련 문서:** [shelter-bgm-plan.md](shelter-bgm-plan.md) / [quests/CS_PROLOGUE_SHELTER.md](quests/CS_PROLOGUE_SHELTER.md) / 제작 도구 `Docs/tools/audiogen/prologue_tension.py` · `prologue_calm.py`

---

## 1. 목표

- 프롤로그에서는 **쉘터 BGM을 틀지 않는다.** 대신 장면의 감정선에 맞춘 곡 3개를 비트에 따라 바꾼다.
- 긴장 곡이 끊긴 직후의 안도와 탈진, 경계, 희미한 희망, 온기로 이어지는 감정선을 한 조성(D)과 한 템포(75 BPM)로 묶어 곡이 바뀌어도 이질감이 없게 한다.
- 마지막에 쉘터 BGM(D 도리안, 72 BPM)으로 자연스럽게 인계한다.

**제외:** 쉘터 BGM 변경, 던전 BGM 변경, 대사 길이에 맞춘 시간표식 재생(대사는 플레이어가 넘기므로 루프 + 줄 번호 트리거만 쓴다).

---

## 2. 재생 흐름

| 구간 | 곡 | 전환 시점 |
|---|---|---|
| 1~3비트 (총성 · 고블린 추격 · 게이트로 달려감) | **긴장** | 씬 전환이 끝나 화면이 보일 때 페이드 인 |
| 4~6비트 전반 (경비병 발견 · 로나 등장 · 상황 설명) | **경계** | 플레이어가 게이트(`PlayerStop`)에 도착할 때 갈아탐 |
| 6비트 후반 ~ 7비트 (돌파구 · 동맹 제안 · 시설 소개) | **희망** | `_main` 대사 7번째 줄(로나 "…돌파구가 될지도 모르겠어")에서 갈아탐 |
| 컷씬 종료 | 쉘터 BGM | 마지막 페이드 인과 함께 `SceneBgmPlayer.Play()` |

프롤로그 동안 `SceneBgmPlayer`는 컷씬이 꺼 두고, 끝날 때 다시 켜서 시작한다. 곡 사이 전환은 `AudioManager.PlayBgm`의 크로스페이드(이전 곡 페이드 아웃과 새 곡 페이드 인을 동시에, 컷씬의 `_fadeSeconds` 1초)를 쓴다.

---

## 3. 곡 설계

공통: 75 BPM(던전 BGM과 같음), 4/4, D 중심, RMS 기준은 아래 표.

| | 긴장 | 경계 | 희망 |
|---|---|---|---|
| 음계 | D 프리지안 | D 프리지안(E♭ 부딪힘) | D 도리안(E♭ → E 해소) |
| 길이 | 8마디, 1,128,960샘플(25.6초) | 8마디, 1,128,960샘플 | 8마디, 1,128,960샘플 |
| 음량 | RMS -20 dBFS | RMS -23 dBFS | RMS -23 dBFS |
| 에셋 | `PrologueTensionBGM.ogg` | `PrologueWaryBGM.ogg` | `PrologueHopeBGM.ogg` |
| 스크립트 | `prologue_tension.py` | `prologue_calm.py` | `prologue_calm.py` |

### 3.1 긴장

- 킥 · 스네어가 16분음표로 엇갈리는 더블타임의 더블 드럼(300 BPM 느낌) + 32분 하이햇. 필인 없이 처음부터 끝까지 같은 밀도.
- 8분 펄스 베이스(D2, 4마디 순환의 마지막 마디는 E♭2) + D · E♭ · A 클러스터 패드(8분 박동으로 떨림).

### 3.2 경계

- 낮은 현 드론(D2 · A2 · D3) + D3 · E♭3 단2도 패드로 불안한 부딪힘을 남긴다.
- 심장박동(lub-dub)이 박마다 한 번. 긴장 곡의 펄스를 사람 심장 속도로 풀어낸 것.
- 첼로 몇 음(D · E♭ · A · C). 멜로디라기보다 숨소리처럼 얹는다.
- 대사 아래에 깔리도록 다른 BGM보다 3 dB 낮다.

### 3.3 희망

- 같은 드론 위에서 패드가 D · E · A(sus2)로 열린다. **E♭이 E로 풀리는 한 음 이동**이 "공포가 희망으로 풀림"이다.
- 심장박동이 두 박에 한 번으로 느려진다.
- 첼로가 쉘터 모티프 **A – F – G – B**를 낮은 음역에서 인용하고, 6마디째에 뮤직박스가 같은 모티프를 한 옥타브 위로 들려준다. 쉘터 BGM으로 넘어갈 때 "같은 곳으로 돌아왔다"는 연결감을 만든다.
- 하프 · 플루트는 쓰지 않는다. 쉘터 BGM이 시작될 때 처음 들어오는 음색으로 남겨 둔다.

---

## 4. 이음새 처리

- 드론 · 패드는 루프 길이에 정수 주기가 들어가는 주파수(`loop_freq`)를 쓰고, 리버브 꼬리와 마지막 음의 여운은 루프 시작으로 감아 붙인다.
- `check.py`의 이음새 검사(점프 · 앞뒤 50ms RMS 비율)를 통과한다. OGG 변환 후에도 샘플 수가 1,128,960으로 같다.
- 큰 배열을 한 번에 OGG로 쓰면 인코더가 조용히 죽으므로 `sf.SoundFile`로 1초씩 나눠 쓴다.

---

## 5. 적용

| 단계 | 내용 |
|---|---|
| 에셋 | `Assets/06.Sounds/Shelter/PrologueTensionBGM.ogg` · `PrologueWaryBGM.ogg` · `PrologueHopeBGM.ogg` (임포트: Compressed In Memory, Vorbis, Preload) |
| 컷씬 | `PrologueShelterCutscene`의 `_tensionBgm` · `_waryBgm` · `_hopeBgm`. 비워 두면 그 구간은 BGM이 없다 |
| 씬 BGM | `SceneBgmPlayer`에 `Play()`를 공개하고, 전환이 끝났을 때 컴포넌트가 꺼져 있으면 재생하지 않게 했다 |
| AudioManager | BGM 전환을 크로스페이드로 변경: 이전 곡을 `_bgmOutgoingSource`로 넘겨 새 곡과 겹쳐 페이드한다(전환 사이 무음 제거). 타이틀 → 쉘터 전환도 같은 방식 |
| 대사 연동 | `MainHopeLine = 7`: `_main` 대사 데이터에서 "돌파구" 줄 번호. 대사 줄이 바뀌면 이 값을 맞춘다 |

## 6. 재현

```
cd Docs/tools/audiogen
.venv/Scripts/python.exe prologue_tension.py   # Docs/drafts/audio/prologue_tension_loop.wav
.venv/Scripts/python.exe prologue_calm.py      # prologue_wary_loop.wav · prologue_hope_loop.wav
```

같은 코드는 항상 같은 WAV를 만든다(이름 기반 시드). OGG 변환은 위 4장 방식으로 한다.

## 7. 남은 일

- 플레이 모드에서 전환 시점(게이트 도착, 돌파구 줄) 청취 확인. 음량(총성 · 대사와의 균형)과 곡 사이 1초 크로스페이드의 어색함 여부. 타이틀 → 쉘터 전환(AudioManager 크로스페이드 영향) 확인.
- `_main` 대사 줄이 바뀌면 `MainHopeLine` 값 확인.
- 컷씬을 도중에 중단(Abort)하면 쉘터 BGM이 시작되지 않는다.
