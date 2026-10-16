using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShotgunAndRun.UI.Dialogue
{
    /// <summary>
    /// 대화 시스템의 부품들(View, Input, TimeHandler, Session)을 조합(Composition)하여
    /// 전체 대화 흐름을 오케스트레이션하는 서비스 컴포넌트 (SOLID, IDialogueService)
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueManager : MonoBehaviour, IDialogueService
    {
        public static IDialogueService Instance
        {
            get;
            private set;
        }

        [Header("조합 컴포넌트 (Composition)")]
        [SerializeField] private MonoBehaviour _dialogueViewComponent;

        [SerializeField] private MonoBehaviour _inputProviderComponent;

        // 인터페이스 참조들 (DIP)
        private IDialogueView _view;
        private IDialogueInputProvider _inputProvider;
        private readonly ITimePauseHandler _timeHandler = new TimeScalePauseHandler();

        // 현재 진행 중인 세션 (POCO)
        private DialogueSession _currentSession;
        private DialogueDataSO _currentData;
        private Action<string> _onChoiceSelected;
        private Action _onDialogueComplete;
        private int _startedFrame = -1;

        public bool IsDialogueActive => _view != null && _view.IsOpen;

        public event Action<DialogueDataSO, int, DialogueLine> LineStarted;

        private void Awake()
        {
            // 비동기 씬 전환에서는 새 씬의 Awake가 이전 씬 언로드보다 먼저 실행되므로
            // 같은 씬 안의 중복만 제거하고, 언로드될 이전 씬의 매니저는 새 매니저로 교체합니다.
            if (Instance != null && (DialogueManager)Instance != this &&
                ((DialogueManager)Instance).gameObject.scene == gameObject.scene)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            ResolveDependencies();
        }

        private void OnDestroy()
        {
            if ((DialogueManager)Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (!IsDialogueActive || _inputProvider == null)
                return;

            // 대화를 연 입력(NPC 상호작용 E키 등)이 같은 프레임에 진행 입력으로 다시 읽히지 않도록 무시
            if (Time.frameCount == _startedFrame)
                return;

            if (_view.IsShowingChoices)
            {
                HandleChoiceInput();
                return;
            }

            if (_inputProvider.IsAdvanceTriggered())
            {
                HandleAdvance();
            }
        }

        public void StartDialogue(
            DialogueDataSO data,
            Action<string> onChoiceSelected = null,
            Action onDialogueComplete = null)
        {
            if (data == null)
            {
                Debug.LogWarning("[DialogueManager] DialogueDataSO가 null입니다.");
                return;
            }

            if (_view == null)
            {
                Debug.LogError("[DialogueManager] IDialogueView 구현체가 설정되지 않았습니다.");
                return;
            }

            _currentData = data;
            _currentSession = new DialogueSession(data.Lines, data.Choices);
            _onChoiceSelected = onChoiceSelected;
            _onDialogueComplete = onDialogueComplete;
            _startedFrame = Time.frameCount;

            if (data.PauseGame)
            {
                _timeHandler.Pause();
            }

            _view.Open(data.GuideImage);

            if (_currentSession.HasLines)
            {
                DisplayCurrentLine();
            }
            else
            {
                _view.SetSpeaker(data.SpeakerName, data.SpeakerPortrait);
                PresentChoicesOrClose();
            }
        }

        public void ShowConfirm(
            string message,
            Action onConfirm,
            Action onCancel = null,
            string confirmText = "확인",
            string cancelText = "취소",
            string speakerName = "")
        {
            var choices = new List<DialogueChoice>
            {
                new DialogueChoice(confirmText, "CONFIRM"),
                new DialogueChoice(cancelText, "CANCEL")
            };

            var runtimeData = DialogueDataSO.CreateRuntimeInstance(
                speakerName: speakerName,
                speakerPortrait: null,
                guideImage: null,
                sentences: new[] { message },
                choices: choices,
                pauseGame: false
            );

            StartDialogue(runtimeData, onChoiceSelected: (choiceId) =>
            {
                if (choiceId == "CONFIRM")
                {
                    onConfirm?.Invoke();
                }
                else
                {
                    onCancel?.Invoke();
                }
            });
        }

        public void ShowAlert(
            string message,
            Action onComplete = null,
            string confirmText = "확인",
            string speakerName = "안내")
        {
            var choices = new List<DialogueChoice>
            {
                new DialogueChoice(confirmText, "OK")
            };

            var runtimeData = DialogueDataSO.CreateRuntimeInstance(
                speakerName: speakerName,
                speakerPortrait: null,
                guideImage: null,
                sentences: new[] { message },
                choices: choices,
                pauseGame: false
            );

            StartDialogue(runtimeData, onChoiceSelected: (_) => onComplete?.Invoke());
        }

        public void ShowTutorial(
            IEnumerable<string> sentences,
            Sprite guideImage = null,
            Action onComplete = null,
            string speakerName = "튜토리얼",
            bool pauseGame = true)
        {
            var choices = new List<DialogueChoice>
            {
                new DialogueChoice("확인", "COMPLETE")
            };

            var runtimeData = DialogueDataSO.CreateRuntimeInstance(
                speakerName: speakerName,
                speakerPortrait: null,
                guideImage: guideImage,
                sentences: sentences,
                choices: choices,
                pauseGame: pauseGame
            );

            StartDialogue(runtimeData, onChoiceSelected: (_) => onComplete?.Invoke());
        }

        public void CloseDialogue()
        {
            _timeHandler.Resume();

            if (_view != null)
            {
                _view.Close();
            }

            var completeCallback = _onDialogueComplete;
            _currentSession = null;
            _currentData = null;
            _onChoiceSelected = null;
            _onDialogueComplete = null;

            completeCallback?.Invoke();
        }

        private void DisplayCurrentLine()
        {
            if (_currentSession == null || _currentData == null || _view == null) return;

            DialogueLine line = _currentSession.GetCurrentLine();
            bool isLast = _currentSession.IsLastLine;

            // 대사마다 화자가 바뀔 수 있으므로 매 대사 화자를 다시 설정한다
            _view.SetSpeaker(_currentData.GetSpeakerName(line), _currentData.GetSpeakerPortrait(line));
            LineStarted?.Invoke(_currentData, _currentSession.CurrentIndex, line);
            _view.DisplaySentence(line.Text, isLast, onComplete: () =>
            {
                if (isLast)
                {
                    PresentChoicesOrClose();
                }
            });
        }

        private void HandleAdvance()
        {
            if (_currentSession == null || _view == null) return;

            // 1. 텍스트 애니메이션 진행 중이면 즉시 스킵
            if (_view.IsAnimatingText)
            {
                bool isLast = _currentSession.IsLastLine;
                _view.CompleteSentenceImmediately(isLast, onComplete: () =>
                {
                    if (isLast)
                    {
                        PresentChoicesOrClose();
                    }
                });
                return;
            }

            // 2. 다음 문장이 남아있으면 인덱스 전진
            if (_currentSession.Advance())
            {
                DisplayCurrentLine();
                return;
            }

            // 3. 마지막 문장인데 선택지가 없는 경우 대화 종료
            if (!_currentSession.HasChoices)
            {
                CloseDialogue();
            }
        }

        private void HandleChoiceInput()
        {
            int navigation = _inputProvider.GetChoiceNavigation();
            if (navigation != 0)
            {
                _view.MoveChoiceSelection(navigation);
            }

            if (_inputProvider.IsSubmitTriggered())
            {
                _view.SubmitSelectedChoice();
            }
        }

        private void PresentChoicesOrClose()
        {
            if (_currentSession == null || _view == null) return;

            if (_currentSession.HasChoices)
            {
                _view.ShowChoices(_currentSession.Choices, HandleChoiceSelected);
            }
            else
            {
                _view.SetNextIndicator(true);
            }
        }

        private void HandleChoiceSelected(string choiceId)
        {
            var callback = _onChoiceSelected;
            CloseDialogue();
            callback?.Invoke(choiceId);
        }

        private void ResolveDependencies()
        {
            // View 의존성 바인딩
            if (_dialogueViewComponent is IDialogueView view)
            {
                _view = view;
            }
            else
            {
                _view = GetComponentInChildren<IDialogueView>(true);
            }

            // Input 의존성 바인딩
            if (_inputProviderComponent is IDialogueInputProvider inputProvider)
            {
                _inputProvider = inputProvider;
            }
            else
            {
                _inputProvider = GetComponentInChildren<IDialogueInputProvider>(true);
                if (_inputProvider == null)
                {
                    // 기본 Standalone 입력기 자동 부착
                    _inputProvider = gameObject.AddComponent<StandaloneDialogueInputProvider>();
                }
            }
        }
    }
}
