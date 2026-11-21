using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SlotBattle.Core.Battle;
using SlotBattle.Core.Data;
using SlotBattle.Tools.BalanceSim;

namespace SlotBattle.Core.Tests
{
    internal static class TestHelper
    {
        private static GameData _data;
        private static GameData _rules;
        private static GameData _noCastleFire;

        /// <summary>Assets/Data의 실제 JSON.</summary>
        public static GameData Data => _data ?? (_data = DataFiles.Load());

        /// <summary>
        /// 규칙 테스트용: 실제 JSON에서 유닛 능력치 배율만 1로 둔다.
        /// 그래서 기대값을 units.json 기준값(설계서 3장 표)으로 바로 쓸 수 있다. 배율 자체는 별도 테스트가 본다.
        /// </summary>
        public static GameData RuleData
        {
            get
            {
                if (_rules == null)
                {
                    var battle = LoadBattle();
                    battle.UnitHpScale = 1f;
                    battle.UnitAttackScale = 1f;
                    _rules = new GameData(Data.Units, Data.Slot, battle, Data.Stages);
                }
                return _rules;
            }
        }

        private static BattleConfig LoadBattle()
        {
            string json = File.ReadAllText(Path.Combine(DataFiles.FindDataDirectory(), "battle.json"));
            return JsonConvert.DeserializeObject<BattleConfig>(json);
        }

        public static int SpinCost => Data.Slot.SpinCost;

        /// <summary>성 포격만 끈 데이터. 성 근처에서 다른 규칙을 떼어 볼 때 쓴다.</summary>
        public static GameData DataWithoutCastleFire
        {
            get
            {
                if (_noCastleFire == null)
                {
                    var battle = LoadBattle();
                    battle.UnitHpScale = 1f;
                    battle.UnitAttackScale = 1f;
                    battle.CastleDefense.Damage = 0f;
                    _noCastleFire = new GameData(Data.Units, Data.Slot, battle, Data.Stages);
                }
                return _noCastleFire;
            }
        }

        public static BattleSimulation NewSimWithoutCastleFire(StageDef stage = null, ulong seed = 1)
        {
            return new BattleSimulation(DataWithoutCastleFire, stage ?? EmptyStage(), Deck, seed);
        }

        public static IReadOnlyList<string> Deck => Data.Slot.StartingDeck;

        /// <summary>적이 나오지 않는 빈 스테이지. 규칙 하나만 떼어 볼 때 쓴다.</summary>
        public static StageDef EmptyStage(float enemyCastleHp = 0f)
        {
            return new StageDef { Id = 99, Name = "test", EnemyCastleHp = enemyCastleHp, Spawns = new List<SpawnDef>() };
        }

        public static BattleSimulation NewSim(StageDef stage = null, ulong seed = 1, IReadOnlyList<string> deck = null)
        {
            return new BattleSimulation(RuleData, stage ?? EmptyStage(), deck ?? Deck, seed);
        }

        public static Unit Spawn(BattleSimulation sim, string unitId, Side side, int lane, float y, int tier = 1)
        {
            return sim.SpawnForTest(unitId, tier, side, lane, y)[0];
        }

        /// <summary>적을 제자리에 묶어 둔다(이동과 공격 모두 안 함).</summary>
        public static Unit SpawnFrozenEnemy(BattleSimulation sim, string unitId, int lane, float y, int tier = 1)
        {
            var unit = Spawn(sim, unitId, Side.Enemy, lane, y, tier);
            unit.StunTicks = 100000;
            return unit;
        }

        public static void Ticks(BattleSimulation sim, int count)
        {
            for (int i = 0; i < count; i++)
            {
                sim.Tick();
            }
        }

        /// <summary>첫 스핀 결과가 조건을 만족하는 시드를 찾는다.</summary>
        public static ulong FindSeed(Func<int[], bool> predicate)
        {
            for (ulong seed = 1; seed < 100000; seed++)
            {
                var sim = NewSim(seed: seed);
                sim.SetManaForTest(10);
                sim.Execute(new SpinCommand());
                if (predicate(sim.PendingSpin.Symbols.ToArray()))
                {
                    return seed;
                }
            }
            throw new InvalidOperationException("조건에 맞는 시드가 없습니다.");
        }

        public static bool AllDifferent(int[] s) => s[0] != s[1] && s[1] != s[2] && s[0] != s[2];
        public static bool ExactlyPair(int[] s) => !AllDifferent(s) && !(s[0] == s[1] && s[1] == s[2]);
        public static bool Triple(int[] s) => s[0] == s[1] && s[1] == s[2];
    }
}
