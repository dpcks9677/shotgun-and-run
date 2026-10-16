using NUnit.Framework;

namespace ShotgunAndRun.Quest.Tests
{
    public sealed class QuestCardStateTests
    {
        [TestCase(QuestSlot.Request, false, QuestState.Available, true, QuestCardState.Available)]
        [TestCase(QuestSlot.Request, false, QuestState.Available, false, QuestCardState.Full)]
        [TestCase(QuestSlot.Request, false, QuestState.Accepted, true, QuestCardState.Active)]
        [TestCase(QuestSlot.Story, false, QuestState.Accepted, false, QuestCardState.StoryActive)]
        [TestCase(QuestSlot.Request, false, QuestState.ReadyToTurnIn, false, QuestCardState.Ready)]
        [TestCase(QuestSlot.Story, false, QuestState.ReadyToTurnIn, false, QuestCardState.Ready)]
        [TestCase(QuestSlot.Story, true, QuestState.ReadyToTurnIn, false, QuestCardState.ReportToNpc)]
        [TestCase(QuestSlot.Request, false, QuestState.Completed, false, QuestCardState.Done)]
        [TestCase(QuestSlot.Story, true, QuestState.Completed, false, QuestCardState.Done)]
        public void TryResolve_MapsState(QuestSlot slot, bool turnInByNpc, QuestState state, bool canAccept,
            QuestCardState expected)
        {
            Assert.IsTrue(QuestCardStateResolver.TryResolve(slot, turnInByNpc, state, canAccept, out QuestCardState result));
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void TryResolve_Locked_ReturnsFalse()
        {
            Assert.IsFalse(QuestCardStateResolver.TryResolve(QuestSlot.Request, false, QuestState.Locked, false, out _));
        }
    }
}
