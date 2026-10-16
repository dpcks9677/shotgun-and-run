# audiogen

던전 BGM(탐험 앰비언트 + 전투 드럼 · 신스 + 습격 스템 + 원샷 SFX)과 타이틀 · 쉘터 BGM을 파이썬으로 절차 합성하는 도구. 계획은 `Docs/dungeon-bgm-plan.md` · `Docs/title-bgm-plan.md` · `Docs/shelter-bgm-plan.md`.

## 환경

```
cd Docs/tools/audiogen
python -m venv .venv
.venv/Scripts/pip install numpy scipy soundfile
```

`.venv/`는 gitignore. 항상 `.venv/Scripts/python.exe`로 실행한다.

## 스크립트

| 파일 | 만드는 것 / 용도 |
|---|---|
| `ambient.py` | 탐험 앰비언트 루프 `ambient_loop.wav` (드론 · 바람 · 패드 · 심장박동 맥박) |
| `drums.py` | 전투 드럼 루프 `drums_loop.wav`. 킥 · 스네어 · 탐 함수는 `raid.py`가 가져다 쓴다 |
| `synth.py` | 다크 신스 리프 + 서브 `synth_loop.wav` |
| `raid.py` | 습격 스템 `raid_loop.wav` (브라스 + 서스테인 파워코드 + 디스토션 베이스 처킹 + 더블타임 드럼) |
| `title.py` | 타이틀 BGM 루프 `title_loop.wav` (튜바 드론 · 트롬본 코랄 · 호른 콜, D 프리지안, 54 BPM 16마디). `dsp.py`의 루프 길이 상수 대신 자체 `N`을 쓰므로 규격이 다르다 |
| `shelter.py` | 쉘터 BGM 루프 `shelter_loop.wav` (하프 · 플루트 · 뮤직박스 · 현 드론, D 도리안, 72 BPM 16마디). 규격은 `title.py`와 같은 방식. 난수 시드 이름은 `title/...`로 남아 있다(바꾸면 소리가 달라진다) |
| `oneshots.py` | 원샷 SFX 12개 `oneshots/sfx_*.wav` (물방울 · 금속 · 바람 등) |
| `anvil_hit.py` | 쉘터 모루 타격음 3종 `anvil_hit_01~03.wav`(시안 `Docs/drafts/audio/anvil/`). 실제 모루 타격 녹음을 분석한 값(울림 2385 · 3220Hz, -30dB까지 0.27초)에 맞춰 합성. 한 강화에 3번 내려치므로 변형 3개를 순서대로 쓴다 |
| `elena_teleport.py` | 엘레나 순간이동 컷씬의 밧줄 풀림 `rope_release.wav`. 순간이동음은 플레이어와 같은 `teleport_out.wav`를 쓴다 |
| `ambush_alarm.py` | 엘레나 매복 경보음 `ambush_alarm.wav`. 습격 뿔나팔과 겹치지 않게 녹슨 경종 4타 + 첫 타 바닥 붐 |
| `tutorial_alert.py` | 튜토리얼 하늘색 · 보라색 방 컷씬 끝의 몬스터 포효 `monster_alert.wav`(두 컷씬 공용) |
| `faint_collapse.py` | 튜토리얼 기절 컷씬의 쓰러짐 `faint_collapse.wav`(털썩 + 이명 + 먹먹한 심장박동, 사망음 없음. 승리 · 패배 공용) |
| `gate_close.py` | 퀘스트 디펜스 시작 때 묵직한 쇠창살이 떨어져 닫히는 `gate_close.wav`(쇠사슬 덜컹 → 석재 문턱에 쿵 → 눌려 한 번 더 쿵 → 낮게 울림 → 빗장 철컥 → 되울림, 쿵 피크 0.16초, 2.0초, RMS -10 dBFS). 쇠 배음은 레퍼런스 금속 타격 녹음(freesound metal-hit-echo)의 스펙트럼 분석값을 0.6배로 낮추고 44 · 55Hz 저역 질량을 더함. `Assets/06.Sounds/Environment/` |
| `prologue_tension.py` | 프롤로그 쉘터 진입 전 긴장 BGM 루프 `prologue_tension_loop.wav`(D 프리지안, 75 BPM, 8마디. 킥 · 스네어가 16분음표로 엇갈리는 더블타임의 더블 드럼(300 BPM 느낌, 32분 하이햇, 필인 없음) + 8분 펄스 베이스 + D · E♭ · A 클러스터 패드). `raid.py`의 하이햇과 `drums.py`의 킥 · 스네어 · 탐을 가져다 쓴다. 최종 에셋은 `Assets/06.Sounds/Shelter/PrologueTensionBGM.ogg` |
| `prologue_calm.py` | 프롤로그 4~7비트용 루프 2개: 경계 `prologue_wary_loop.wav`(현 드론 + 심장박동 + 첼로, E♭ 부딪힘)와 희망 `prologue_hope_loop.wav`(E♭ → E 해소 + 쉘터 모티프 인용). 75 BPM 8마디, RMS -23 dBFS. `shelter.py`의 `music_box` · `hz`와 `drums.py`의 킥을 가져다 쓴다. 최종 에셋은 `Assets/06.Sounds/Shelter/PrologueWaryBGM.ogg` · `PrologueHopeBGM.ogg` |
| `preview_mix.py` | 스템을 이어 듣는 확인용 `preview_mix.wav` (최종 에셋 아님) |
| `check.py` | 루프 · 원샷 WAV 검증. 생성 스크립트가 끝에 자동 호출 |
| `dsp.py` | 공통 모듈: 상수, 시드, 오실레이터, 필터, 엔벨로프, 리버브 · 딜레이, 믹스, WAV 저장 |

## 실행 순서

```
.venv/Scripts/python.exe ambient.py
.venv/Scripts/python.exe drums.py
.venv/Scripts/python.exe synth.py
.venv/Scripts/python.exe raid.py
.venv/Scripts/python.exe oneshots.py
.venv/Scripts/python.exe preview_mix.py   # 앞 출력이 모두 있어야 한다
```

## 규격

- 던전: 75 BPM, 32마디, 루프 4,515,840샘플(44.1kHz, 102.4초), D 프리지안, 16bit PCM WAV
- 타이틀: 54 BPM, 16마디, 루프 3,136,000샘플(71.11초), D 프리지안, RMS -20 dBFS. 최종 에셋은 OGG(Vorbis)
- 쉘터: 72 BPM, 16마디, 루프 2,352,000샘플(53.33초), D 도리안, RMS -20 dBFS. 최종 에셋은 OGG(Vorbis)
- 프롤로그 긴장: 75 BPM, 8마디, 루프 1,128,960샘플(25.6초), D 프리지안, RMS -20 dBFS.
- 프롤로그 경계 · 희망: 75 BPM, 8마디, 루프 1,128,960샘플(25.6초), D 프리지안 · D 도리안, RMS -23 dBFS (대사 아래로 깔리게 낮춤). 최종 에셋은 OGG(Vorbis). 큰 배열을 한 번에 OGG로 쓰면 인코더가 조용히 죽으므로 `sf.SoundFile`로 1초씩 나눠 쓴다
- 스템 레벨(RMS): ambient -26 dBFS + 맥박, drums -23, synth -23, raid -21

## check.py

```
.venv/Scripts/python.exe check.py --selftest          # 검증기 자체 테스트
.venv/Scripts/python.exe check.py --stem 파일.wav     # 리듬 스템(seam_rms는 참고값)
.venv/Scripts/python.exe check.py --oneshot 파일.wav  # 원샷
.venv/Scripts/python.exe check.py 파일.wav            # 일반 루프
```

## 출력 위치

시안은 `Docs/drafts/audio/`(gitignore)에 나오고, 승인된 최종 에셋은 `Assets/Resources/Audio/Dungeon/`(타이틀 · 쉘터는 `Assets/06.Sounds/Title/` · `Assets/06.Sounds/Shelter/`, 컷씬 SFX는 `Assets/06.Sounds/Cutscene/`)에 둔다.

## 재현성과 주의

난수는 이름 기반 시드(`dsp.rng("raid/bass")` 등)라 같은 코드는 항상 바이트 단위로 같은 WAV를 만든다. 시드 이름이나 난수를 쓰는 순서를 바꾸면 소리가 달라지므로, 스크립트를 고친 뒤에는 출력 WAV의 sha256을 수정 전과 비교해 의도한 변화인지 확인한다.
