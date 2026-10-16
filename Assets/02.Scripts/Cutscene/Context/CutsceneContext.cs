using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShotgunAndRun.Core;
using ShotgunAndRun.NPC;
using ShotgunAndRun.Player;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;
using PlayerComponent = ShotgunAndRun.Player.Player;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 컷씬 한 번의 재생 상태이자, 연출 코드가 쓰는 명령 모음입니다.
    /// 명령은 기능별 partial 파일(Flow · Actor · Camera · Dialogue · Screen · Audio · Game)에 나뉘어 있습니다.
    ///
    /// 명령 호출 규칙: 명령은 호출하는 순간 시작하고 핸들을 돌려줍니다.
    /// yield return 핸들 → 그 명령이 끝날 때까지 기다림 / yield return 하지 않음 → 기다리지 않고 다음 줄로 (동시 진행)
    /// </summary>
    public sealed partial class CutsceneContext
    {
        private sealed class Task
        {
            public CutsceneRoutine Routine;
            public CutsceneHandle Handle;
            public string Owner;
            public bool Cancelled;
        }

        private readonly CutsceneDirector _director;
        private readonly CutsceneRequirements _requirements;
        private readonly Dictionary<CharacterProfileSO, CutsceneActor> _actors = new Dictionary<CharacterProfileSO, CutsceneActor>();
        private readonly Dictionary<string, CutsceneMarker> _markers = new Dictionary<string, CutsceneMarker>();
        private readonly Dictionary<string, List<CutsceneSignal>> _signals = new Dictionary<string, List<CutsceneSignal>>();
        private readonly List<Task> _tasks = new List<Task>();
        private readonly List<NpcInteractable> _disabledInteractables = new List<NpcInteractable>();

        private IDialogueService _dialogue;
        private CutsceneCamera _camera;
        private CutsceneRoutine _main;
        private PlayerInputReader _lockedInput;
        private RunSession _pausedRun;
        private CutsceneEnemyFreeze _enemyFreeze;
        private float _bgmVolume;
        private bool _weaponHiddenByCutscene;
        private readonly List<Behaviour> _hiddenPlayerHud = new List<Behaviour>();

        /// <summary>
        /// 재생 중인 컷씬 에셋
        /// </summary>
        public CutsceneAsset Asset
        {
            get;
        }

        /// <summary>
        /// 플레이어 인물
        /// </summary>
        public CutsceneActor Player
        {
            get; private set;
        }

        internal Camera Camera => _camera.Camera;
        internal bool IsMainFinished => _main != null && _main.IsFinished;

        private CutsceneContext(CutsceneDirector director, CutsceneAsset asset, CutsceneRequirements requirements)
        {
            _director = director;
            Asset = asset;
            _requirements = requirements;
        }

        // ───────────────────────────── 사전 검사 ─────────────────────────────

        /// <summary>
        /// 컷씬이 필요로 하는 것을 씬에서 모두 찾습니다. 하나라도 없으면 null과 함께 빠진 항목을 모두 돌려줍니다.
        /// </summary>
        internal static CutsceneContext TryCreate(CutsceneDirector director, CutsceneAsset asset, out List<string> errors)
        {
            var requirements = asset.CollectRequirements();
            var context = new CutsceneContext(director, asset, requirements);
            errors = new List<string>(requirements.Errors);
            director.ValidateParts(errors);

            context.ResolveServices(errors);
            context.ResolveActors(errors);
            context.ResolveMarkers(errors);
            context.ResolveSignals(errors);
            context.ResolveEnemySpawner(errors);
            context.ResolveCrowds(errors);

            return errors.Count == 0 ? context : null;
        }

        private void ResolveServices(List<string> errors)
        {
            _dialogue = DialogueManager.Instance;
            if (_dialogue == null && _requirements.Dialogues.Count > 0)
            {
                errors.Add("씬에 DialogueManager가 없습니다.");
            }

            var follow = CutsceneCamera.FindMainFollow();
            if (follow == null)
            {
                errors.Add("씬에 PlayerCameraFollow가 붙은 카메라가 없습니다.");
            }
            else
            {
                _camera = new CutsceneCamera(follow);
                if (_camera.Camera == null)
                {
                    errors.Add($"'{follow.name}' 아래에 Camera 컴포넌트가 없습니다.");
                }
            }
        }

        private void ResolveActors(List<string> errors)
        {
            var player = PlayerComponent.PlayerInstance;
            if (player == null)
            {
                errors.Add("씬에 플레이어가 없습니다.");
            }
            else
            {
                var playerActor = PlayerCutsceneActor.GetOrAdd(player);
                playerActor.AssignProfile(_director.PlayerProfile);
                playerActor.Validate(errors);
                Player = playerActor;
                if (_director.PlayerProfile != null)
                {
                    _actors[_director.PlayerProfile] = playerActor;
                }
            }

            foreach (var actor in UnityEngine.Object.FindObjectsByType<CutsceneActor>(FindObjectsSortMode.None))
            {
                if (actor == Player || actor.Profile == null) continue;

                if (_actors.TryGetValue(actor.Profile, out var existing))
                {
                    if (_requirements.Actors.Contains(actor.Profile))
                    {
                        errors.Add($"'{actor.Profile.name}' 인물이 씬에 둘 이상 있습니다: '{existing.name}', '{actor.name}'");
                    }
                    continue;
                }

                _actors[actor.Profile] = actor;
            }

            foreach (var profile in _requirements.Actors)
            {
                if (!_actors.TryGetValue(profile, out var actor))
                {
                    errors.Add($"씬에 '{profile.name}' 인물(CutsceneActor)이 없습니다.");
                }
                else if (actor != Player)
                {
                    actor.Validate(errors);
                }
            }
        }

        private void ResolveMarkers(List<string> errors)
        {
            var all = UnityEngine.Object.FindObjectsByType<CutsceneMarker>(FindObjectsSortMode.None);
            foreach (string key in _requirements.Markers)
            {
                // 이 컷씬 전용 마커를 먼저 찾고, 없으면 공용 마커를 쓴다
                var own = all.Where(m => m.Cutscene == Asset && m.Key == key).ToArray();
                var candidates = own.Length > 0 ? own : all.Where(m => m.Cutscene == null && m.Key == key).ToArray();

                if (candidates.Length == 0)
                {
                    errors.Add($"씬에 '{Asset.Id}' 컷씬의 '{key}' 마커(CutsceneMarker)가 없습니다.");
                }
                else if (candidates.Length > 1)
                {
                    errors.Add($"'{key}' 마커가 씬에 {candidates.Length}개 있습니다: {string.Join(", ", candidates.Select(m => m.name))}");
                }
                else
                {
                    _markers[key] = candidates[0];
                }
            }
        }

        private void ResolveSignals(List<string> errors)
        {
            var all = UnityEngine.Object.FindObjectsByType<CutsceneSignal>(FindObjectsSortMode.None);
            foreach (string key in _requirements.Signals)
            {
                var receivers = all.Where(s => s.Key == key).ToList();
                if (receivers.Count == 0)
                {
                    errors.Add($"씬에 '{key}' 신호(CutsceneSignal)가 없습니다.");
                }
                else
                {
                    _signals[key] = receivers;
                }
            }
        }

        // ───────────────────────────── 시작 · 진행 · 정리 ─────────────────────────────

        internal void Begin()
        {
            _bgmVolume = _director.BgmSource.volume;

            if (Asset.LockPlayer)
            {
                _lockedInput = PlayerComponent.PlayerInstance.GetComponent<PlayerInputReader>();
                _lockedInput?.SetGameplayEnabled(false);
            }

            // 플레이어에 붙은 산탄 범위 · 장전 막대 표시도 HUD라서 함께 숨긴다. 무기를 든 채 쏘고 장전하는 연출에서도 보이지 않는다
            if (Asset.HideHud)
            {
                var player = PlayerComponent.PlayerInstance;
                HidePlayerHud(player.GetComponentInChildren<SpreadArcIndicator>(true));
                HidePlayerHud(player.GetComponentInChildren<PlayerReloadSliderUI>(true));
            }

            // NPC 상호작용(E)은 플레이어 입력과 별개로 키보드를 직접 읽으므로 컷씬 동안 꺼 둔다
            foreach (var interactable in UnityEngine.Object.FindObjectsByType<NpcInteractable>(FindObjectsSortMode.None))
            {
                if (!interactable.enabled) continue;
                interactable.enabled = false;
                _disabledInteractables.Add(interactable);
            }

            if (Asset.PauseGameplay)
            {
                var run = GameManager.Instance != null ? GameManager.Instance.CurrentRun : null;
                if (run != null && !run.IsPaused)
                {
                    run.SetPaused(true);
                    _pausedRun = run;
                }

                // 런 시간만 멈추면 적은 계속 움직이므로 적도 함께 멈춘다
                _enemyFreeze = new CutsceneEnemyFreeze();
                _enemyFreeze.Begin();
            }

            _camera.Begin();
            foreach (var actor in _actors.Values.Distinct())
            {
                actor.BeginCutscene(this);
            }

            if (_dialogue != null)
            {
                _dialogue.LineStarted += HandleLineStarted;
            }

            _main = new CutsceneRoutine(Asset.CreateSequence(this));
            _main.Tick();
            _enemyFreeze?.Sweep();
        }

        /// <summary>
        /// 한 프레임 진행합니다. 연출 코드의 예외는 호출자(Director)에게 전달됩니다.
        /// </summary>
        internal void Tick()
        {
            int count = _tasks.Count;
            for (int i = 0; i < count; i++)
            {
                var task = _tasks[i];
                if (task.Cancelled) continue;

                task.Routine.Tick();
                if (task.Routine.IsFinished)
                {
                    task.Handle.Complete();
                }
            }
            _tasks.RemoveAll(t => t.Cancelled || t.Routine.IsFinished);

            if (!_main.IsFinished)
            {
                _main.Tick();
            }

            // 이번 프레임에 소환된 적도 바로 멈춘다
            _enemyFreeze?.Sweep();
        }

        internal void LateTick()
        {
            _camera.LateTick(Time.deltaTime);
            UpdateWhiteOuts();
        }

        /// <summary>
        /// 컷씬이 끝났을 때 카메라를 플레이어로 돌립니다. 이미 플레이어를 비추고 있으면 바로 끝납니다.
        /// </summary>
        internal CutsceneHandle ReturnCameraToPlayer(float duration)
        {
            if (_camera.IsSettledOn(Player)) return CutsceneHandle.Done();
            return CameraTo(Player, duration);
        }

        /// <summary>
        /// 정상 종료 · 중단 · 예외 어느 경우든 호출되어 컷씬이 바꾼 상태를 되돌립니다.
        /// </summary>
        internal void Cleanup()
        {
            // 다른 정리 단계가 예외를 내도 적이 멈춘 채 남지 않도록 먼저 푼다
            _enemyFreeze?.End();
            _enemyFreeze = null;

            ClearWhiteOuts();

            _main?.Dispose();
            foreach (var task in _tasks)
            {
                task.Routine.Dispose();
                task.Handle.Complete();
            }
            _tasks.Clear();

            if (_dialogue != null)
            {
                _dialogue.LineStarted -= HandleLineStarted;
                if (_activeDialogue != null && _dialogue.IsDialogueActive)
                {
                    _dialogue.CloseDialogue();
                }
            }
            _activeDialogue = null;

            _director.Bubbles.HideAll();
            _director.Screen.ResetAll();
            _director.BgmSource.Stop();
            _director.BgmSource.volume = _bgmVolume;

            foreach (var actor in _actors.Values.Distinct())
            {
                if (actor != null) actor.EndCutscene();
            }
            _camera?.End();

            foreach (var interactable in _disabledInteractables)
            {
                if (interactable != null) interactable.enabled = true;
            }
            _disabledInteractables.Clear();

            // 죽은 채 끝난 컷씬(사망 연출)은 사망 때 막힌 무기 · 입력을 되살리지 않는다
            var player = PlayerComponent.PlayerInstance;
            bool isPlayerDead = player != null && player.Health != null && player.Health.IsDead;

            // 컷씬이 숨긴 무기는 중단 · 예외로 끝나도 되돌려 둔다
            if (_weaponHiddenByCutscene)
            {
                if (!isPlayerDead) CutscenePlayerState.SetWeaponHidden(false);
                _weaponHiddenByCutscene = false;
            }

            foreach (var hud in _hiddenPlayerHud)
            {
                if (hud != null) hud.enabled = true;
            }
            _hiddenPlayerHud.Clear();

            _pausedRun?.SetPaused(false);
            _pausedRun = null;

            if (_lockedInput != null)
            {
                if (!isPlayerDead) _lockedInput.SetGameplayEnabled(true);
                _lockedInput = null;
            }
        }

        // ───────────────────────────── 내부 도우미 ─────────────────────────────

        /// <summary>
        /// 명령 본문을 백그라운드 작업으로 시작합니다. 호출 즉시 첫 대기 지점까지 실행됩니다.
        /// owner가 같은 작업이 이미 있으면 먼저 취소합니다. (예: 같은 인물에게 새 이동 명령)
        /// </summary>
        private CutsceneHandle Run(IEnumerator body, string owner = null, CutsceneHandle handle = null)
        {
            handle ??= new CutsceneHandle();
            if (owner != null) Cancel(owner);

            var task = new Task { Routine = new CutsceneRoutine(body), Handle = handle, Owner = owner };
            task.Routine.Tick();
            if (task.Routine.IsFinished)
            {
                handle.Complete();
            }
            else
            {
                _tasks.Add(task);
            }

            return handle;
        }

        private void HidePlayerHud(Behaviour hud)
        {
            if (hud == null || !hud.enabled) return;

            hud.enabled = false;
            _hiddenPlayerHud.Add(hud);
        }

        private void Cancel(string owner)
        {
            foreach (var task in _tasks)
            {
                if (task.Cancelled || task.Owner != owner) continue;
                task.Cancelled = true;
                task.Routine.Dispose();
                task.Handle.Complete();
            }
        }

        private Vector3 MarkerPosition(string key)
        {
            if (!_markers.TryGetValue(key, out var marker))
            {
                throw new InvalidOperationException($"'{key}' 마커는 DeclareRequirements에 선언되지 않았습니다.");
            }

            return marker.Position;
        }

        private void RequireActor(CutsceneActor actor)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor), "인물이 null입니다. c.Actor(프로필)로 얻은 인물을 넘기세요.");
            }
        }
    }
}
