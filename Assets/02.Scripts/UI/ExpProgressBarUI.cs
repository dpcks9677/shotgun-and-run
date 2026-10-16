using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// 플레이어의 누적 경험치와 레벨 진행도를 시각적으로 보여주는 프로그레스바 UI 컴포넌트입니다.
    /// game-spec.md §9.1 명세 수치를 기본값으로 지원하며, 인스펙터에서 커스텀 경험치 테이블로도 재정의할 수 있습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ExpProgressBarUI : MonoBehaviour
    {
        // game-spec.md §9.1 기준 레벨업 필요 경험치 (Lv.1->2 ~ Lv.9->10)
        private static readonly int[] DefaultRequiredXpTable = new int[]
        {
            1200, // Lv.1 -> 2
            1400, // Lv.2 -> 3
            1650, // Lv.3 -> 4
            1850, // Lv.4 -> 5
            2100, // Lv.5 -> 6
            2300, // Lv.6 -> 7
            2550, // Lv.7 -> 8
            2750, // Lv.8 -> 9
            2950 // Lv.9 -> 10
        };

        [Header("UI Components")]
        [Tooltip("경험치 슬라이더 (Unity UI Slider)")]
        [SerializeField]
        private Slider _slider;
        [Tooltip("슬라이더 대신 Image Filled 방식을 사용할 경우 (선택 사항)")]
        [SerializeField]
        private Image _fillImage;
        [Tooltip("레벨 텍스트 (예: Lv.4)")]
        [SerializeField]
        private TextMeshProUGUI _levelText;
        [Tooltip("경험치 비율 텍스트 (예: 1,143 / 1,400)")]
        [SerializeField]
        private TextMeshProUGUI _expRatioText;
        [Tooltip("획득 경험치 강조 텍스트 (예: +525 XP, 선택 사항)")]
        [SerializeField]
        private TextMeshProUGUI _expGainedText;

        [Header("Custom Level Table (비워두면 game-spec 9.1 기본 명세서 자동 적용)")]
        [Tooltip("각 레벨에서 다음 레벨로 가기 위한 필요 경험치 목록 (비워두면 기본 명세서 테이블 1,200 ~ 2,950 적용)")]
        [SerializeField]
        private int[] _customRequiredXpTable;

        [Header("Animation Settings")]
        [Tooltip("게이지가 차오르는 기본 연출 시간 (초)")]
        [SerializeField, Range(0.3f, 3f)]
        private float _fillDuration = 1.2f;
        [Tooltip("레벨업 발생 시 게이지 리셋 전 잠시 멈추는 연출 시간 (초)")]
        [SerializeField, Range(0.1f, 1f)]
        private float _levelUpPauseDuration = 0.3f;
        [Tooltip("레벨업 시 레벨 텍스트 스케일 펀치 크기 배율")]
        [SerializeField]
        private float _levelUpPunchScale = 1.3f;

        [Header("Text Formats")]
        [SerializeField] private string _levelFormat = "Lv.{0}";
        [SerializeField] private string _expRatioFormat = "{0:N0} / {1:N0}";
        [SerializeField] private string _maxLevelRatioText = "MAX";
        [SerializeField] private string _gainFormat = "+{0:N0} XP";

        public event Action<int> OnLevelUp;

        private Coroutine _animateRoutine;
        private bool _isAnimating;
        private int _targetTotalXp;
        private Action _pendingCompleteCallback;

        /// <summary>
        /// 현재 적용된 레벨 테이블을 반환합니다 (커스텀이 지정되어 있지 않으면 명세서 기본 테이블).
        /// </summary>
        public int[] ActiveTable =>
            (_customRequiredXpTable != null && _customRequiredXpTable.Length > 0)
                ? _customRequiredXpTable
                : DefaultRequiredXpTable;

        /// <summary>
        /// 지원하는 최대 레벨
        /// </summary>
        public int MaxLevel => ActiveTable.Length + 1;

        private void OnDisable()
        {
            if (_animateRoutine != null)
            {
                StopCoroutine(_animateRoutine);
                _animateRoutine = null;
            }

            _isAnimating = false;
        }

        /// <summary>
        /// 특정 레벨에서 다음 레벨로 가기 위해 필요한 경험치 반환 (1-indexed: 1 = Lv.1->2)
        /// </summary>
        public int GetRequiredXp(int level)
        {
            int[] table = ActiveTable;
            int index = level - 1;
            if (index < 0) return table[0];
            if (index >= table.Length) return table[table.Length - 1];
            return table[index];
        }

        /// <summary>
        /// 총 누적 경험치를 기반으로 현재 레벨, 현재 레벨 내 누적 경험치, 다음 레벨 필요 경험치, 진행 비율을 계산합니다.
        /// </summary>
        public void CalculateProgress(int totalXp, out int level, out int currentLevelXp, out int requiredForNext,
            out float ratio)
        {
            totalXp = Mathf.Max(0, totalXp);
            int[] table = ActiveTable;
            level = 1;
            currentLevelXp = totalXp;

            for (int i = 0; i < table.Length; i++)
            {
                int req = table[i];
                if (currentLevelXp >= req)
                {
                    currentLevelXp -= req;
                    level++;
                }
                else
                {
                    requiredForNext = req;
                    ratio = req > 0 ? Mathf.Clamp01((float)currentLevelXp / req) : 1f;
                    return;
                }
            }

            // 만렙 도달 시
            level = MaxLevel;
            requiredForNext = table[table.Length - 1];
            currentLevelXp = requiredForNext;
            ratio = 1f;
        }

        /// <summary>
        /// 애니메이션 없이 즉시 경험치와 레벨을 설정합니다.
        /// </summary>
        public void SetInstant(int totalXp)
        {
            CalculateProgress(totalXp, out int level, out int curXp, out int reqXp, out float ratio);
            ApplyVisuals(level, curXp, reqXp, ratio);
        }

        /// <summary>
        /// 이전 경험치에서 새로운 경험치로 부드럽게 차오르는 애니메이션을 시작합니다.
        /// </summary>
        /// <param name="startTotalXp">런 시작 전 누적 경험치</param>
        /// <param name="gainedXp">이번 런에서 획득한 경험치</param>
        /// <param name="onComplete">애니메이션 종료 시 콜백</param>
        public void AnimateExp(int startTotalXp, int gainedXp, Action onComplete = null)
        {
            if (_animateRoutine != null)
            {
                StopCoroutine(_animateRoutine);
            }

            _targetTotalXp = startTotalXp + gainedXp;
            _pendingCompleteCallback = onComplete;

            if (_expGainedText != null)
            {
                _expGainedText.text = string.Format(_gainFormat, gainedXp);
                _expGainedText.gameObject.SetActive(gainedXp > 0);
            }

            _animateRoutine = StartCoroutine(AnimateExpRoutine(startTotalXp, _targetTotalXp));
        }

        /// <summary>
        /// 애니메이션을 즉시 건너뛰고 최종 수치로 점프합니다.
        /// </summary>
        public void SkipAnimation()
        {
            if (!_isAnimating) return;

            if (_animateRoutine != null)
            {
                StopCoroutine(_animateRoutine);
                _animateRoutine = null;
            }

            _isAnimating = false;
            SetInstant(_targetTotalXp);

            Action callback = _pendingCompleteCallback;
            _pendingCompleteCallback = null;
            callback?.Invoke();
        }

        private IEnumerator AnimateExpRoutine(int startTotalXp, int endTotalXp)
        {
            _isAnimating = true;

            CalculateProgress(startTotalXp, out int currentLevel, out int curLevelXp, out int reqXp,
                out float curRatio);
            CalculateProgress(endTotalXp, out int targetLevel, out int targetLevelXp, out int targetReqXp,
                out float targetRatio);

            ApplyVisuals(currentLevel, curLevelXp, reqXp, curRatio);

            if (startTotalXp >= endTotalXp)
            {
                _isAnimating = false;
                _pendingCompleteCallback?.Invoke();
                _pendingCompleteCallback = null;
                yield break;
            }

            // 시작 레벨부터 목표 레벨까지 순차적으로 게이지를 채움
            int workingLevel = currentLevel;
            int workingCurXp = curLevelXp;

            while (workingLevel < targetLevel)
            {
                int neededToLevelUp = reqXp - workingCurXp;
                float startRatio = reqXp > 0 ? (float)workingCurXp / reqXp : 0f;
                float endRatio = 1f;

                // 1) 게이지 채우기
                yield return FillBarRoutine(workingLevel, workingCurXp, reqXp, reqXp, startRatio, endRatio);

                // 2) 레벨업 도달
                workingLevel++;
                workingCurXp = 0;
                reqXp = GetRequiredXp(workingLevel);

                // 레벨업 텍스트 연출 및 이벤트 호출
                OnLevelUp?.Invoke(workingLevel);
                if (_levelText != null)
                {
                    StartCoroutine(PunchScaleRoutine(_levelText.transform));
                }

                // 레벨업 직후 멈칫(하이라이트)
                if (_levelUpPauseDuration > 0f)
                {
                    yield return new WaitForSecondsRealtime(_levelUpPauseDuration);
                }

                // 게이지 0으로 리셋 후 다음 단계 준비
                ApplyVisuals(workingLevel, 0, reqXp, 0f);
            }

            // 마지막 목표 레벨 내에서의 게이지 채우기
            float finalStartRatio = reqXp > 0 ? (float)workingCurXp / reqXp : 0f;
            float finalEndRatio = targetRatio;

            yield return FillBarRoutine(targetLevel, workingCurXp, targetLevelXp, reqXp, finalStartRatio,
                finalEndRatio);

            // 최종 확정 세팅
            ApplyVisuals(targetLevel, targetLevelXp, reqXp, targetRatio);

            _isAnimating = false;
            _animateRoutine = null;

            Action callback = _pendingCompleteCallback;
            _pendingCompleteCallback = null;
            callback?.Invoke();
        }

        private IEnumerator FillBarRoutine(int level, int fromXp, int toXp, int reqXp, float fromRatio, float toRatio)
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0.1f, _fillDuration * Mathf.Abs(toRatio - fromRatio));

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // 부드러운 감속 커브 (EaseOutQuad)
                float easedT = 1f - (1f - t) * (1f - t);

                float currentRatio = Mathf.Lerp(fromRatio, toRatio, easedT);
                int currentXp = Mathf.RoundToInt(Mathf.Lerp(fromXp, toXp, easedT));

                ApplyVisuals(level, currentXp, reqXp, currentRatio);
                yield return null;
            }
        }

        private void ApplyVisuals(int level, int currentXp, int reqXp, float ratio)
        {
            if (_slider != null)
            {
                _slider.value = ratio;
            }

            if (_fillImage != null)
            {
                _fillImage.fillAmount = ratio;
            }

            if (_levelText != null)
            {
                _levelText.text = string.Format(_levelFormat, level);
            }

            if (_expRatioText != null)
            {
                if (level >= MaxLevel)
                {
                    _expRatioText.text = _maxLevelRatioText;
                }
                else
                {
                    _expRatioText.text = string.Format(_expRatioFormat, currentXp, reqXp);
                }
            }
        }

        private IEnumerator PunchScaleRoutine(Transform target)
        {
            if (target == null) yield break;

            Vector3 originalScale = Vector3.one;
            Vector3 peakScale = originalScale * _levelUpPunchScale;

            float upDuration = 0.12f;
            float downDuration = 0.18f;

            float elapsed = 0f;
            while (elapsed < upDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / upDuration);
                target.localScale = Vector3.Lerp(originalScale, peakScale, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < downDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / downDuration);
                target.localScale = Vector3.Lerp(peakScale, originalScale, t);
                yield return null;
            }

            target.localScale = originalScale;
        }
    }
}
