using SlotBattle.Core.Battle;
using SlotBattle.Core.Util;

namespace SlotBattle.Tools.BalanceSim
{
    /// <summary>
    /// 초보형: 돌릴 수 있으면 바로 돌리고, 다시 돌리지 않고 확정하며,
    /// 나온 유닛을 아무 라인·아무 위치에나 즉시 놓는다. 사령관 기술은 쓰지 않는다.
    /// </summary>
    public sealed class NovicePlayer : IAutoPlayer
    {
        private readonly SeededRandom _rng;

        public NovicePlayer(ulong seed)
        {
            _rng = new SeededRandom(seed);
        }

        public string Name => "초보형";

        public void Act(BattleSimulation sim)
        {
            if (sim.IsOver)
            {
                return;
            }
            if (sim.PendingSpin == null && sim.CanSpin)
            {
                sim.Execute(new SpinCommand());
            }
            if (sim.PendingSpin != null)
            {
                sim.Execute(new ConfirmSpinCommand());
            }
            var cfg = sim.Config;
            while (sim.Queue.Count > 0)
            {
                int lane = _rng.NextInt(cfg.LaneCount);
                float position = _rng.NextFloat(0f, cfg.PlayerZoneLength);
                sim.Execute(new PlaceUnitCommand(0, lane, position));
            }
        }
    }
}
