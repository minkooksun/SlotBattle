using System.Collections.Generic;

namespace SlotBattle.Core.Data
{
    /// <summary>units.json의 유닛 한 종. 1단계 기준 수치.</summary>
    public sealed class UnitDef
    {
        public string Id;
        public string Name;
        public string Rarity;
        public float Hp;
        public float Attack;
        public float AttackInterval;
        /// <summary>true면 사거리로 battle.json의 meleeRange를 쓴다.</summary>
        public bool Melee;
        public float Range;
        public float MoveSpeed;
        public List<string> Keywords = new List<string>();
        /// <summary>splash, selfDestruct 키워드가 쓰는 반경.</summary>
        public float SplashRadius;
        /// <summary>swarm 키워드가 쓰는 한 묶음의 기체 수.</summary>
        public int SwarmCount;
        /// <summary>1이면 1차 출시, 2 이상은 업데이트 번호.</summary>
        public int Release = 1;

        public bool HasKeyword(string keyword)
        {
            return Keywords != null && Keywords.Contains(keyword);
        }
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
        public CommanderSkillConfig CommanderSkill = new CommanderSkillConfig();
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
