using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ShotgunAndRun.UI.Dialogue
{
    /// <summary>
    /// 대화창의 선택지 버튼 목록을 동적으로 배치하고 이벤트를 연결하는 전담 UI 컴포넌트 (SRP)
    /// 키보드 이동/확정을 위해 현재 선택된 선택지 하나를 관리합니다.
    /// </summary>
    public class DialogueChoiceGroupUI : MonoBehaviour
    {
        [Tooltip("선택지 박스 루트 (선택지가 없을 때 숨김, 비어있으면 이 오브젝트)")]
        [SerializeField] private GameObject _root;

        [Tooltip("선택지 버튼들이 놓일 부모 컨테이너")]
        [SerializeField] private Transform _container;

        [Tooltip("선택지 버튼 프리팹")]
        [SerializeField] private DialogueChoiceButtonUI _buttonPrefab;

        private readonly List<DialogueChoiceButtonUI> _buttonPool = new List<DialogueChoiceButtonUI>();
        private int _activeCount;
        private int _selectedIndex = -1;

        public bool IsShowing => _activeCount > 0 && Root.activeInHierarchy;

        private GameObject Root => _root != null ? _root : gameObject;

        private void Awake()
        {
            if (_container == null)
            {
                _container = transform;
            }
        }

        /// <summary>
        /// 선택지 목록을 받아 버튼들을 갱신 및 표시하고 첫 번째 선택지를 선택합니다.
        /// </summary>
        public void ShowChoices(IReadOnlyList<DialogueChoice> choices, Action<string> onChoiceSelected)
        {
            Clear();

            if (choices == null || choices.Count == 0)
                return;

            // 비활성 상태로 시작한 경우 여기서 Awake가 호출되어 _container가 준비된다
            Root.SetActive(true);

            for (int i = 0; i < choices.Count; i++)
            {
                var button = GetOrCreateButton(i);
                if (button == null)
                    break;

                var choice = choices[i];
                int index = i;
                button.Setup(
                    choice.ChoiceText,
                    () => onChoiceSelected?.Invoke(choice.ChoiceId),
                    () => Select(index));
                _activeCount++;
            }

            if (_activeCount == 0)
            {
                Clear();
                return;
            }

            // 이전에 클릭된 버튼이 EventSystem에 선택된 채로 남아 Submit이 중복 전달되지 않도록 비운다
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            Select(0);
        }

        /// <summary>
        /// 선택을 위(-1) 또는 아래(1)로 이동합니다. 끝에서는 반대편으로 순환합니다.
        /// </summary>
        public void MoveSelection(int direction)
        {
            if (!IsShowing || direction == 0)
                return;

            int next = (_selectedIndex + direction) % _activeCount;
            if (next < 0)
            {
                next += _activeCount;
            }

            Select(next);
        }

        /// <summary>
        /// 현재 선택된 선택지를 확정합니다.
        /// </summary>
        public void SubmitSelected()
        {
            if (!IsShowing || _selectedIndex < 0)
                return;

            _buttonPool[_selectedIndex].Submit();
        }

        /// <summary>
        /// 모든 선택지 버튼과 선택지 박스를 숨깁니다.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < _buttonPool.Count; i++)
            {
                if (_buttonPool[i] != null)
                {
                    _buttonPool[i].gameObject.SetActive(false);
                }
            }

            _activeCount = 0;
            _selectedIndex = -1;
            Root.SetActive(false);
        }

        private void Select(int index)
        {
            _selectedIndex = index;

            for (int i = 0; i < _activeCount; i++)
            {
                _buttonPool[i].SetSelected(i == index);
            }
        }

        private DialogueChoiceButtonUI GetOrCreateButton(int index)
        {
            if (index < _buttonPool.Count)
            {
                return _buttonPool[index];
            }

            if (_buttonPrefab == null || _container == null)
            {
                Debug.LogError("[DialogueChoiceGroupUI] ButtonPrefab 또는 Container가 할당되지 않았습니다.");
                return null;
            }

            var newButton = Instantiate(_buttonPrefab, _container);
            _buttonPool.Add(newButton);
            return newButton;
        }
    }
}
