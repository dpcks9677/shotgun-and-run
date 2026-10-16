using System;
using ShotgunAndRun.Core;
using ShotgunAndRun.NPC;
using ShotgunAndRun.Player;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;
using PlayerComponent = ShotgunAndRun.Player.Player;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 쉘터의 던전 포탈. NpcInteractable의 Dialogue 칸에 연결하면 E로 대화를 열고,
    /// 입장 선택지를 고르면 입장 컷씬을 재생한 뒤 런을 시작합니다. 다른 선택지는 대화만 닫습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonPortal : MonoBehaviour, INpcDialogue
    {
        [Tooltip("포탈 앞에서 보여줄 대화 (선택지에 ID가 있어야 함)")]
        [SerializeField] private DialogueDataSO _dialogue;

        [Tooltip("이 ID의 선택지를 고르면 던전에 입장합니다")]
        [SerializeField] private string _enterChoiceId = "ENTER";

        [Tooltip("입장 선택 후 재생할 컷씬 (CS_TELEPORT_ENTER). 끝나면 런을 시작합니다")]
        [SerializeField] private CutsceneAsset _entryCutscene;

        private NpcInteractable _speaker;
        private PlayerInputReader _lockedInput;
        private bool _isDialoguePlaying;
        private bool _isEntering;
        private int _endedFrame = -1;

        // 대화를 닫은 E 입력이 같은 프레임에 NpcInteractable로 다시 들어가 대화가 재시작되지 않도록 종료 프레임까지 재생 중으로 취급
        public bool IsPlaying => _isDialoguePlaying || _isEntering || Time.frameCount == _endedFrame;

        private void OnDisable()
        {
            CutsceneDirector.CutsceneFinished -= HandleCutsceneFinished;
            UnlockPlayer();
        }

        public void Play(NpcInteractable speaker, Action onFinished)
        {
            if (IsPlaying || CutsceneDirector.IsPlaying)
                return;

            if (GameManager.Instance == null)
            {
                Debug.LogWarning($"[DungeonPortal] '{name}': GameManager가 없어 던전에 입장할 수 없습니다.", this);
                return;
            }

            var service = DialogueManager.Instance;
            if (_dialogue == null || service == null)
            {
                Debug.LogError($"[DungeonPortal] '{name}': 대화 데이터가 비어 있거나 DialogueManager를 찾을 수 없습니다.", this);
                return;
            }

            if (service.IsDialogueActive)
                return;

            _speaker = speaker;
            _isDialoguePlaying = true;
            LockPlayer();

            service.StartDialogue(_dialogue, HandleChoiceSelected, HandleDialogueComplete);

            // 뷰가 없는 등의 이유로 대화가 열리지 않았으면 상태를 되돌린다
            if (!service.IsDialogueActive)
            {
                HandleDialogueComplete();
            }
        }

        // 대화가 닫힌 직후 호출된다. 닫을 때 완료 콜백이 선택 콜백보다 먼저 불린다.
        private void HandleDialogueComplete()
        {
            _isDialoguePlaying = false;
            _endedFrame = Time.frameCount;
            UnlockPlayer();
        }

        private void HandleChoiceSelected(string choiceId)
        {
            if (choiceId != _enterChoiceId)
                return;

            if (_speaker != null)
            {
                _speaker.enabled = false;
            }

            _isEntering = true;

            var director = CutsceneDirector.Current;
            if (_entryCutscene == null || director == null || !director.Play(_entryCutscene))
            {
                Debug.LogWarning($"[DungeonPortal] '{name}': 입장 컷씬을 재생할 수 없어 바로 던전에 입장합니다.", this);
                StartRun();
                return;
            }

            CutsceneDirector.CutsceneFinished += HandleCutsceneFinished;
        }

        private void HandleCutsceneFinished(CutsceneAsset asset, bool completed)
        {
            if (asset != _entryCutscene) return;

            CutsceneDirector.CutsceneFinished -= HandleCutsceneFinished;
            StartRun();
        }

        private void StartRun()
        {
            var gameManager = GameManager.Instance;
            if (gameManager != null)
            {
                gameManager.StartRun();
            }
        }

        private void LockPlayer()
        {
            var player = PlayerComponent.PlayerInstance;
            if (player == null)
                return;

            _lockedInput = player.GetComponent<PlayerInputReader>();
            if (_lockedInput != null)
            {
                _lockedInput.SetActionsEnabled(false);
            }
        }

        private void UnlockPlayer()
        {
            if (_lockedInput != null)
            {
                _lockedInput.SetActionsEnabled(true);
                _lockedInput = null;
            }
        }
    }
}
