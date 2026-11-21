using SlotBattle.Core.Battle.Keywords;
using SlotBattle.Core.Data;

namespace SlotBattle.Core.Battle
{
    public enum Side
    {
        Player,
        Enemy,
    }

    public enum BattleOutcome
    {
        None,
        Win,
        Lose,
        Draw,
    }

    /// <summary>
    /// 전장의 유닛 하나. 위치 Y는 내 성(0)에서 적 성(laneLength)까지의 좌표.
    /// </summary>
    public sealed class Unit
    {
        internal Unit(int id, UnitDef def, Side side, int tier, int lane, float y,
            float hp, float attack, float range, int attackIntervalTicks, KeywordHandler[] handlers)
        {
            Id = id;
            Def = def;
            Side = side;
            Tier = tier;
            Lane = lane;
            Y = y;
            MaxHp = hp;
            Hp = hp;
            Attack = attack;
            Range = range;
            MoveSpeed = def.MoveSpeed;
            AttackIntervalTicks = attackIntervalTicks;
            Handlers = handlers;
            IsAir = def.HasKeyword(KeywordNames.Air);
            IsAntiAir = def.HasKeyword(KeywordNames.AntiAir);
            for (int i = 0; i < handlers.Length; i++)
            {
                CanMove &= handlers[i].CanMove;
                IsTaunting |= handlers[i].Taunts;
                IsCharger |= handlers[i].Charges;
            }
        }

        /// <summary>스스로 움직이는가(방어진 유닛은 놓은 자리에 머문다).</summary>
        public bool CanMove { get; } = true;
        /// <summary>도발: 적이 먼저 노린다.</summary>
        public bool IsTaunting { get; }
        /// <summary>돌격 키워드가 있는가.</summary>
        public bool IsCharger { get; }
        /// <summary>돌격 중(적을 보고 달려드는 중). 이 상태로 첫 타격을 하면 피해가 세다.</summary>
        public bool IsCharging { get; internal set; }

        public int Id { get; }
        public UnitDef Def { get; }
        public Side Side { get; }
        public int Tier { get; }
        public int Lane { get; }
        public float Y { get; internal set; }
        public float MaxHp { get; }
        public float Hp { get; internal set; }
        public float Attack { get; }
        public float Range { get; }
        /// <summary>초당 이동 거리.</summary>
        public float MoveSpeed { get; }
        public int AttackIntervalTicks { get; }
        public int CooldownTicks { get; internal set; }
        public int StunTicks { get; internal set; }
        public bool IsAir { get; }
        public bool IsAntiAir { get; }

        public bool IsAlive => Hp > 0f;
        public bool IsStunned => StunTicks > 0;
        /// <summary>적 성 쪽 방향. 내 유닛은 +1, 적 유닛은 -1.</summary>
        public int Direction => Side == Side.Player ? 1 : -1;

        internal KeywordHandler[] Handlers { get; }

        public override string ToString()
        {
            return Side + " " + Def.Id + " T" + Tier + " #" + Id + " L" + Lane + " y=" + Y.ToString("0.00") + " hp=" + Hp.ToString("0");
        }
    }

    public sealed class Castle
    {
        internal Castle(Side side, float maxHp, float y)
        {
            Side = side;
            MaxHp = maxHp;
            Hp = maxHp;
            Y = y;
        }

        public Side Side { get; }
        public float MaxHp { get; }
        public float Hp { get; internal set; }
        public float Y { get; }
        /// <summary>다음 포격까지 남은 틱.</summary>
        public int CooldownTicks { get; internal set; }
        public bool IsDestroyed => Hp <= 0f;
        public float HpRatio => MaxHp <= 0f ? 0f : (Hp < 0f ? 0f : Hp) / MaxHp;
    }

    /// <summary>대기 칸에 들어간 유닛 한 묶음.</summary>
    public sealed class QueuedUnit
    {
        public QueuedUnit(UnitDef def, int tier)
        {
            Def = def;
            Tier = tier;
        }

        public UnitDef Def { get; }
        public int Tier { get; }
    }
}
