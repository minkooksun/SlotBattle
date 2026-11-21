using System.Linq;
using NUnit.Framework;
using SlotBattle.Core.Battle;
using SlotBattle.Core.Battle.Keywords;

namespace SlotBattle.Core.Tests
{
    /// <summary>설계서 3장 "유닛 특성 규칙": 키워드별 처리기.</summary>
    public class KeywordTests
    {
        [Test]
        public void 모든_유닛의_키워드에_처리기가_있다()
        {
            foreach (var unit in TestHelper.Data.Units)
            {
                foreach (var keyword in unit.Keywords)
                {
                    Assert.IsTrue(KeywordRegistry.IsKnown(keyword), unit.Id + ": " + keyword);
                }
            }
        }

        // ── 공중 / 대공 ──

        [Test]
        public void 공중_유닛은_대공이_없는_유닛에게_공격받지_않는다()
        {
            var sim = TestHelper.NewSim();
            var blade = TestHelper.Spawn(sim, "blade", Side.Player, 0, 5f);
            var plasma = TestHelper.Spawn(sim, "plasma", Side.Player, 0, 4f);
            var drone = TestHelper.SpawnFrozenEnemy(sim, "drone", 0, 5.5f);
            Assert.IsFalse(BattleSimulation.CanAttack(blade, drone));
            Assert.IsFalse(BattleSimulation.CanAttack(plasma, drone), "플라즈마봇은 지상만 공격");
            TestHelper.Ticks(sim, 3);
            Assert.AreEqual(70f, drone.Hp);
            Assert.Greater(blade.Y, 5f, "공격할 수 없는 공중 유닛은 지나쳐 간다");
        }

        [Test]
        public void 대공_유닛은_공중_유닛을_공격한다()
        {
            var sim = TestHelper.NewSim();
            var shooter = TestHelper.Spawn(sim, "shooter", Side.Player, 0, 5f);
            var drone = TestHelper.SpawnFrozenEnemy(sim, "drone", 0, 8f);
            Assert.IsTrue(BattleSimulation.CanAttack(shooter, drone));
            TestHelper.TickAndLand(sim);
            Assert.AreEqual(70f - 15f * 1.5f, drone.Hp, 1e-4, "슈터봇은 드론봇에 강하다");
        }

        [Test]
        public void 공중_유닛은_지상_유닛과_성을_공격한다()
        {
            var sim = TestHelper.NewSim();
            var drone = TestHelper.Spawn(sim, "drone", Side.Player, 0, 5f);
            var ground = TestHelper.SpawnFrozenEnemy(sim, "blade", 0, 7f);
            Assert.IsTrue(BattleSimulation.CanAttack(drone, ground));
            TestHelper.TickAndLand(sim);
            Assert.AreEqual(100f - 14f * 1.5f, ground.Hp, 1e-4, "드론봇은 블레이드봇에 강하다");

            var sim2 = TestHelper.NewSim();
            TestHelper.Spawn(sim2, "drone", Side.Player, 1, 18f);
            TestHelper.TickAndLand(sim2);
            Assert.AreEqual(sim2.EnemyCastle.MaxHp - 14f, sim2.EnemyCastle.Hp);
        }

        [Test]
        public void 드론봇끼리는_서로_공격한다()
        {
            var sim = TestHelper.NewSim();
            var a = TestHelper.Spawn(sim, "drone", Side.Player, 0, 5f);
            var b = TestHelper.Spawn(sim, "drone", Side.Enemy, 0, 7f);
            Assert.IsTrue(BattleSimulation.CanAttack(a, b));
            TestHelper.TickAndLand(sim);
            Assert.AreEqual(56f, a.Hp);
            Assert.AreEqual(56f, b.Hp);
        }

        // ── 범위 ──

        [Test]
        public void 범위_공격은_맞은_지점_반경_안의_적에게도_피해를_준다()
        {
            var sim = TestHelper.NewSim();
            TestHelper.Spawn(sim, "plasma", Side.Player, 0, 5f);
            var target = TestHelper.SpawnFrozenEnemy(sim, "shield", 0, 8f);
            var inside = TestHelper.SpawnFrozenEnemy(sim, "shield", 0, 8.9f);
            var outside = TestHelper.SpawnFrozenEnemy(sim, "shield", 0, 9.5f);
            var otherLane = TestHelper.SpawnFrozenEnemy(sim, "shield", 1, 8f);
            TestHelper.TickAndLand(sim);
            Assert.AreEqual(220f, target.Hp);
            Assert.AreEqual(220f, inside.Hp);
            Assert.AreEqual(250f, outside.Hp);
            Assert.AreEqual(250f, otherLane.Hp);
        }

        [Test]
        public void 범위_공격은_공중_유닛에게_피해를_주지_않는다()
        {
            var sim = TestHelper.NewSim();
            TestHelper.Spawn(sim, "plasma", Side.Player, 0, 5f);
            TestHelper.SpawnFrozenEnemy(sim, "shield", 0, 8f);
            var drone = TestHelper.SpawnFrozenEnemy(sim, "drone", 0, 8.2f);
            TestHelper.TickAndLand(sim);
            Assert.AreEqual(70f, drone.Hp);
        }

        // ── 자폭 ──

        [Test]
        public void 자폭_유닛은_적에_닿으면_반경_1_5_폭발_후_파괴된다()
        {
            var sim = TestHelper.NewSim();
            var boom = TestHelper.Spawn(sim, "boom", Side.Player, 0, 5f);
            var touching = TestHelper.SpawnFrozenEnemy(sim, "blade", 0, 5.5f);
            var edge = TestHelper.SpawnFrozenEnemy(sim, "blade", 0, 6.5f);
            var outside = TestHelper.SpawnFrozenEnemy(sim, "blade", 0, 7f);
            sim.DrainEvents();
            sim.Tick();
            Assert.AreEqual(40f, touching.Hp);
            Assert.AreEqual(40f, edge.Hp);
            Assert.AreEqual(100f, outside.Hp);
            Assert.IsFalse(boom.IsAlive);
            Assert.IsFalse(sim.Units.Contains(boom));
            var events = sim.DrainEvents();
            Assert.AreEqual(1, events.OfType<ExplosionEvent>().Count());
            Assert.AreEqual(1, events.OfType<UnitDiedEvent>().Count(e => e.Unit == boom));
        }

        [Test]
        public void 자폭_유닛은_성에_닿아도_폭발한다()
        {
            var sim = TestHelper.NewSim();
            var boom = TestHelper.Spawn(sim, "boom", Side.Player, 1, 19.5f);
            sim.Tick();
            Assert.AreEqual(sim.EnemyCastle.MaxHp - 60f, sim.EnemyCastle.Hp);
            Assert.IsFalse(boom.IsAlive);
        }

        [Test]
        public void 자폭_유닛은_공중_유닛에게는_닿지_않는다()
        {
            var sim = TestHelper.NewSim();
            var boom = TestHelper.Spawn(sim, "boom", Side.Player, 0, 5f);
            var drone = TestHelper.SpawnFrozenEnemy(sim, "drone", 0, 5.2f);
            sim.Tick();
            Assert.IsTrue(boom.IsAlive);
            Assert.AreEqual(70f, drone.Hp);
        }

        // ── 물량 ──

        [Test]
        public void 트윈봇은_한_묶음이_2기로_나온다()
        {
            var sim = TestHelper.NewSim();
            sim.AddToQueueForTest("twin", 2);
            sim.Execute(new PlaceUnitCommand(0, 0, 5f));
            Assert.AreEqual(2, sim.Units.Count);
            foreach (var unit in sim.Units)
            {
                Assert.AreEqual("twin", unit.Def.Id);
                Assert.AreEqual(50f * 2.2f, unit.MaxHp, 1e-3, "기당 체력에 단계 배율");
                Assert.AreEqual(0, unit.Lane);
            }
            CollectionAssert.AreEqual(new[] { 5f, 4.7f }, sim.Units.Select(u => u.Y).ToArray(), new FloatComparer(1e-4f));
            Assert.AreEqual(0, sim.Queue.Count, "대기 칸은 한 칸만 쓴다");
        }

        private sealed class FloatComparer : System.Collections.IComparer
        {
            private readonly float _tolerance;
            public FloatComparer(float tolerance) { _tolerance = tolerance; }
            public int Compare(object x, object y)
            {
                float a = (float)x, b = (float)y;
                return System.Math.Abs(a - b) <= _tolerance ? 0 : a.CompareTo(b);
            }
        }
    }
}
