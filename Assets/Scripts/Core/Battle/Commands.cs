namespace SlotBattle.Core.Battle
{
    /// <summary>플레이어 입력. Core는 입력을 이 명령 객체로만 받는다.</summary>
    public abstract class BattleCommand
    {
    }

    /// <summary>마나를 써서 슬롯을 돌린다.</summary>
    public sealed class SpinCommand : BattleCommand
    {
    }

    /// <summary>고른 칸을 고정하고 나머지를 다시 돌린다.</summary>
    public sealed class RespinCommand : BattleCommand
    {
        public RespinCommand(params int[] lockedReels)
        {
            LockedReels = lockedReels;
        }

        public int[] LockedReels { get; }
    }

    /// <summary>스핀 결과를 확정해 대기 칸에 넣는다.</summary>
    public sealed class ConfirmSpinCommand : BattleCommand
    {
    }

    /// <summary>대기 칸의 유닛을 라인과 위치에 놓는다. Position은 내 성에서의 거리.</summary>
    public sealed class PlaceUnitCommand : BattleCommand
    {
        public PlaceUnitCommand(int queueIndex, int lane, float position)
        {
            QueueIndex = queueIndex;
            Lane = lane;
            Position = position;
        }

        public int QueueIndex { get; }
        public int Lane { get; }
        public float Position { get; }
    }

    /// <summary>사령관 기술(EMP 폭격)을 라인과 위치에 쓴다. Position은 내 성에서의 거리.</summary>
    public sealed class UseCommanderSkillCommand : BattleCommand
    {
        public UseCommanderSkillCommand(int lane, float position)
        {
            Lane = lane;
            Position = position;
        }

        public int Lane { get; }
        public float Position { get; }
    }

    public enum CommandResult
    {
        Ok,
        BattleOver,
        NotEnoughMana,
        QueueFull,
        SpinPending,
        NoPendingSpin,
        RespinLimitReached,
        InvalidLock,
        NotEnoughQueueSpace,
        InvalidQueueIndex,
        InvalidLane,
        InvalidPosition,
        SkillOnCooldown,
        UnknownCommand,
    }

    /// <summary>리플레이용 기록: 몇 번째 틱 직전에 어떤 명령이 들어왔는지.</summary>
    public sealed class LoggedCommand
    {
        public LoggedCommand(int tick, BattleCommand command)
        {
            Tick = tick;
            Command = command;
        }

        public int Tick { get; }
        public BattleCommand Command { get; }
    }
}
