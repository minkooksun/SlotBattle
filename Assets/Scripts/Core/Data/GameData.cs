using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using SlotBattle.Core.Battle.Keywords;

namespace SlotBattle.Core.Data
{
    /// <summary>
    /// JSON 데이터 묶음. 파일 읽기는 하지 않고 문자열만 받는다
    /// (Unity는 TextAsset, 테스트와 BalanceSim은 파일에서 읽어 넘긴다).
    /// </summary>
    public sealed class GameData
    {
        private readonly Dictionary<string, UnitDef> _units;

        public BattleConfig Battle { get; }
        public SlotConfig Slot { get; }
        public IReadOnlyList<UnitDef> Units { get; }
        public IReadOnlyList<StageDef> Stages { get; }

        public GameData(IReadOnlyList<UnitDef> units, SlotConfig slot, BattleConfig battle, IReadOnlyList<StageDef> stages)
        {
            Units = units ?? throw new ArgumentNullException(nameof(units));
            Slot = slot ?? throw new ArgumentNullException(nameof(slot));
            Battle = battle ?? throw new ArgumentNullException(nameof(battle));
            Stages = stages ?? new List<StageDef>();
            _units = new Dictionary<string, UnitDef>();
            foreach (var unit in units)
            {
                if (_units.ContainsKey(unit.Id))
                {
                    throw new InvalidOperationException("유닛 id 중복: " + unit.Id);
                }
                _units.Add(unit.Id, unit);
            }
            Validate();
        }

        public static GameData FromJson(string unitsJson, string slotJson, string battleJson, string stagesJson)
        {
            var units = JsonConvert.DeserializeObject<UnitsFile>(unitsJson).Units;
            var slot = JsonConvert.DeserializeObject<SlotConfig>(slotJson);
            var battle = JsonConvert.DeserializeObject<BattleConfig>(battleJson);
            var stages = JsonConvert.DeserializeObject<StagesFile>(stagesJson).Stages;
            return new GameData(units, slot, battle, stages);
        }

        public UnitDef GetUnit(string id)
        {
            if (!_units.TryGetValue(id, out var def))
            {
                throw new KeyNotFoundException("알 수 없는 유닛: " + id);
            }
            return def;
        }

        public bool TryGetUnit(string id, out UnitDef def)
        {
            return _units.TryGetValue(id, out def);
        }

        public StageDef GetStage(int id)
        {
            foreach (var stage in Stages)
            {
                if (stage.Id == id)
                {
                    return stage;
                }
            }
            throw new KeyNotFoundException("알 수 없는 스테이지: " + id);
        }

        public int RarityWeight(UnitDef def)
        {
            if (!Slot.RarityWeights.TryGetValue(def.Rarity, out var weight))
            {
                throw new KeyNotFoundException("가중치가 없는 등급: " + def.Rarity);
            }
            return weight;
        }

        public float TierMultiplier(int tier)
        {
            return Slot.TierMultipliers[tier - 1];
        }

        public int MaxTier => Slot.TierMultipliers.Length;

        private void Validate()
        {
            foreach (var unit in Units)
            {
                if (!Slot.RarityWeights.ContainsKey(unit.Rarity))
                {
                    throw new InvalidOperationException(unit.Id + ": 등급 가중치 없음 " + unit.Rarity);
                }
                foreach (var keyword in unit.Keywords)
                {
                    if (!KeywordRegistry.IsKnown(keyword))
                    {
                        throw new InvalidOperationException(unit.Id + ": 처리기가 없는 키워드 " + keyword);
                    }
                }
                ValidateAttack(unit);
            }
            var defense = Battle.CastleDefense;
            if (defense != null && defense.Damage > 0f && defense.ProjectileSpeed <= 0f)
            {
                throw new InvalidOperationException("성 포격: projectileSpeed가 0보다 커야 합니다.");
            }
            foreach (var stage in Stages)
            {
                foreach (var spawn in stage.Spawns)
                {
                    if (!_units.ContainsKey(spawn.Unit))
                    {
                        throw new InvalidOperationException("스테이지 " + stage.Id + ": 알 수 없는 유닛 " + spawn.Unit);
                    }
                    if (spawn.Lane < 0 || spawn.Lane >= Battle.LaneCount)
                    {
                        throw new InvalidOperationException("스테이지 " + stage.Id + ": 잘못된 라인 " + spawn.Lane);
                    }
                    if (spawn.Tier < 1 || spawn.Tier > MaxTier)
                    {
                        throw new InvalidOperationException("스테이지 " + stage.Id + ": 잘못된 단계 " + spawn.Tier);
                    }
                }
            }
            foreach (var id in Slot.StartingDeck)
            {
                if (!_units.ContainsKey(id))
                {
                    throw new InvalidOperationException("시작 덱에 알 수 없는 유닛: " + id);
                }
            }
        }

        private static void ValidateAttack(UnitDef unit)
        {
            switch (unit.AttackType)
            {
                case AttackType.Melee:
                case AttackType.Charge:
                    break;
                case AttackType.Explode:
                    RequirePositive(unit, unit.SplashRadius, "splashRadius");
                    break;
                case AttackType.Bolt:
                    RequirePositive(unit, unit.Range, "range");
                    RequirePositive(unit, unit.ProjectileSpeed, "projectileSpeed");
                    break;
                case AttackType.Lob:
                    RequirePositive(unit, unit.Range, "range");
                    RequirePositive(unit, unit.ProjectileSpeed, "projectileSpeed");
                    RequirePositive(unit, unit.SplashRadius, "splashRadius");
                    break;
                default:
                    throw new InvalidOperationException(unit.Id + ": attackType이 없습니다.");
            }
        }

        private static void RequirePositive(UnitDef unit, float value, string field)
        {
            if (value <= 0f)
            {
                throw new InvalidOperationException(unit.Id + ": " + unit.AttackType + " 공격에는 " + field + "가 0보다 커야 합니다.");
            }
        }

        private sealed class UnitsFile
        {
            public List<UnitDef> Units = new List<UnitDef>();
        }

        private sealed class StagesFile
        {
            public List<StageDef> Stages = new List<StageDef>();
        }
    }
}
