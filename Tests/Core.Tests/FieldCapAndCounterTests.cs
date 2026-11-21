using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SlotBattle.Core.Battle;
using SlotBattle.Core.Data;

namespace SlotBattle.Core.Tests
{
    /// <summary>설계서 2장 "전투 규칙"의 전장 상한과 상성 피해.</summary>
    public class FieldCapAndCounterTests
    {
        // ── 전장 상한 ──

        [Test]
        public void 전장_상한과_상성_배율은_battle_json에_있다()
        {
            Assert.AreEqual(8, TestHelper.Data.Battle.MaxFieldUnits);
            Assert.AreEqual(1.5f, TestHelper.Data.Battle.CounterMultiplier, 1e-6);
        }

        private static BattleSimulation SimWithPlayerUnits(int count)
        {
            var sim = TestHelper.NewSimWithoutCastleFire();
            for (int i = 0; i < count; i++)
            {
                TestHelper.Spawn(sim, "shield", Side.Player, i % 2, 1f + i * 0.5f);
            }
            return sim;
        }

        [Test]
        public void 내_유닛이_8기면_놓을_수_없고_대기_칸에_남는다()
        {
            var sim = SimWithPlayerUnits(8);
            sim.AddToQueueForTest("blade", 1);
            Assert.AreEqual(CommandResult.FieldFull, sim.Execute(new PlaceUnitCommand(0, 0, 5f)));
            Assert.AreEqual(1, sim.Queue.Count);
            Assert.AreEqual(8, sim.FieldUnitCount(Side.Player));
        }

        [Test]
        public void 자리가_나면_다시_놓을_수_있다()
        {
            var sim = SimWithPlayerUnits(8);
            sim.AddToQueueForTest("blade", 1);
            sim.Units.First(u => u.Side == Side.Player).Hp = 0f;
            sim.Tick();
            Assert.AreEqual(7, sim.FieldUnitCount(Side.Player));
            Assert.AreEqual(CommandResult.Ok, sim.Execute(new PlaceUnitCommand(0, 0, 5f)));
            Assert.AreEqual(8, sim.FieldUnitCount(Side.Player));
        }

        [Test]
        public void 트윈봇은_두_자리를_쓴다()
        {
            var sim = SimWithPlayerUnits(7);
            sim.AddToQueueForTest("twin", 1);
            sim.AddToQueueForTest("blade", 1);
            Assert.AreEqual(CommandResult.FieldFull, sim.Execute(new PlaceUnitCommand(0, 0, 5f)), "7기 + 2기 = 9기");
            Assert.AreEqual(CommandResult.Ok, sim.Execute(new PlaceUnitCommand(1, 0, 5f)), "1기짜리는 들어간다");
        }

        [Test]
        public void 적은_자리가_날_때까지_기다렸다가_시간표_순서대로_나온다()
        {
            var stage = new StageDef
            {
                Id = 97,
                Name = "cap",
                Spawns = new List<SpawnDef>
                {
                    new SpawnDef { Time = 1f, Lane = 0, Unit = "shield" },
                    new SpawnDef { Time = 1.5f, Lane = 1, Unit = "blade" },
                },
            };
            var sim = TestHelper.NewSimWithoutCastleFire(stage);
            var frozen = new List<Unit>();
            for (int i = 0; i < 8; i++)
            {
                frozen.Add(TestHelper.SpawnFrozenEnemy(sim, "shield", i % 2, 18f - i * 0.3f));
            }
            TestHelper.Ticks(sim, 40);
            Assert.AreEqual(8, sim.FieldUnitCount(Side.Enemy), "가득 차 있어 나오지 않는다");

            frozen[0].Hp = 0f;
            sim.Tick(); // 이 틱 끝에 쓰러진 유닛이 빠진다
            sim.Tick();
            var arrived = sim.Units.Where(u => u.Side == Side.Enemy && !frozen.Contains(u)).ToList();
            Assert.AreEqual(1, arrived.Count, "한 자리가 나면 먼저 기다린 한 기만");
            Assert.AreEqual("shield", arrived[0].Def.Id);
            Assert.AreEqual(8, sim.FieldUnitCount(Side.Enemy));
        }

        // ── 상성 피해 ──

        [Test]
        public void 상성표는_units_json의_strongAgainst에_있다()
        {
            var d = TestHelper.Data;
            CollectionAssert.AreEquivalent(new[] { "shooter", "plasma" }, d.GetUnit("blade").StrongAgainst);
            CollectionAssert.AreEquivalent(new[] { "shield", "drone" }, d.GetUnit("shooter").StrongAgainst);
            CollectionAssert.AreEquivalent(new[] { "blade", "dash" }, d.GetUnit("shield").StrongAgainst);
            CollectionAssert.AreEquivalent(new[] { "shooter" }, d.GetUnit("twin").StrongAgainst);
            CollectionAssert.AreEquivalent(new[] { "shield", "twin" }, d.GetUnit("boom").StrongAgainst);
            CollectionAssert.AreEquivalent(new[] { "twin" }, d.GetUnit("plasma").StrongAgainst);
            CollectionAssert.AreEquivalent(new[] { "shooter", "plasma" }, d.GetUnit("dash").StrongAgainst);
            CollectionAssert.AreEquivalent(new[] { "blade", "shield", "dash" }, d.GetUnit("drone").StrongAgainst);
        }

        [Test]
        public void 강한_상대를_때리면_피해가_1_5배()
        {
            var sim = TestHelper.NewSimWithoutCastleFire();
            TestHelper.Spawn(sim, "blade", Side.Player, 0, 5f);
            var shooter = TestHelper.SpawnFrozenEnemy(sim, "shooter", 0, 5.5f);
            sim.DrainEvents();
            sim.Tick();
            var hit = sim.DrainEvents().OfType<UnitHitEvent>().Single(e => e.Target == shooter);
            Assert.AreEqual(20f * 1.5f, hit.Damage, 1e-4);
            Assert.AreEqual(60f - 30f, shooter.Hp, 1e-4);
        }

        [Test]
        public void 강한_상대가_아니면_그대로()
        {
            var sim = TestHelper.NewSimWithoutCastleFire();
            TestHelper.Spawn(sim, "blade", Side.Player, 0, 5f);
            var shield = TestHelper.SpawnFrozenEnemy(sim, "shield", 0, 5.5f);
            sim.DrainEvents();
            sim.Tick();
            Assert.AreEqual(20f, sim.DrainEvents().OfType<UnitHitEvent>().Single(e => e.Target == shield).Damage, 1e-4);
        }

        [Test]
        public void 탄도_맞는_대상에_따라_상성_피해를_준다()
        {
            var sim = TestHelper.NewSimWithoutCastleFire();
            TestHelper.Spawn(sim, "shooter", Side.Player, 0, 5f);
            var shield = TestHelper.SpawnFrozenEnemy(sim, "shield", 0, 8f);
            sim.DrainEvents();
            TestHelper.TickAndLand(sim);
            var hit = sim.DrainEvents().OfType<UnitHitEvent>().First(e => e.Target == shield);
            Assert.AreEqual(15f * 1.5f, hit.Damage, 1e-4);
        }

        [Test]
        public void 곡사탄_폭발은_맞은_대상마다_따로_정한다()
        {
            var sim = TestHelper.NewSimWithoutCastleFire();
            TestHelper.Spawn(sim, "plasma", Side.Player, 0, 5f);
            var twins = TestHelper.Spawn(sim, "twin", Side.Enemy, 0, 8f);
            var blade = TestHelper.SpawnFrozenEnemy(sim, "blade", 0, 8.1f);
            foreach (var u in sim.Units.Where(u => u.Def.Id == "twin"))
            {
                u.StunTicks = 100000;
            }
            sim.DrainEvents();
            TestHelper.TickAndLand(sim);
            var hits = sim.DrainEvents().OfType<UnitHitEvent>().ToList();
            Assert.AreEqual(30f * 1.5f, hits.First(e => e.Target.Def.Id == "twin").Damage, 1e-4, "트윈봇에는 상성 피해");
            Assert.AreEqual(30f, hits.Single(e => e.Target == blade).Damage, 1e-4, "블레이드봇에는 그대로");
        }

        [Test]
        public void 건물에는_상성_피해가_없다()
        {
            var sim = TestHelper.NewSimWithoutCastleFire();
            TestHelper.Spawn(sim, "blade", Side.Player, 0, 19.5f);
            float before = sim.EnemyCastle.Hp;
            sim.Tick();
            Assert.AreEqual(20f, before - sim.EnemyCastle.Hp, 1e-4);
        }

        [Test]
        public void strongAgainst에_없는_유닛이_있으면_거부한다()
        {
            string units = "{\"units\":[{\"id\":\"x\",\"rarity\":\"common\",\"hp\":1,\"attackType\":\"melee\",\"strongAgainst\":[\"nope\"]}]}";
            string slot = "{\"rarityWeights\":{\"common\":25},\"tierMultipliers\":[1,2.2,5],\"startingDeck\":[]}";
            string battle = "{\"laneCount\":2}";
            string stages = "{\"stages\":[]}";
            Assert.Throws<System.InvalidOperationException>(() => GameData.FromJson(units, slot, battle, stages));
        }
    }
}
