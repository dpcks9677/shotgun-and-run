using System;
using System.Collections.Generic;
using ShotgunAndRun.Quest;

namespace ShotgunAndRun.Core
{
    // 런 종료 시점의 결과 스냅샷 (결과 화면, 정산, 퀘스트 판정에 사용)
    public sealed class RunResult
    {
        public RunResult(
            RunOutcome outcome,
            float duration,
            int normalKills,
            int eliteKills,
            int clearedZoneCount,
            int baseXp,
            int finalXp)
            : this(outcome, duration, normalKills, eliteKills, clearedZoneCount, baseXp, finalXp, string.Empty, null,
                null)
        {
        }

        public RunResult(
            RunOutcome outcome,
            float duration,
            int normalKills,
            int eliteKills,
            int clearedZoneCount,
            int baseXp,
            int finalXp,
            string deathCause,
            IReadOnlyList<RunItemRecord> securedItems = null,
            IReadOnlyList<RunItemRecord> lostItems = null,
            bool isAbandoned = false)
        {
            Outcome = outcome;
            Duration = duration;
            NormalKills = normalKills;
            EliteKills = eliteKills;
            ClearedZoneCount = clearedZoneCount;
            BaseXp = baseXp;
            FinalXp = finalXp;
            DeathCause = deathCause ?? string.Empty;
            SecuredItems = securedItems ?? Array.Empty<RunItemRecord>();
            LostItems = lostItems ?? Array.Empty<RunItemRecord>();
            IsAbandoned = isAbandoned;
        }

        public bool IsAbandoned
        {
            get;
        }

        public RunOutcome Outcome
        {
            get;
        }
        public float Duration
        {
            get;
        }
        public int NormalKills
        {
            get;
        }
        public int EliteKills
        {
            get;
        }
        public int ClearedZoneCount
        {
            get;
        }

        // 배율 적용 전 런 XP
        public int BaseXp
        {
            get;
        }

        // 탈출(x1.5) 또는 사망(x0.3) 배율을 적용한 최종 XP
        public int FinalXp
        {
            get;
        }

        // 사망 원인 (탈출 시 빈 문자열)
        public string DeathCause
        {
            get;
        }

        // 탈출 성공으로 쉘터 보관함에 회수된 전리품 목록
        public IReadOnlyList<RunItemRecord> SecuredItems
        {
            get;
        }

        // 사망으로 인해 던전에 유실된 전리품 목록
        public IReadOnlyList<RunItemRecord> LostItems
        {
            get;
        }

        // 탈출 시 퀘스트 판정 결과 (퀘스트 시스템이 없거나 탈출이 아니면 null). GameManager.EndRun이 설정합니다.
        public QuestReadyReport QuestReport
        {
            get;
            internal set;
        }
    }
}
