using SlotBattle.Core.Data;

namespace SlotBattle.Core.Battle
{
    /// <summary>
    /// 날아가는 탄 하나(직사탄 또는 곡사탄). 위치 Y는 유닛과 같은 좌표(내 성 0 ~ 적 성 laneLength).
    /// 이동과 도착은 BattleSimulation이 틱마다 계산한다.
    /// </summary>
    public sealed class Projectile
    {
        internal Projectile(int id, AttackType type, Side side, int lane, float y,
            Unit attacker, Castle sourceCastle, Unit targetUnit, Castle targetCastle, float targetY,
            float speed, float damage, float radius)
        {
            Id = id;
            Type = type;
            Side = side;
            Lane = lane;
            Y = y;
            FromY = y;
            Attacker = attacker;
            SourceCastle = sourceCastle;
            TargetUnit = targetUnit;
            TargetCastle = targetCastle;
            TargetY = targetY;
            Speed = speed;
            Damage = damage;
            Radius = radius;
        }

        public int Id { get; }
        public AttackType Type { get; }
        /// <summary>쏜 쪽.</summary>
        public Side Side { get; }
        public int Lane { get; }
        public float Y { get; internal set; }
        /// <summary>쏜 위치.</summary>
        public float FromY { get; }
        /// <summary>쏜 유닛. 성 포격이면 null. 쏜 뒤 죽었을 수 있다.</summary>
        public Unit Attacker { get; }
        /// <summary>포격한 성. 유닛이 쏜 탄이면 null.</summary>
        public Castle SourceCastle { get; }
        /// <summary>직사탄이 따라가는 유닛, 또는 곡사탄을 쏠 때 노린 유닛.</summary>
        public Unit TargetUnit { get; }
        /// <summary>성을 노린 탄이면 그 성.</summary>
        public Castle TargetCastle { get; }
        /// <summary>떨어질 위치. 직사탄은 대상 유닛을 따라 매 틱 바뀌고, 곡사탄은 쏠 때 정해진다.</summary>
        public float TargetY { get; internal set; }
        /// <summary>초당 이동 거리.</summary>
        public float Speed { get; }
        /// <summary>쏜 순간의 공격력.</summary>
        public float Damage { get; }
        /// <summary>곡사탄의 폭발 반경. 직사탄은 0.</summary>
        public float Radius { get; }
    }
}
