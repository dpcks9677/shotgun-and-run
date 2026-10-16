using System;
using UnityEngine;

namespace ShotgunAndRun.Core
{
    // GameManager는 씬 밖(DontDestroyOnLoad)에 있어 Button OnClick에 직접 연결할 수 없습니다.
    // 이 컴포넌트를 씬에 두고 OnClick에 연결하면 GameManager의 흐름 메서드를 호출합니다.
    public sealed class GameFlowButtons : MonoBehaviour
    {
        // 타이틀 씬에서만 지정합니다. 저장이 있을 때 New game이 띄우는 덮어쓰기 확인창입니다.
        [SerializeField] private GameObject _overwriteConfirm;
        // 타이틀 씬에서만 지정합니다. 게임을 시작하기 전에 플레이어가 걸어 나가는 연출입니다.
        [SerializeField] private TitleExitWalk _exitWalk;

        public void GoToTitle()
        {
            if (TryGetGameManager(out GameManager gameManager))
            {
                gameManager.GoToTitle();
            }
        }

        // New game 버튼: 저장이 있으면 확인창을 먼저 띄웁니다.
        public void StartGame()
        {
            if (!TryGetGameManager(out GameManager gameManager))
            {
                return;
            }

            if (_overwriteConfirm != null && gameManager.HasSave)
            {
                _overwriteConfirm.SetActive(true);
                return;
            }

            Begin(gameManager.StartNewGame);
        }

        // 확인창의 확인 버튼
        public void ConfirmNewGame()
        {
            if (TryGetGameManager(out GameManager gameManager))
            {
                _overwriteConfirm.SetActive(false);
                Begin(gameManager.StartNewGame);
            }
        }

        // 확인창의 취소 버튼
        public void CancelNewGame()
        {
            _overwriteConfirm.SetActive(false);
        }

        public void LoadGame()
        {
            if (TryGetGameManager(out GameManager gameManager))
            {
                Begin(gameManager.LoadGame);
            }
        }

        public void GoToShelter()
        {
            if (TryGetGameManager(out GameManager gameManager))
            {
                gameManager.GoToShelter();
            }
        }

        public void StartRun()
        {
            if (TryGetGameManager(out GameManager gameManager))
            {
                gameManager.StartRun();
            }
        }

        public void QuitGame()
        {
            if (TryGetGameManager(out GameManager gameManager))
            {
                gameManager.QuitGame();
                return;
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // 걸어 나가는 연출이 있으면 그것이 끝난 뒤에, 없으면 바로 시작합니다.
        private void Begin(Action start)
        {
            if (_exitWalk != null)
            {
                _exitWalk.Play(start);
                return;
            }

            start();
        }

        private bool TryGetGameManager(out GameManager gameManager)
        {
            gameManager = GameManager.Instance;
            if (gameManager != null)
            {
                return true;
            }

            Debug.LogWarning("GameManager is not available.", this);
            return false;
        }
    }
}
