namespace SlotBattle.Core.Battle
{
    /// <summary>
    /// Core가 알리는 사건. Presentation은 이것을 받아 연출만 재생한다.
    /// </summary>
    public abstract class BattleEvent
    {
        protected BattleEvent(int tick)
        {
            Tick = tick;
        }

        public int Tick { get; }
    }

    /// <summary>릴 하나가 멈춤. 다시 돌리기에서는 고정하지 않은 칸만 알린다.</summary>
    public sealed class ReelStoppedEvent : BattleEvent
    {
        public ReelStoppedEvent(int tick, int reelIndex, int symbol, bool isRespin) : base(tick)
        {
            ReelIndex = reelIndex;
            Symbol = symbol;
            IsRespin = isRespin;
        }

        public int ReelIndex { get; }
        public int Symbol { get; }
        public bool IsRespin { get; }
    }

    public sealed class JackpotEvent : BattleEvent
    {
        public JackpotEvent(int tick, int symbol) : base(tick)
        {
            Symbol = symbol;
        }

        public int Symbol { get; }
    }

    /// <summary>슬롯 결과를 확정해 대기 칸에 넣음.</summary>
    public sealed class UnitsQueuedEvent : BattleEvent
    {
        public UnitsQueuedEvent(int tick, int count) : base(tick)
        {
            Count = count;
        }

        public int Count { get; }
    }

    /// <summary>유닛이 전장에 나타남(내 배치와 적 시간표 모두).</summary>
    public sealed class UnitSummonedEvent : BattleEvent
    {
        public UnitSummonedEvent(int tick, Unit unit) : base(tick)
        {
            Unit = unit;
        }

        public Unit Unit { get; }
    }

    /// <summary>
    /// 유닛이 타격을 받음. Attacker가 있으면 유닛의 공격, SourceCastle이 있으면 성 포격,
    /// 둘 다 null이면 사령관 기술.
    /// </summary>
    public sealed class UnitHitEvent : BattleEvent
    {
        public UnitHitEvent(int tick, Unit attacker, Unit target, float damage, Castle sourceCastle = null) : base(tick)
        {
            Attacker = attacker;
            Target = target;
            Damage = damage;
            SourceCastle = sourceCastle;
        }

        public Unit Attacker { get; }
        public Castle SourceCastle { get; }
        public Unit Target { get; }
        public float Damage { get; }
    }

    public sealed class UnitDiedEvent : BattleEvent
    {
        public UnitDiedEvent(int tick, Unit unit) : base(tick)
        {
            Unit = unit;
        }

        public Unit Unit { get; }
    }

    public sealed class CastleDamagedEvent : BattleEvent
    {
        public CastleDamagedEvent(int tick, Castle castle, Unit attacker, float damage) : base(tick)
        {
            Castle = castle;
            Attacker = attacker;
            Damage = damage;
        }

        public Castle Castle { get; }
        public Unit Attacker { get; }
        public float Damage { get; }
    }

    /// <summary>자폭 또는 범위 공격의 폭발 지점.</summary>
    public sealed class ExplosionEvent : BattleEvent
    {
        public ExplosionEvent(int tick, Unit source, int lane, float y, float radius) : base(tick)
        {
            Source = source;
            Lane = lane;
            Y = y;
            Radius = radius;
        }

        public Unit Source { get; }
        public int Lane { get; }
        public float Y { get; }
        public float Radius { get; }
    }

    public sealed class CommanderSkillUsedEvent : BattleEvent
    {
        public CommanderSkillUsedEvent(int tick, int lane, float y, float radius) : base(tick)
        {
            Lane = lane;
            Y = y;
            Radius = radius;
        }

        public int Lane { get; }
        public float Y { get; }
        public float Radius { get; }
    }

    public sealed class BattleEndedEvent : BattleEvent
    {
        public BattleEndedEvent(int tick, BattleOutcome outcome) : base(tick)
        {
            Outcome = outcome;
        }

        public BattleOutcome Outcome { get; }
    }
}
