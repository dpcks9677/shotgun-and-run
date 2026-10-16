using System.Collections;
using System.Linq;
using ShotgunAndRun.Core;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// CS_PROLOGUE_SHELTER: 절망 속의 불씨. 대본: Docs/quests/CS_PROLOGUE_SHELTER.md
    /// 쉘터 중앙의 길드장과 경비병들이 바깥의 총성을 듣고 방어 대형을 갖추고, 서쪽 복도에서 뒷걸음질하며 따라붙는 고블린들을 한 발에 한 마리씩 쓰러뜨린 뒤
    /// 게이트로 달려 들어온 플레이어와 경비병이 서로를 발견하며,
    /// 로나와 첫 대화 후 거점 시설을 차례로 소개합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "CS_PROLOGUE_SHELTER", menuName = "ShotgunAndRun/Cutscene/Shelter/Prologue")]
    public sealed class PrologueShelterCutscene : CutsceneAsset
    {
        // 씬에 같은 키의 CutsceneMarker가 있어야 한다
        private const string PlayerStart = "PlayerStart";
        private const string PlayerStop = "PlayerStop";
        private const string GuardPost = "GuardPost";
        private const string GuardAside = "GuardAside";
        private const string GuardCenter1 = "GuardCenter1";
        private const string GuardCenter2 = "GuardCenter2";
        private const string BarricadeGuard1 = "BarricadeGuard1";
        private const string BarricadeGuard2 = "BarricadeGuard2";
        private const string LonaStart = "LonaStart";
        private const string LonaCenter = "LonaCenter";
        private const string LonaBarricade = "LonaBarricade";
        private const string LonaMeet = "LonaMeet";
        private const string LonaHome = "LonaHome";
        private const string CenterCamera = "CenterCamera";
        private const string MeetCamera = "MeetCamera";
        private const string PlayerRest = "PlayerRest";

        // 씬에 같은 키의 CutsceneCrowd가 있어야 한다 (뒷걸음질하는 플레이어를 쫓는 연출용 고블린)
        private const string GoblinsKey = "Goblins";

        // 대사 데이터의 선택지 ID
        private const string AllianceChoice = "ALLIANCE_ACCEPT";
        private const string ElevatorChoice = "ASK_ELEVATOR";
        private const string JoinChoice = "JOIN_ACCEPT";

        [Header("등장인물")]
        [SerializeField] private CharacterProfileSO _guard1;
        [SerializeField] private CharacterProfileSO _guard2;
        [SerializeField] private CharacterProfileSO _guard3;
        [SerializeField] private CharacterProfileSO _lona;

        [Header("대사")]
        [Tooltip("1~2비트: 경비병 1 · 로나 · 경비병 2 · 경비병 3 · 로나. 줄 수는 자유: 앞 세 줄에 총성 · 방향 연출이, 마지막 줄에 방어 대형 이동이 붙는다")]
        [SerializeField] private DialogueDataSO _intro;

        [Tooltip("4비트: 경비병 3이 플레이어를 발견했을 때의 외침")]
        [SerializeField] private DialogueDataSO _guardSpotted;

        [Tooltip("6비트: 로나와의 첫 대화 (선택지 ALLIANCE_ACCEPT · ASK_ELEVATOR)")]
        [SerializeField] private DialogueDataSO _main;

        [Tooltip("ASK_ELEVATOR를 고른 뒤의 로나 설명 (선택지 JOIN_ACCEPT)")]
        [SerializeField] private DialogueDataSO _elevator;

        [Tooltip("합류를 정한 뒤 로나의 한마디")]
        [SerializeField] private DialogueDataSO _rest;

        [Tooltip("7비트: 거점 시설 소개 대사. 줄 수는 자유: 마지막 줄은 마지막 마커(로나 복귀)를, 나머지는 앞 마커를 차례로 비춘다")]
        [SerializeField] private DialogueDataSO _tour;

        [Tooltip("휴식을 마치고 오른쪽 천막 앞에서 깨어난 플레이어의 혼잣말")]
        [SerializeField] private DialogueDataSO _afterRest;

        [Header("거점 시설 소개")]
        [Tooltip("소개 대사의 줄마다 카메라가 비출 마커 키 (마지막은 로나로 복귀)")]
        [SerializeField] private string[] _facilityMarkers = { "Workbench", "QuestBoard", "Storage", MeetCamera };

        [Header("총성 (비워 두면 소리 없이 진행)")]
        [SerializeField] private AudioClip _gunshot;

        [Header("BGM (비워 두면 그 구간은 BGM 없음. 쉘터 BGM은 프롤로그가 끝난 뒤에 시작)")]
        [Tooltip("1~3비트(쉘터에 들어서기 전)의 긴장 곡")]
        [SerializeField] private AudioClip _tensionBgm;

        [Tooltip("4~6비트의 경계 곡. 플레이어가 게이트에 도착하면 긴장 곡에서 갈아탄다")]
        [SerializeField] private AudioClip _waryBgm;

        [Tooltip("6비트의 희망 곡. 로나가 샷건을 돌파구라 말하는 줄에서 경계 곡에서 갈아탄다")]
        [SerializeField] private AudioClip _hopeBgm;

        [Header("연출 수치")]
        [SerializeField, Min(0f)] private float _fadeSeconds = 1f;
        [Tooltip("휴식 중에 페이드 아웃으로 가린 검은 화면을 유지하는 시간 (초)")]
        [SerializeField, Min(0f)] private float _restBlackSeconds = 1f;
        [Tooltip("쏘기 전에 쓰러뜨릴 고블린을 겨누고 있는 시간 (초). 박보다 이만큼 먼저 겨누기 시작해 총성이 박에 떨어진다")]
        [SerializeField, Min(0f)] private float _retreatAimSeconds = 0.1f;
        [Tooltip("박(0.8초)마다 바뀌는 플레이어 질주 속도. 박 번호는 추격 시작이 0이고, 마지막 값은 그 뒤 박에도 쓴다")]
        [SerializeField] private float[] _runSpeeds = { 6.5f, 7.5f, 8.5f, 9f, 9f, 9f, 8f, 7f };
        [Tooltip("박마다 맨 앞 고블린이 플레이어 뒤 이만큼(유닛)까지 붙는다. 작을수록 아슬아슬하다. 마지막 값은 그 뒤 박에도 쓴다")]
        [SerializeField] private float[] _goblinGaps = { 2.8f, 2.6f, 2.2f, 1.8f, 1.4f, 1.9f, 2.4f };
        [Tooltip("고블린이 플레이어를 따라잡는 속도")]
        [SerializeField, Min(0.1f)] private float _goblinCatchUpSpeed = 16f;
        [Tooltip("총을 쏘는 박 번호(오름차순). 박은 0.8초라 샷건 연사 간격과 같다. 목록이 끝난 뒤에도 남은 고블린은 박마다 쏘고, 빠진 번호는 쉬는 박이다")]
        [SerializeField] private int[] _shotBeats = { 1, 2, 3, 4, 5, 6 };
        [SerializeField, Min(0.1f)] private float _guardStepSpeed = 2f;
        [SerializeField, Min(0.1f)] private float _formationSpeed = 4f;
        [SerializeField, Min(0.1f)] private float _lonaWalkSpeed = 3.2f;
        [Tooltip("게이트에 도착한 직후, 아무도 움직이지 않고 음악도 없는 정적의 시간 (초)")]
        [SerializeField, Min(0f)] private float _encounterPauseSeconds = 0.5f;
        [SerializeField, Min(0f)] private float _guardAlertSeconds = 1.2f;
        [Tooltip("로나가 움직이는 동안 카메라가 로나를 따라붙기 시작하는 블렌드 시간")]
        [SerializeField, Min(0f)] private float _lonaCameraBlend = 0.6f;
        [Tooltip("추격이 시작될 때 카메라가 복도의 플레이어에게 옮겨 가는 시간. 짧을수록 끊어 치듯 보인다")]
        [SerializeField, Min(0f)] private float _chaseCameraBlend = 0.3f;
        [Tooltip("게이트에 도착해 카메라가 로나 · 경비병 구도로 옮겨 가는 시간")]
        [SerializeField, Min(0f)] private float _arrivalCameraBlend = 0.35f;
        [Tooltip("추격 중 카메라가 천천히 확대되는 크기 비율 (1이면 확대 없음)")]
        [SerializeField, Range(0.5f, 1f)] private float _chaseZoomScale = 0.92f;
        [SerializeField, Min(0f)] private float _facilityCameraBlend = 0.6f;

        protected override void DeclareRequirements(CutsceneRequirements r)
        {
            r.Actor(_guard1, nameof(_guard1));
            r.Actor(_guard2, nameof(_guard2));
            r.Actor(_guard3, nameof(_guard3));
            r.Actor(_lona, nameof(_lona));
            r.Dialogue(_intro, nameof(_intro));
            r.Dialogue(_guardSpotted, nameof(_guardSpotted));
            r.Dialogue(_main, nameof(_main));
            r.Dialogue(_elevator, nameof(_elevator));
            r.Dialogue(_rest, nameof(_rest));
            r.Dialogue(_tour, nameof(_tour));
            r.Dialogue(_afterRest, nameof(_afterRest));
            r.Marker(PlayerStart, PlayerStop, GuardPost, GuardAside, GuardCenter1, GuardCenter2, BarricadeGuard1, BarricadeGuard2,
                LonaStart, LonaCenter, LonaBarricade, LonaMeet, LonaHome, CenterCamera, MeetCamera, PlayerRest);
            r.Marker(_facilityMarkers);
            r.Crowd(GoblinsKey);

            if (_main != null)
            {
                r.Check(_main.Choices.Any(choice => choice.ChoiceId == AllianceChoice), $"대사 '{_main.name}'에 '{AllianceChoice}' 선택지가 없습니다.");
                r.Check(_main.Choices.Any(choice => choice.ChoiceId == ElevatorChoice), $"대사 '{_main.name}'에 '{ElevatorChoice}' 선택지가 없습니다.");
            }

            if (_elevator != null)
            {
                r.Check(_elevator.Choices.Any(choice => choice.ChoiceId == JoinChoice), $"대사 '{_elevator.name}'에 '{JoinChoice}' 선택지가 없습니다.");
            }
        }

        // _intro의 줄 번호 (0부터): 경비병 1, 로나, 경비병 2. 방어 대형 이동은 줄 수와 관계없이 마지막 줄
        private const int IntroFarShots = 0;
        private const int IntroLonaLooksWest = 1;
        private const int IntroMidShots = 2;

        // _main의 줄 번호 (0부터): 로나가 샷건을 "돌파구"라 부르는 줄
        private const int MainHopeLine = 7;

        // 긴장 곡의 한 박(75 BPM). 샷건의 연사 간격과 같아서 박마다 한 발씩 쏠 수 있다
        private const float BeatSeconds = 0.8f;

        // 샷건의 연사 간격. 총성은 어느 연출에서든 이보다 가깝게 울리지 않는다
        private const float MinShotInterval = BeatSeconds;

        // 연사 간격이 아직 풀리지 않아 쏘지 못했을 때 이 시간 안에서 한 프레임씩 다시 시도한다
        private const float FireRetrySeconds = 0.4f;
        private const float FireRetryStep = 0.01f;

        // 사격 때 카메라 흔들림 (마지막 한 마리는 더 크게)
        private const float ShotShakePower = 0.06f;
        private const float ShotShakeSeconds = 0.15f;
        private const float FinalShakePower = 0.12f;
        private const float FinalShakeSeconds = 0.3f;

        // 게이트에 도착하면 긴장 곡을 이만큼 빠르게 끊어 안도의 무음을 만든다
        private const float TensionCutSeconds = 0.15f;

        // 마지막 총성이 울린 뒤 다음 총성이 허용되는 시각 (Time.time). 대사를 빨리 넘겨 총성 묶음이 겹쳐도 간격을 지키도록 뒤로 미룬다
        private float _nextShotAt;

        protected override IEnumerator Play(CutsceneContext c)
        {
            // 쉘터 BGM은 프롤로그가 끝날 때까지 시작하지 않는다 (SceneBgmPlayer는 전환이 끝난 뒤 꺼져 있으면 재생하지 않는다)
            // 전환 중 이전 씬의 플레이어를 잡지 않도록 활성 씬 것만 고르고, 컷씬이 중간에 멈춰도 finally에서 되돌려 쉘터 BGM이 비지 않게 한다
            Scene scene = SceneManager.GetActiveScene();
            var shelterBgm = FindObjectsByType<SceneBgmPlayer>(FindObjectsSortMode.None)
                .FirstOrDefault(player => player.gameObject.scene == scene);
            if (shelterBgm != null)
            {
                shelterBgm.enabled = false;
            }

            try
            {
                yield return PlayBeats(c, shelterBgm);
            }
            finally
            {
                if (shelterBgm != null)
                {
                    shelterBgm.enabled = true;
                    shelterBgm.Play();
                }
            }
        }

        private IEnumerator PlayBeats(CutsceneContext c, SceneBgmPlayer shelterBgm)
        {
            _nextShotAt = 0f;

            var guard1 = c.Actor(_guard1);
            var guard2 = c.Actor(_guard2);
            var guard3 = c.Actor(_guard3);
            var lona = c.Actor(_lona);
            var goblins = c.Crowd(GoblinsKey);

            // 경비병이 컷씬 전에 서 있던 자리와 방향. 휴식 뒤 각자 자리로 되돌린다
            var guards = new[] { guard1, guard2, guard3 };
            var guardHomes = new Vector3[guards.Length];
            var guardFacesLeft = new bool[guards.Length];
            for (int i = 0; i < guards.Length; i++)
            {
                guardHomes[i] = guards[i].Position;
                guardFacesLeft[i] = c.IsFacingLeft(guards[i]);
            }

            // 배치: 플레이어는 게이트 밖, 경비병 1 · 2는 중앙, 경비병 3은 게이트 보초 자리, 로나는 위쪽 집무 구역. 화면은 검게 덮어 둔다
            c.Teleport(c.Player, PlayerStart);
            c.Teleport(guard1, GuardCenter1);
            c.Teleport(guard2, GuardCenter2);
            c.Teleport(guard3, GuardPost);
            c.Teleport(lona, LonaStart);
            c.Face(guard1, left: true);
            c.Face(guard2, left: true);
            c.Face(guard3, left: true);
            c.CameraTo(CenterCamera, 0f);
            c.FadeOut(0f);

            // 긴장 BGM: 컷씬은 씬 전환 중에 시작되므로 전환이 끝나 화면이 보일 때 재생한다
            if (_tensionBgm != null)
            {
                yield return c.WaitUntil(() => GameManager.Instance == null || !GameManager.Instance.IsSceneTransitioning);
                AudioManager.Instance.PlayBgm(_tensionBgm, true, _fadeSeconds);
            }

            // 1~2비트: 페이드 인, 로나가 중앙으로 내려오는 동안 서쪽에서 총성이 점점 가까워지고 로나가 방어 대형을 명령
            c.FadeIn(_fadeSeconds);
            c.MoveTo(lona, LonaCenter, _lonaWalkSpeed);
            c.CameraTo(lona, _lonaCameraBlend);

            int lastIntroLine = _intro.Lines.Count - 1;
            yield return c.Say(_intro, line =>
            {
                if (line == lastIntroLine)
                {
                    Gunshots(c, count: 3, interval: MinShotInterval, volume: 0.8f);
                    c.MoveTo(guard1, BarricadeGuard1, _formationSpeed);
                    c.MoveTo(guard2, BarricadeGuard2, _formationSpeed);
                    c.MoveTo(lona, LonaBarricade, _lonaWalkSpeed);
                    return;
                }

                switch (line)
                {
                    case IntroFarShots:
                        Gunshots(c, count: 2, interval: 1.4f, volume: 0.25f);
                        break;
                    case IntroLonaLooksWest:
                        c.Face(lona, left: true);
                        break;
                    case IntroMidShots:
                        Gunshots(c, count: 2, interval: 0.9f, volume: 0.5f);
                        break;
                }
            });

            // 3비트: 어둠 속 서쪽 복도에서 고블린이 쫓아온다. 플레이어는 뒤를 겨눈 채 뒷걸음질로 달리며 박(0.8초)마다 한 마리씩 쓰러뜨린다.
            // 박마다 질주 속도가 오르고 고블린 간격이 좁아진다. 전투는 복도 끝에 닿기 전에 끝나고, 남은 거리는 혼자 달린다 (탄은 줄지 않는 연기)
            // 대형 이동은 기다리지 않고 바로 이어 간다. 추격 시작은 첫 사격이 앞 장면 총성 뒤 연사 간격 이상이 되도록 미루고,
            // 긴장 곡의 박 경계에 맞춘다 (겨누기 시간만큼 앞서 시작해 총성이 박에 떨어진다)
            float firstShotIn = _retreatAimSeconds + (_shotBeats.Length > 0 ? _shotBeats[0] : 0) * BeatSeconds;
            float wait = Mathf.Max(0f, _nextShotAt - Time.time - firstShotIn);
            if (_tensionBgm != null)
            {
                float untilBeat = BeatSeconds - (AudioManager.Instance.BgmTime + wait) % BeatSeconds - _retreatAimSeconds;
                wait += untilBeat < 0f ? untilBeat + BeatSeconds : untilBeat;
            }

            yield return c.Wait(wait);
            float chaseStart = Time.time;

            c.SetPlayerArmed(true);
            c.SetPlayerMagazine(int.MaxValue);
            c.Face(c.Player, left: true);
            c.SetVisible(goblins, true);
            c.CameraToPlayer(_chaseCameraBlend);

            int lastShotBeat = _shotBeats.Length > 0 ? _shotBeats[_shotBeats.Length - 1] : 0;
            c.CameraZoomScale(_chaseZoomScale, (lastShotBeat + 1) * BeatSeconds);

            int shotIndex = 0;
            for (int beat = 0; ; beat++)
            {
                bool listed = shotIndex < _shotBeats.Length;
                bool alive = c.CrowdNearest(goblins, c.Player, out Vector3 target);
                if (!listed && !alive) break;

                // 이 박의 질주 속도와 고블린 간격 (다시 지정하면 이전 명령은 취소된다)
                float speed = _runSpeeds[Mathf.Min(beat, _runSpeeds.Length - 1)];
                c.MoveBackward(c.Player, PlayerStop, speed);
                c.CrowdFollow(goblins, c.Player, _goblinGaps[Mathf.Min(beat, _goblinGaps.Length - 1)], speed, _goblinCatchUpSpeed);

                bool shoot = !listed || beat >= _shotBeats[shotIndex];
                if (listed && shoot)
                {
                    shotIndex++;
                }

                if (shoot && alive)
                {
                    // 뒤를 본 채 이동을 끊지 않고 조준해 쏜다
                    c.AimAt(c.Player, target);
                    yield return c.Wait(_retreatAimSeconds);

                    // 연사 간격이 아직 풀리지 않았으면 풀릴 때까지 기다려 쏜다. 쏘지 못했는데 쓰러뜨리면 총 없이 죽는 것처럼 보인다
                    float fireDeadline = Time.time + FireRetrySeconds;
                    while (!c.PlayerFire() && Time.time < fireDeadline)
                    {
                        yield return c.Wait(FireRetryStep);
                    }

                    c.CrowdKill(goblins, c.Player);

                    bool last = shotIndex >= _shotBeats.Length && !c.CrowdNearest(goblins, c.Player, out _);
                    c.CameraShake(last ? FinalShakePower : ShotShakePower, last ? FinalShakeSeconds : ShotShakeSeconds);

                    if (last) break;
                }

                yield return c.Wait(chaseStart + (beat + 1) * BeatSeconds - Time.time);
            }

            // 마지막 한 마리를 쓰러뜨리면 앞으로 돌아 남은 복도를 혼자 달려 게이트에 감속해 들어선다. 긴장 곡은 빠르게 끊고 카메라는 대면 구도로 옮긴다
            c.Face(guard1, left: true);
            c.Face(guard2, left: true);
            c.Face(c.Player, left: false);
            yield return c.MoveTo(c.Player, PlayerStop, _runSpeeds[_runSpeeds.Length - 1]);
            c.CameraZoomReset(_arrivalCameraBlend);
            c.CameraTo(MeetCamera, _arrivalCameraBlend);

            if (_tensionBgm != null)
            {
                AudioManager.Instance.StopBgm(TensionCutSeconds);
            }

            // 4비트: 도착 직후 잠시 정적이 흐른 뒤, 경비병 3과 플레이어가 서로를 발견하고 함께 놀란 뒤 확인 (Shock 말풍선은 대사 데이터에 있음)
            yield return c.Wait(_encounterPauseSeconds);
            var playerAlert = c.Bubble(c.Player, EmotionType.Alert, _guardAlertSeconds);
            yield return c.Bubble(guard3, EmotionType.Alert, _guardAlertSeconds);
            yield return playerAlert;
            yield return c.Say(_guardSpotted, line =>
            {
                // 경계 곡은 긴장이 끊긴 뒤의 무음을 지나 경비병의 첫 외침에서 시작한다
                if (line == 0 && _waryBgm != null)
                {
                    AudioManager.Instance.PlayBgm(_waryBgm, true, _fadeSeconds);
                }
            });

            // 5비트: 경비병 3이 물러서고 로나가 바리케이트 뒤에서 걸어 나와 플레이어와 마주 선다
            var guardAside = c.MoveTo(guard3, GuardAside, _guardStepSpeed);
            var lonaMeet = c.MoveTo(lona, LonaMeet, _lonaWalkSpeed);
            c.CameraTo(lona, _lonaCameraBlend);
            yield return c.WaitAll(guardAside, lonaMeet);
            c.CameraTo(MeetCamera, _lonaCameraBlend);
            c.FaceEachOther(c.Player, lona);

            // 6비트: 대화와 동맹 제안. 승강기를 물었다면 설명을 듣고 한 번 더 합류 여부를 고른다 (대사별 말풍선은 대사 데이터에 있음)
            var talk = c.Say(_main, line =>
            {
                if (line == MainHopeLine && _hopeBgm != null)
                {
                    AudioManager.Instance.PlayBgm(_hopeBgm, true, _fadeSeconds);
                }
            });
            yield return talk;

            if (talk.ChoiceId == ElevatorChoice)
            {
                yield return c.Say(_elevator);
            }

            yield return c.Say(_rest);

            // 7비트: 거점 시설 소개. 대사 줄마다 카메라가 시설을 비추고, 마지막 줄에서 로나에게 돌아온다
            int lastTourLine = _tour.Lines.Count - 1;
            int lastMarker = _facilityMarkers.Length - 1;
            yield return c.Say(_tour, line => c.CameraTo(_facilityMarkers[line == lastTourLine ? lastMarker : Mathf.Min(line, lastMarker - 1)], _facilityCameraBlend));

            // 마무리(휴식): 페이드 아웃으로 화면을 가린 동안 로나와 경비병들을 각자 자리로 돌려보내고, 플레이어를 쉘터 맨손 상태로 오른쪽 천막 앞에 옮긴다.
            // 검은 화면을 잠시 유지한 뒤 페이드 인하고, 쉘터 BGM을 시작하며 플레이어가 한숨 돌리는 혼잣말을 한다
            yield return c.FadeOut(_fadeSeconds);
            c.Teleport(lona, LonaHome);
            for (int i = 0; i < guards.Length; i++)
            {
                c.TeleportToPosition(guards[i], guardHomes[i]);
                c.Face(guards[i], guardFacesLeft[i]);
            }

            c.SetVisible(goblins, false);
            c.SetPlayerArmed(false);
            c.Teleport(c.Player, PlayerRest);
            c.Face(c.Player, left: true);
            yield return c.CameraToPlayer(0f);
            yield return c.Wait(_restBlackSeconds);

            // 프롤로그가 끝나면 쉘터 BGM을 시작한다
            if (shelterBgm != null)
            {
                shelterBgm.enabled = true;
                shelterBgm.Play();
            }

            yield return c.FadeIn(_fadeSeconds);
            yield return c.Say(_afterRest);
        }

        private void Gunshots(CutsceneContext c, int count, float interval, float volume)
        {
            if (_gunshot != null)
            {
                interval = Mathf.Max(interval, MinShotInterval);
                float start = Mathf.Max(0f, _nextShotAt - Time.time);
                c.PlaySfxBurst(_gunshot, count, interval, volume, start);
                _nextShotAt = Time.time + start + (count - 1) * interval + MinShotInterval;
            }
        }
    }
}
