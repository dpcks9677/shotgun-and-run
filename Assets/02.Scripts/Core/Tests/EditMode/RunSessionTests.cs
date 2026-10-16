using NUnit.Framework;

namespace ShotgunAndRun.Core.Tests
{
    public sealed class RunSessionTests
    {
        [Test]
        public void Tick_RaisesExtractionUnlockedOnceAtUnlockTime()
        {
            RunSession session = new RunSession(new RunRules { ExtractionUnlockTime = 180f, TimeLimit = 600f });
            int unlockCount = 0;
            session.ExtractionUnlocked += () => unlockCount++;

            session.Tick(179.9f);
            Assert.That(unlockCount, Is.EqualTo(0));
            Assert.That(session.IsExtractionUnlocked, Is.False);

            session.Tick(0.1f);
            session.Tick(10f);

            Assert.That(unlockCount, Is.EqualTo(1));
            Assert.That(session.IsExtractionUnlocked, Is.True);
            Assert.That(session.TimeUntilExtraction, Is.EqualTo(0f));
        }

        [Test]
        public void Tick_RaisesTimeLimitExceededOnceAtTimeLimit()
        {
            RunSession session = new RunSession(new RunRules { ExtractionUnlockTime = 180f, TimeLimit = 600f });
            int exceededCount = 0;
            session.TimeLimitExceeded += () => exceededCount++;

            session.Tick(599f);
            Assert.That(session.RemainingTime, Is.EqualTo(1f).Within(0.001f));
            Assert.That(exceededCount, Is.EqualTo(0));

            session.Tick(1f);
            session.Tick(30f);

            Assert.That(exceededCount, Is.EqualTo(1));
            Assert.That(session.IsOverTime, Is.True);
            Assert.That(session.RemainingTime, Is.EqualTo(0f));
        }

        [Test]
        public void End_AppliesOutcomeXpMultiplier()
        {
            RunRules rules = new RunRules();

            RunSession extracted = CreateSessionWithRecord(rules);
            RunResult extractedResult = extracted.End(RunOutcome.Extracted);

            RunSession died = CreateSessionWithRecord(rules);
            RunResult diedResult = died.End(RunOutcome.Died);

            RunSession abandoned = CreateSessionWithRecord(rules);
            RunResult abandonedResult = abandoned.End(RunOutcome.Died, GameManager.DeathCauseAbandoned, null, null, true);

            // game-spec 9.2 예시: 적 30마리 + 존 2곳 = 350 XP
            Assert.That(extractedResult.BaseXp, Is.EqualTo(350));
            Assert.That(extractedResult.FinalXp, Is.EqualTo(525));
            Assert.That(diedResult.FinalXp, Is.EqualTo(105));
            Assert.That(abandonedResult.FinalXp, Is.EqualTo(0));
            Assert.That(abandonedResult.IsAbandoned, Is.True);
        }

        [Test]
        public void End_PreservesDeathCauseAndItemRecords()
        {
            RunSession session = new RunSession(new RunRules());
            RunItemRecord[] secured = new[] { new RunItemRecord(null, 5) };
            RunResult result = session.End(RunOutcome.Died, "엘리트 스나이퍼", null, secured);

            Assert.That(result.DeathCause, Is.EqualTo("엘리트 스나이퍼"));
            Assert.That(result.LostItems.Count, Is.EqualTo(1));
            Assert.That(result.LostItems[0].Amount, Is.EqualTo(5));
            Assert.That(result.SecuredItems, Is.Empty);
        }

        [Test]
        public void End_IsIdempotentAndStopsRecording()
        {
            RunSession session = new RunSession(new RunRules());
            int endedCount = 0;
            session.Ended += _ => endedCount++;

            session.Tick(5f);
            RunResult first = session.End(RunOutcome.Died);
            RunResult second = session.End(RunOutcome.Extracted);
            session.Tick(100f);
            session.RegisterKill(false);

            Assert.That(second, Is.SameAs(first));
            Assert.That(first.Outcome, Is.EqualTo(RunOutcome.Died));
            Assert.That(endedCount, Is.EqualTo(1));
            Assert.That(session.Elapsed, Is.EqualTo(5f));
            Assert.That(session.NormalKills, Is.EqualTo(0));
        }

        [Test]
        public void RegisterZoneCleared_CountsEachZoneOnce()
        {
            RunSession session = new RunSession(new RunRules());

            Assert.That(session.RegisterZoneCleared("Z1"), Is.True);
            Assert.That(session.RegisterZoneCleared("Z1"), Is.False);
            Assert.That(session.RegisterZoneCleared(""), Is.False);
            Assert.That(session.ClearedZones.Count, Is.EqualTo(1));
        }

        [Test]
        public void ExtractionHold_IgnoredBeforeUnlockAndCompletesAfterHoldTime()
        {
            RunSession session = new RunSession(new RunRules { ExtractionUnlockTime = 10f, ExtractionHoldTime = 3f });

            session.AdvanceExtractionHold(5f);
            Assert.That(session.ExtractionHoldTime, Is.EqualTo(0f));

            session.Tick(10f);
            session.AdvanceExtractionHold(2f);
            Assert.That(session.IsExtractionHoldComplete, Is.False);
            Assert.That(session.ExtractionHoldProgress, Is.EqualTo(2f / 3f).Within(0.001f));

            session.AdvanceExtractionHold(1f);
            Assert.That(session.IsExtractionHoldComplete, Is.True);
        }

        [Test]
        public void CancelExtractionHold_ResetsAndNotifiesOnce()
        {
            RunSession session = new RunSession(new RunRules { ExtractionUnlockTime = 0f, ExtractionHoldTime = 10f });
            int cancelCount = 0;
            session.ExtractionHoldCancelled += () => cancelCount++;

            session.Tick(0.1f);
            session.AdvanceExtractionHold(4f);
            session.CancelExtractionHold();
            session.CancelExtractionHold();

            Assert.That(session.ExtractionHoldTime, Is.EqualTo(0f));
            Assert.That(cancelCount, Is.EqualTo(1));
        }

        private static RunSession CreateSessionWithRecord(RunRules rules)
        {
            RunSession session = new RunSession(rules);
            for (int i = 0; i < 30; i++)
            {
                session.RegisterKill(false);
            }

            session.RegisterZoneCleared("Z1");
            session.RegisterZoneCleared("Z2");
            return session;
        }
    }
}
