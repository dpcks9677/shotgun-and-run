using UnityEngine;

namespace ShotgunAndRun.Player
{
    public class RunPlayerBinder : MonoBehaviour
    {
        public Player _player;
        public RunRules _rules;

        private const string OverTimePenaltySourceId = "RunOverTime";

        public void ApplyOverTimePenalty()
        {
            if (_player == null || _player.RuntimeStats == null)
            {
                return;
            }

            _player.RuntimeStats.SetMaxHealthMultiplier(
                OverTimePenaltySourceId,
                _rules.OverTimeMaxHealthDebuffMultiplier);

            _player.RuntimeStats.SetMoveSpeedMultiplier(
                OverTimePenaltySourceId,
                _rules.OverTimeMoveSpeedDebuffMultiplier);
        }

        public void RemoveOverTimePenalty()
        {
            if (_player == null || _player.RuntimeStats == null)
            {
                return;
            }

            _player.RuntimeStats.RemoveMaxHealthMultiplier(
                OverTimePenaltySourceId
            );

            _player.RuntimeStats.RemoveMoveSpeedMultiplier(
                OverTimePenaltySourceId);
        }
    }
}
