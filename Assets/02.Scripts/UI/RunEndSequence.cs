using System.Collections;
using ShotgunAndRun.Core;
using ShotgunAndRun.Cutscene;
using UnityEngine;

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// 런이 끝나면 결과에 맞는 컷씬(탈출 CS_TELEPORT_EXIT · 사망 CS_RUN_DEATH)을 재생한 뒤 결과창을 띄웁니다. 던전 씬에 둡니다.
    /// 정산 · 저장은 EndRun에서 이미 끝나 있고, 결과창 표시만 늦춥니다.
    /// 컷씬이 중단되거나 재생할 수 없어도 결과창은 반드시 띄웁니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunEndSequence : MonoBehaviour
    {
        [Tooltip("탈출 직후 재생할 컷씬 (CS_TELEPORT_EXIT)")]
        [SerializeField] private CutsceneAsset _exitCutscene;

        [Tooltip("사망 직후 재생할 컷씬 (CS_RUN_DEATH)")]
        [SerializeField] private CutsceneAsset _deathCutscene;

        [Tooltip("사망 순간 시간을 멈추는 시간(초, 실시간)")]
        [SerializeField, Min(0f)] private float _hitStopSeconds = 0.2f;

        [Tooltip("비어 있으면 씬에서 찾는다")]
        [SerializeField] private GameResultUI _resultUI;

        private GameManager _subscribedManager;
        private Coroutine _bindRoutine;
        private RunResult _pendingResult;
        private CutsceneAsset _playingCutscene;
        private bool _hitStopping;

        /// <summary>
        /// 활성 상태인 시퀀스. 있으면 GameResultUI는 결과창을 직접 띄우지 않고 이 시퀀스에 맡깁니다.
        /// </summary>
        public static RunEndSequence Active
        {
            get; private set;
        }

        private void OnEnable()
        {
            Active = this;
            Subscribe();
            _bindRoutine = StartCoroutine(BindGameManagerRoutine());
        }

        private void OnDisable()
        {
            if (Active == this)
            {
                Active = null;
            }

            if (_bindRoutine != null)
            {
                StopCoroutine(_bindRoutine);
                _bindRoutine = null;
            }

            if (_subscribedManager != null)
            {
                _subscribedManager.RunEnded -= HandleRunEnded;
                _subscribedManager = null;
            }

            CutsceneDirector.CutsceneFinished -= HandleCutsceneFinished;

            // 히트스톱 도중 꺼져도 시간이 멈춘 채 남지 않게 한다
            if (_hitStopping)
            {
                Time.timeScale = 1f;
                _hitStopping = false;
            }
        }

        private IEnumerator BindGameManagerRoutine()
        {
            while (GameManager.Instance == null)
            {
                yield return null;
            }

            Subscribe();
            _bindRoutine = null;
        }

        private void Subscribe()
        {
            if (_subscribedManager != null || GameManager.Instance == null) return;

            _subscribedManager = GameManager.Instance;
            _subscribedManager.RunEnded += HandleRunEnded;
        }

        private void HandleRunEnded(RunResult result)
        {
            if (_pendingResult != null) return;

            _pendingResult = result;
            StartCoroutine(PlayEndRoutine(result));
        }

        private IEnumerator PlayEndRoutine(RunResult result)
        {
            // 사망 순간 잠깐 멈춰 충격을 준 뒤 컷씬을 시작한다
            if (result.Outcome != RunOutcome.Extracted)
            {
                _hitStopping = true;
                Time.timeScale = 0f;
                yield return new WaitForSecondsRealtime(_hitStopSeconds);
                Time.timeScale = 1f;
                _hitStopping = false;
            }

            var cutscene = result.Outcome == RunOutcome.Extracted ? _exitCutscene : _deathCutscene;
            var director = CutsceneDirector.Current;
            if (cutscene == null || director == null || !director.Play(cutscene))
            {
                Debug.LogWarning($"[RunEndSequence] '{name}': {result.Outcome} 컷씬을 재생할 수 없어 결과창을 바로 띄웁니다.", this);
                ShowResult();
                yield break;
            }

            _playingCutscene = cutscene;
            CutsceneDirector.CutsceneFinished += HandleCutsceneFinished;
        }

        private void HandleCutsceneFinished(CutsceneAsset asset, bool completed)
        {
            if (asset != _playingCutscene) return;

            _playingCutscene = null;
            CutsceneDirector.CutsceneFinished -= HandleCutsceneFinished;

            // 컷씬이 끝나며 걷힌 화면 덮개를 같은 프레임에 다시 덮어, 검은 화면 위에 결과창을 띄운다
            if (_pendingResult.Outcome != RunOutcome.Extracted)
            {
                CutsceneDirector.Current.Screen.SetOverlay(Color.black, 1f);
            }

            ShowResult();
        }

        private void ShowResult()
        {
            var result = _pendingResult;
            _pendingResult = null;

            if (_resultUI == null)
            {
                _resultUI = FindFirstObjectByType<GameResultUI>(FindObjectsInactive.Include);
            }

            if (_resultUI != null)
            {
                _resultUI.ShowResult(result);
            }
            else
            {
                Debug.LogError($"[RunEndSequence] '{name}': GameResultUI를 찾을 수 없어 결과창을 띄우지 못했습니다.", this);
            }
        }
    }
}
