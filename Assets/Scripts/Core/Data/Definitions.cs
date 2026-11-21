using System.Collections.Generic;

namespace SlotBattle.Core.Data
{
    /// <summary>공격 방식(units.json의 attackType). 설계서 2장 "공격 방식과 탄".</summary>
    public enum AttackType
    {
        /// <summary>지정하지 않음. 데이터 검증에서 거른다.</summary>
        None,
        /// <summary>근접: 공격한 틱에 피해.</summary>
        Melee,
        /// <summary>직사탄: 대상을 따라가 도착할 때 피해. 대상이 먼저 죽으면 사라진다.</summary>
        Bolt,
        /// <summary>곡사탄: 쏠 때의 대상 위치에 떨어져 반경 안 적에게 피해.</summary>
        Lob,
        /// <summary>자폭: 자기 위치 반경 안에 그 틱에 피해를 주고 파괴된다.</summary>
        Explode,
        /// <summary>돌진: 규칙은 근접과 같고 연출만 다르다.</summary>
        Charge,
    }

    /// <summary>units.json의 유닛 한 종. 1단계 기준 수치.</summary>
    public sealed class UnitDef
    {
        public string Id;
        public string Name;
        public string Rarity;
        public float Hp;
        public float Attack;
        public float AttackInterval;
        public AttackType AttackType;
        /// <summary>원거리 사거리. 근접 계열(근접, 돌진, 자폭)은 battle.json의 meleeRange를 쓴다.</summary>
        public float Range;
        /// <summary>직사탄과 곡사탄의 초당 이동 거리.</summary>
        public float ProjectileSpeed;
        public float MoveSpeed;
        public List<string> Keywords = new List<string>();
        /// <summary>폭발 반경: 곡사탄, 자폭, splash 키워드가 쓴다.</summary>
        public float SplashRadius;
        /// <summary>swarm 키워드가 쓰는 한 묶음의 기체 수.</summary>
        public int SwarmCount;
        /// <summary>상성표의 강한 상대(유닛 id). 이 상대를 공격하면 피해에 battle.json의 counterMultiplier를 곱한다.</summary>
        public List<string> StrongAgainst = new List<string>();
        /// <summary>1이면 1차 출시, 2 이상은 업데이트 번호.</summary>
        public int Release = 1;

        /// <summary>근접 계열(근접, 돌진, 자폭)이라 사거리로 meleeRange를 쓰는가.</summary>
        public bool Melee => AttackType == AttackType.Melee || AttackType == AttackType.Charge || AttackType == AttackType.Explode;

        /// <summary>탄을 쏘는 공격인가.</summary>
        public bool FiresProjectile => AttackType == AttackType.Bolt || AttackType == AttackType.Lob;

        public bool HasKeyword(string keyword)
        {
            return Keywords != null && Keywords.Contains(keyword);
        }

        public bool IsStrongAgainst(UnitDef other)
        {
            return StrongAgainst != null && other != null && StrongAgainst.Contains(other.Id);
        }
    }

    public sealed class ChargeConfig
    {
        /// <summary>앞쪽 이 거리 안에 적이 보이면 돌격한다.</summary>
        public float Range = 4f;
        public float SpeedMultiplier = 2f;
        /// <summary>돌격 뒤 첫 타격의 피해 배율.</summary>
        public float DamageMultiplier = 2f;
    }

    public sealed class SlotConfig
    {
        public int ReelCount = 3;
        public int SpinCost;
        public int RespinCost;
        public int RespinsPerSpin;
        public int MaxLockedReels;
        public Dictionary<string, int> RarityWeights = new Dictionary<string, int>();
        /// <summary>인덱스 0이 1단계. 체력과 공격력에 곱한다.</summary>
        public float[] TierMultipliers;
        public int DeckSize;
        public List<string> StartingDeck = new List<string>();
    }

    public sealed class CommanderSkillConfig
    {
        public int ManaCost;
        public float Radius;
        public float Damage;
        public float StunSeconds;
        public float CooldownSeconds;
    }

    /// <summary>성 포격: 사거리 안의 가장 가까운 적 유닛 하나를 공격한다. 피해가 0이면 포격하지 않는다.</summary>
    public sealed class CastleDefenseConfig
    {
        public float Damage;
        public float AttackInterval = 1f;
        /// <summary>성에서의 거리.</summary>
        public float Range;
        /// <summary>포탄(직사탄)의 초당 이동 거리.</summary>
        public float ProjectileSpeed;
    }

    public sealed class BattleConfig
    {
        public float DurationSeconds;
        public int LaneCount;
        public float LaneLength;
        public float PlayerZoneLength;
        public float CastleHp;
        public float StartMana;
        public float MaxMana;
        public float ManaRegenPerSecond;
        public float FinalPhaseSeconds;
        public float FinalPhaseRegenMultiplier;
        public int WaitingQueueCapacity;
        public float MeleeRange;
        public float SwarmSpacing;
        public float EnemySpawnDistance;
        /// <summary>한 편이 전장에 동시에 둘 수 있는 기체 수. 0이면 제한 없음.</summary>
        public int MaxFieldUnits;
        /// <summary>상성표의 강한 상대를 공격할 때 피해 배율. 1이면 상성 피해 없음.</summary>
        public float CounterMultiplier = 1f;
        /// <summary>돌격 키워드(설계서 2장 "유닛 행동").</summary>
        public ChargeConfig Charge = new ChargeConfig();
        /// <summary>유닛 능력치 배율(양쪽 모두). units.json은 유닛끼리의 비율을 정하는 기준값이다.</summary>
        public float UnitHpScale = 1f;
        public float UnitAttackScale = 1f;
        public CommanderSkillConfig CommanderSkill = new CommanderSkillConfig();
        public CastleDefenseConfig CastleDefense = new CastleDefenseConfig();
    }

    /// <summary>적 소환 시간표의 한 줄. repeatEvery가 0보다 크면 repeatUntil까지 반복한다.</summary>
    public sealed class SpawnDef
    {
        public float Time;
        public int Lane;
        public string Unit;
        public int Tier = 1;
        /// <summary>적 성에서 떨어진 거리. 0 이하면 battle.json의 enemySpawnDistance.</summary>
        public float Distance;
        public float RepeatEvery;
        public float RepeatUntil;
    }

    public sealed class StageDef
    {
        public int Id;
        public string Name;
        /// <summary>0 이하면 battle.json의 castleHp.</summary>
        public float EnemyCastleHp;
        public float EnemyStatMultiplier = 1f;
        public int RewardGold;
        public List<SpawnDef> Spawns = new List<SpawnDef>();
    }
}
