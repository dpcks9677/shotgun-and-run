using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShotgunAndRun.UI.Dialogue
{
    /// <summary>
    /// 대화창 및 튜토리얼 UI의 시각적 요소(화자, 텍스트, 가이드 이미지 등)를 조합하여 제어하는 뷰 컴포넌트 (IDialogueView, Composition)
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueUI : MonoBehaviour, IDialogueView
    {
        [Header("루트 패널")]
        [Tooltip("대화 UI 전체를 감싸는 부모 오브젝트")]
        [SerializeField] private GameObject _panelRoot;

        [Header("화자 정보 (Speaker)")]
        [SerializeField] private TextMeshProUGUI _speakerNameText;
        [SerializeField] private GameObject _speakerNameRoot;
        [SerializeField] private Image _speakerPortraitImage;
        [SerializeField] private GameObject _speakerPortraitRoot;

        [Header("튜토리얼 / 가이드 이미지 (Guide)")]
        [SerializeField] private Image _guideImage;
        [SerializeField] private GameObject _guideImageRoot;

        [Header("대사 텍스트")]
        [SerializeField] private TextMeshProUGUI _dialogueText;
        [SerializeField] private GameObject _nextIndicator;

        [Header("조합 컴포넌트 (Composition)")]
        [Tooltip("텍스트 연출 컴포넌트 (ITextAnimator를 구현한 컴포넌트, 비어있으면 자동 탐색)")]
        [SerializeField] private MonoBehaviour _textAnimatorComponent;

        [Tooltip("선택지 버튼들을 관리하는 그룹 컴포넌트")]
        [SerializeField] private DialogueChoiceGroupUI _choiceGroup;

        private ITextAnimator _textAnimator;

        public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;
        public bool IsAnimatingText => _textAnimator != null && _textAnimator.IsAnimating;
        public bool IsShowingChoices => _choiceGroup != null && _choiceGroup.IsShowing;

        private void Awake()
        {
            if (_panelRoot == null)
            {
                _panelRoot = gameObject;
            }

            // ITextAnimator 인터페이스 바인딩
            if (_textAnimatorComponent is ITextAnimator animator)
            {
                _textAnimator = animator;
            }
            else
            {
                _textAnimator = GetComponentInChildren<ITextAnimator>(true);
            }

            // DialogueChoiceGroupUI 컴포넌트 바인딩
            if (_choiceGroup == null)
            {
                _choiceGroup = GetComponentInChildren<DialogueChoiceGroupUI>(true);
            }

            Close();
        }

        public void Open(Sprite guideImage)
        {
            if (_panelRoot != null)
                _panelRoot.SetActive(true);

            // 가이드 이미지 (튜토리얼/안내)
            bool hasGuide = guideImage != null;
            if (_guideImage != null)
            {
                _guideImage.sprite = guideImage;
                _guideImage.enabled = hasGuide;
            }
            if (_guideImageRoot != null)
                _guideImageRoot.SetActive(hasGuide);

            if (_choiceGroup != null)
                _choiceGroup.Clear();

            SetNextIndicator(false);
        }

        public void SetSpeaker(string speakerName, Sprite speakerPortrait)
        {
            // 화자 이름
            bool hasSpeaker = !string.IsNullOrWhiteSpace(speakerName);
            if (_speakerNameText != null)
                _speakerNameText.text = speakerName;
            if (_speakerNameRoot != null)
                _speakerNameRoot.SetActive(hasSpeaker);

            // 화자 초상화
            bool hasPortrait = speakerPortrait != null;
            if (_speakerPortraitImage != null)
            {
                _speakerPortraitImage.sprite = speakerPortrait;
                _speakerPortraitImage.enabled = hasPortrait;
            }
            if (_speakerPortraitRoot != null)
                _speakerPortraitRoot.SetActive(hasPortrait);
        }

        public void Close()
        {
            if (_textAnimator != null)
                _textAnimator.Stop();

            if (_choiceGroup != null)
                _choiceGroup.Clear();

            SetNextIndicator(false);

            if (_dialogueText != null)
                _dialogueText.text = string.Empty;

            if (_panelRoot != null)
                _panelRoot.SetActive(false);
        }

        public void DisplaySentence(string sentence, bool isLastSentence, Action onComplete)
        {
            if (_choiceGroup != null)
                _choiceGroup.Clear();

            SetNextIndicator(false);

            if (_textAnimator != null)
            {
                _textAnimator.AnimateText(_dialogueText, sentence, () =>
                {
                    onComplete?.Invoke();
                    if (!isLastSentence)
                        SetNextIndicator(true);
                });
            }
            else
            {
                if (_dialogueText != null)
                    _dialogueText.text = sentence;

                onComplete?.Invoke();
                if (!isLastSentence)
                    SetNextIndicator(true);
            }
        }

        public void CompleteSentenceImmediately(bool isLastSentence, Action onComplete)
        {
            if (_textAnimator != null && _textAnimator.IsAnimating)
            {
                _textAnimator.CompleteImmediately(_dialogueText, () =>
                {
                    onComplete?.Invoke();
                    if (!isLastSentence)
                        SetNextIndicator(true);
                });
            }
        }

        public void ShowChoices(IReadOnlyList<DialogueChoice> choices, Action<string> onChoiceSelected)
        {
            SetNextIndicator(false);

            if (_choiceGroup != null)
            {
                _choiceGroup.ShowChoices(choices, onChoiceSelected);
            }
        }

        public void MoveChoiceSelection(int direction)
        {
            if (_choiceGroup != null)
                _choiceGroup.MoveSelection(direction);
        }

        public void SubmitSelectedChoice()
        {
            if (_choiceGroup != null)
                _choiceGroup.SubmitSelected();
        }

        public void SetNextIndicator(bool active)
        {
            if (_nextIndicator != null)
                _nextIndicator.SetActive(active);
        }
    }
}
