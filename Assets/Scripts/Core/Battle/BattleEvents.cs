using SlotBattle.Core.Data;

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

    /// <summary>
    /// 공격이 시작됨. 근접·돌진·자폭은 같은 틱에 피해가 나고, 직사탄·곡사탄은 같은 틱에 탄이 발사된다.
    /// Attacker가 null이면 SourceCastle의 포격. 대상은 TargetUnit 또는 TargetCastle 중 하나.
    /// </summary>
    public sealed class AttackStartedEvent : BattleEvent
    {
        public AttackStartedEvent(int tick, Unit attacker, Castle sourceCastle, Unit targetUnit, Castle targetCastle, AttackType type) : base(tick)
        {
            Attacker = attacker;
            SourceCastle = sourceCastle;
            TargetUnit = targetUnit;
            TargetCastle = targetCastle;
            Type = type;
        }

        public Unit Attacker { get; }
        public Castle SourceCastle { get; }
        public Unit TargetUnit { get; }
        public Castle TargetCastle { get; }
        public AttackType Type { get; }
    }

    /// <summary>탄이 발사됨. 탄의 위치는 BattleSimulation.Projectiles에서 매 틱 읽는다.</summary>
    public sealed class ProjectileLaunchedEvent : BattleEvent
    {
        public ProjectileLaunchedEvent(int tick, Projectile projectile) : base(tick)
        {
            Projectile = projectile;
        }

        public Projectile Projectile { get; }
    }

    /// <summary>
    /// 탄이 도착함. Hit이 false면 직사탄의 대상이 먼저 죽어 탄이 사라진 것이다.
    /// 곡사탄은 항상 떨어지며(Hit = true), 피해는 함께 알리는 ExplosionEvent와 UnitHitEvent로 본다.
    /// </summary>
    public sealed class ProjectileArrivedEvent : BattleEvent
    {
        public ProjectileArrivedEvent(int tick, Projectile projectile, bool hit) : base(tick)
        {
            Projectile = projectile;
            Hit = hit;
        }

        public Projectile Projectile { get; }
        public bool Hit { get; }
        public float Y => Projectile.Y;
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

    /// <summary>자폭, 곡사탄 착탄, 범위 공격의 폭발 지점과 반경. Source는 폭발을 일으킨 유닛(이미 죽었을 수 있음).</summary>
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
