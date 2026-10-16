using System;
using UnityEngine;

namespace ShotgunAndRun.Core
{
    // sealed: 다른 클래스에서 상속 불가 ( 부모 클래스 불가능 )
    public sealed class RaidTimer
    {
        // 습격 주기 필드
        private float _raidDuration;
        private float _currentCoolTime;
        private const float WarningTime = 10f;
        private bool _isWarningStarted;

        // 타이머 중지 / 재개
        private bool _isPaused;
        public void PauseTimer() => _isPaused = true;
        public void ResumeTimer() => _isPaused = false;

        // 프로퍼티
        public float RaidDuration => _raidDuration;
        public float CurrentCoolTime => _currentCoolTime;
        public bool IsPaused => _isPaused;

        // 이벤트
        public event Action WarningEvent; // 10초 전 경고 이벤트
        public event Action RaidEvent; // 레이드 이벤트

        // 생성자
        public RaidTimer()
        {
            _currentCoolTime = 0f;
            _isWarningStarted = false;
            ResetTimer();
            Debug.Log("레이드 타이머 첫 시작");
        }

        // Tick
        public void Tick(float deltaTime)
        {
            // 0. 방어코드 deltaTime 음수 처리
            if (_isPaused || deltaTime <= 0f) return;

            // 1. 쿨타임 진행
            _currentCoolTime += deltaTime;

            // 2. 남은 시간 체크 및 경고 시간 조건
            float remainingTime = _raidDuration - _currentCoolTime;

            if (!_isWarningStarted && remainingTime <= WarningTime)
            {
                _isWarningStarted = true;
                WarningEvent?.Invoke();
            }

            // 3. 습격 시작 및 다음 타이머 설정
            if (_currentCoolTime < _raidDuration) return;

            Debug.Log($"습격 시작");
            ResetTimer();
            RaidEvent?.Invoke();
        }

        private void ResetTimer()
        {
            _raidDuration = UnityEngine.Random.Range(RunRules.RaidMinDuration, RunRules.RaidMaxDuration);

            _currentCoolTime = 0f;
            _isWarningStarted = false;

            Debug.Log("레이드 타이머 리셋");
        }
    }
}