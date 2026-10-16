using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShotgunAndRun.Core
{
    // 런 1회의 시간과 기록을 관리합니다.
    // MonoBehaviour가 아니므로 GameManager가 매 프레임 Tick을 호출합니다.
    public sealed class RunSession
    {
        private readonly RunRules _rules;
        private readonly List<string> _clearedZones = new List<string>();

        // 습격 타이머 객체 필드 & 프로퍼티
        private RaidTimer _raidTimer;
        public RaidTimer RaidTimer => _raidTimer;

        public RunSession(RunRules rules)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));

            // 습격 타이머 기능 설정
            _raidTimer = new RaidTimer();
        }

        // 탈출 구역 활성화 시점 (런 시작 3분 후)
        public event Action ExtractionUnlocked;

        // 제한 시간 초과 시점 (런 시작 10분 후) → 페널티 적용
        public event Action TimeLimitExceeded;

        public event Action<RunResult> Ended;

        // 탈출 구역 대기 진행률 0 ~ 1 (UI 게이지용)
        public event Action<float> ExtractionHoldChanged;

        // 탈출 구역을 벗어나 대기 시간이 초기화됨
        public event Action ExtractionHoldCancelled;

        public float Elapsed
        {
            get;
            private set;
        }

        // 컷씬 등으로 런 진행(경과 시간 · 습격 타이머)을 멈춘 상태
        public bool IsPaused
        {
            get;
            private set;
        }

        public float RemainingTime => Mathf.Max(0f, _rules.TimeLimit - Elapsed);
        public float TimeUntilExtraction => Mathf.Max(0f, _rules.ExtractionUnlockTime - Elapsed);

        public bool IsExtractionUnlocked
        {
            get;
            private set;
        }

        public bool IsOverTime
        {
            get;
            private set;
        }

        public bool IsEnded => Result != null;

        public RunResult Result
        {
            get;
            private set;
        }

        public int NormalKills
        {
            get;
            private set;
        }

        public int EliteKills
        {
            get;
            private set;
        }

        public IReadOnlyList<string> ClearedZones => _clearedZones;

        // 탈출 구역 안에서 머문 시간 (대기 중이 아니면 0)
        public float ExtractionHoldTime
        {
            get;
            private set;
        }

        public float RequiredExtractionHoldTime => _rules.ExtractionHoldTime;

        public float ExtractionHoldProgress =>
            _rules.ExtractionHoldTime > 0f ? Mathf.Clamp01(ExtractionHoldTime / _rules.ExtractionHoldTime) : 1f;

        public bool IsExtractionHoldComplete =>
            IsExtractionUnlocked && ExtractionHoldTime >= _rules.ExtractionHoldTime;

        public int RunXp =>
            NormalKills * _rules.NormalKillXp
            + EliteKills * _rules.EliteKillXp
            + _clearedZones.Count * _rules.ZoneClearXp;

        public void Tick(float deltaTime)
        {
            if (IsEnded || IsPaused || deltaTime <= 0f)
            {
                return;
            }

            Elapsed += deltaTime;

            // 습격 타이머 진행
            _raidTimer.Tick(deltaTime);

            if (!IsExtractionUnlocked && Elapsed >= _rules.ExtractionUnlockTime)
            {
                IsExtractionUnlocked = true;
                ExtractionUnlocked?.Invoke();
            }

            if (!IsOverTime && Elapsed >= _rules.TimeLimit)
            {
                IsOverTime = true;
                TimeLimitExceeded?.Invoke();
            }
        }

        // 컷씬처럼 플레이어가 조작할 수 없는 동안 경과 시간과 습격 타이머를 멈춥니다.
        public void SetPaused(bool paused)
        {
            IsPaused = paused;
        }

        // 플레이어가 활성화된 탈출 구역 안에 있는 동안 매 프레임 호출합니다.
        public void AdvanceExtractionHold(float deltaTime)
        {
            if (IsEnded || !IsExtractionUnlocked || deltaTime <= 0f)
            {
                return;
            }

            ExtractionHoldTime += deltaTime;
            ExtractionHoldChanged?.Invoke(ExtractionHoldProgress);
        }

        public void CancelExtractionHold()
        {
            if (ExtractionHoldTime <= 0f)
            {
                return;
            }

            ExtractionHoldTime = 0f;
            ExtractionHoldCancelled?.Invoke();
        }

        public void RegisterKill(bool isElite)
        {
            if (IsEnded)
            {
                return;
            }

            if (isElite)
            {
                EliteKills++;
            }
            else
            {
                NormalKills++;
            }
        }

        // 같은 존은 한 번만 인정합니다.
        public bool RegisterZoneCleared(string zoneId)
        {
            if (IsEnded || string.IsNullOrEmpty(zoneId) || _clearedZones.Contains(zoneId))
            {
                return false;
            }

            _clearedZones.Add(zoneId);
            return true;
        }

        // 이미 끝난 런이면 기존 결과를 그대로 반환하고 이벤트를 다시 발행하지 않습니다.
        public RunResult End(
            RunOutcome outcome,
            string deathCause = null,
            IReadOnlyList<RunItemRecord> securedItems = null,
            IReadOnlyList<RunItemRecord> lostItems = null,
            bool isAbandoned = false)
        {
            if (IsEnded)
            {
                return Result;
            }

            int baseXp = RunXp;
            float multiplier = isAbandoned
                ? 0f
                : (outcome == RunOutcome.Extracted ? _rules.ExtractionXpMultiplier : _rules.DeathXpMultiplier);

            Result = new RunResult(
                outcome,
                Elapsed,
                NormalKills,
                EliteKills,
                _clearedZones.Count,
                baseXp,
                Mathf.FloorToInt(baseXp * multiplier),
                deathCause,
                securedItems,
                lostItems,
                isAbandoned);

            Ended?.Invoke(Result);
            return Result;
        }
    }
}
