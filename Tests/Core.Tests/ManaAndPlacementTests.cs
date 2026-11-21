using System.Linq;
using NUnit.Framework;
using SlotBattle.Core.Battle;

namespace SlotBattle.Core.Tests
{
    /// <summary>설계서 2장 "한 판의 흐름" 1번(마나)과 5번(배치), 고정 간격 진행.</summary>
    public class ManaAndPlacementTests
    {
        [Test]
        public void 시뮬레이션은_초당_20틱이다()
        {
            Assert.AreEqual(20, BattleSimulation.TickRate);
            var sim = TestHelper.NewSim();
            TestHelper.Ticks(sim, 20);
            Assert.AreEqual(1f, sim.ElapsedSeconds, 1e-6);
            Assert.AreEqual(180 * 20, sim.DurationTicks);
        }

        [Test]
        public void 마나는_5에서_시작해_초당_1씩_찬다()
        {
            var sim = TestHelper.NewSim();
            Assert.AreEqual(5f, sim.ManaExact);
            TestHelper.Ticks(sim, 20);
            Assert.AreEqual(6f, sim.ManaExact, 1e-6);
            TestHelper.Ticks(sim, 10);
            Assert.AreEqual(6.5f, sim.ManaExact, 1e-6);
            Assert.AreEqual(6, sim.Mana);
        }

        [Test]
        public void 마나는_최대_10이다()
        {
            var sim = TestHelper.NewSim();
            TestHelper.Ticks(sim, 20 * 30);
            Assert.AreEqual(10f, sim.ManaExact);
            Assert.AreEqual(10, sim.MaxMana);
        }

        [Test]
        public void 마지막_30초는_마나_회복이_2배다()
        {
            var sim = TestHelper.NewSim();
            TestHelper.Ticks(sim, 150 * 20 - 20);
            Assert.IsFalse(sim.IsFinalPhase);
            sim.SetManaForTest(0);
            TestHelper.Ticks(sim, 20); // 149~150초: 평소 속도
            Assert.AreEqual(1f, sim.ManaExact, 1e-6);
            Assert.IsTrue(sim.IsFinalPhase);
            TestHelper.Ticks(sim, 20); // 150~151초: 2배
            Assert.AreEqual(3f, sim.ManaExact, 1e-6);
        }

        [Test]
        public void 대기_칸의_유닛을_원하는_라인과_위치에_놓는다()
        {
            var sim = TestHelper.NewSim();
            sim.AddToQueueForTest("blade", 1);
            sim.AddToQueueForTest("shooter", 2);
            Assert.AreEqual(CommandResult.Ok, sim.Execute(new PlaceUnitCommand(1, 1, 7.5f)));
            Assert.AreEqual(1, sim.Queue.Count);
            Assert.AreEqual("blade", sim.Queue[0].Def.Id);
            var unit = sim.Units.Single();
            Assert.AreEqual("shooter", unit.Def.Id);
            Assert.AreEqual(2, unit.Tier);
            Assert.AreEqual(1, unit.Lane);
            Assert.AreEqual(7.5f, unit.Y);
            Assert.AreEqual(Side.Player, unit.Side);
            Assert.AreEqual(1, sim.DrainEvents().OfType<UnitSummonedEvent>().Count());
        }

        [Test]
        public void 내_진영_밖이나_없는_라인에는_놓을_수_없다()
        {
            var sim = TestHelper.NewSim();
            sim.AddToQueueForTest("blade", 1);
            Assert.AreEqual(CommandResult.InvalidPosition, sim.Execute(new PlaceUnitCommand(0, 0, 10.01f)));
            Assert.AreEqual(CommandResult.InvalidPosition, sim.Execute(new PlaceUnitCommand(0, 0, -0.1f)));
            Assert.AreEqual(CommandResult.InvalidLane, sim.Execute(new PlaceUnitCommand(0, 2, 5f)));
            Assert.AreEqual(CommandResult.InvalidQueueIndex, sim.Execute(new PlaceUnitCommand(1, 0, 5f)));
            Assert.AreEqual(CommandResult.Ok, sim.Execute(new PlaceUnitCommand(0, 0, 10f)), "중앙선까지는 놓을 수 있다");
        }

        [Test]
        public void 놓인_유닛은_자동으로_적_성을_향해_전진한다()
        {
            var sim = TestHelper.NewSim();
            var blade = TestHelper.Spawn(sim, "blade", Side.Player, 0, 2f);
            var dash = TestHelper.Spawn(sim, "dash", Side.Player, 1, 2f);
            var enemy = TestHelper.Spawn(sim, "plasma", Side.Enemy, 0, 19f);
            TestHelper.Ticks(sim, 20);
            Assert.AreEqual(3.0f, blade.Y, 1e-3, "블레이드봇 이동 속도 1.0");
            Assert.AreEqual(3.8f, dash.Y, 1e-3, "대시봇 이동 속도 1.8(앞에 적이 없어 돌격하지 않음)");
            Assert.AreEqual(18.2f, enemy.Y, 1e-3, "적은 반대 방향으로 이동, 플라즈마봇 0.8");
        }
    }
}
