using System.Linq;
using NUnit.Framework;
using SlotBattle.Core.Battle;

namespace SlotBattle.Core.Tests
{
    /// <summary>설계서 2장 사령관 기술: EMP 폭격.</summary>
    public class CommanderSkillTests
    {
        private static float Damage => TestHelper.Data.Battle.CommanderSkill.Damage;

        [Test]
        public void EMP는_지정한_위치_반경_2_안의_적에게_설정된_피해를_준다()
        {
            var sim = TestHelper.NewSim();
            sim.SetManaForTest(10);
            var center = TestHelper.Spawn(sim, "shield", Side.Enemy, 0, 12f);
            var edge = TestHelper.Spawn(sim, "shield", Side.Enemy, 0, 13.9f);
            var outside = TestHelper.Spawn(sim, "shield", Side.Enemy, 0, 14.5f);
            var otherLane = TestHelper.Spawn(sim, "shield", Side.Enemy, 1, 12f);
            var mine = TestHelper.Spawn(sim, "shield", Side.Player, 0, 11f);

            Assert.AreEqual(CommandResult.Ok, sim.Execute(new UseCommanderSkillCommand(0, 12f)));
            Assert.AreEqual(250f - Damage, center.Hp);
            Assert.AreEqual(250f - Damage, edge.Hp);
            Assert.AreEqual(250f, outside.Hp);
            Assert.AreEqual(250f, otherLane.Hp);
            Assert.AreEqual(250f, mine.Hp, "아군은 맞지 않는다");
            Assert.AreEqual(1, sim.DrainEvents().OfType<CommanderSkillUsedEvent>().Count());
        }

        [Test]
        public void EMP는_공중_유닛에게도_맞는다()
        {
            var sim = TestHelper.NewSim();
            var drone = TestHelper.Spawn(sim, "drone", Side.Enemy, 1, 12f, tier: 3);
            sim.Execute(new UseCommanderSkillCommand(1, 12f));
            Assert.AreEqual(drone.MaxHp - Damage, drone.Hp);
        }

        [Test]
        public void EMP를_맞은_적은_2초_동안_움직이지도_공격하지도_않는다()
        {
            var sim = TestHelper.NewSim();
            var enemy = TestHelper.Spawn(sim, "blade", Side.Enemy, 0, 5.5f, tier: 2);
            var mine = TestHelper.Spawn(sim, "shield", Side.Player, 0, 5f);
            mine.StunTicks = 100000; // 내 유닛은 가만히 두어 적의 행동만 본다
            sim.Execute(new UseCommanderSkillCommand(0, 5.5f));
            TestHelper.Ticks(sim, 40);
            Assert.AreEqual(250f, mine.Hp, "기절 중에는 공격하지 않는다");
            Assert.AreEqual(0, enemy.StunTicks);
            sim.Tick();
            Assert.AreEqual(250f - enemy.Attack, mine.Hp, "2초가 지나면 다시 공격한다");

            var sim2 = TestHelper.NewSim();
            var walker = TestHelper.Spawn(sim2, "blade", Side.Enemy, 0, 15f, tier: 2);
            sim2.Execute(new UseCommanderSkillCommand(0, 15f));
            TestHelper.Ticks(sim2, 40);
            Assert.AreEqual(15f, walker.Y, "기절 중에는 움직이지 않는다");
            sim2.Tick();
            Assert.Less(walker.Y, 15f);
        }

        [Test]
        public void EMP는_마나_4를_쓰고_재사용_대기는_30초다()
        {
            var sim = TestHelper.NewSim();
            sim.SetManaForTest(10);
            Assert.IsTrue(sim.IsSkillReady);
            Assert.AreEqual(CommandResult.Ok, sim.Execute(new UseCommanderSkillCommand(0, 10f)));
            Assert.AreEqual(6, sim.Mana);
            Assert.AreEqual(600, sim.SkillCooldownTicks);
            Assert.AreEqual(CommandResult.SkillOnCooldown, sim.Execute(new UseCommanderSkillCommand(0, 10f)));
            TestHelper.Ticks(sim, 599);
            Assert.AreEqual(CommandResult.SkillOnCooldown, sim.Execute(new UseCommanderSkillCommand(0, 10f)));
            sim.Tick();
            Assert.AreEqual(CommandResult.Ok, sim.Execute(new UseCommanderSkillCommand(0, 10f)));
        }

        [Test]
        public void 마나가_모자라면_EMP를_쓸_수_없다()
        {
            var sim = TestHelper.NewSim();
            sim.SetManaForTest(3.9f);
            Assert.IsFalse(sim.IsSkillReady);
            Assert.AreEqual(CommandResult.NotEnoughMana, sim.Execute(new UseCommanderSkillCommand(0, 10f)));
            Assert.AreEqual(0, sim.SkillCooldownTicks);
        }

        [Test]
        public void EMP는_전장_어디든_조준할_수_있지만_라인_밖은_안_된다()
        {
            var sim = TestHelper.NewSim();
            sim.SetManaForTest(10);
            Assert.AreEqual(CommandResult.InvalidLane, sim.Execute(new UseCommanderSkillCommand(2, 10f)));
            Assert.AreEqual(CommandResult.InvalidPosition, sim.Execute(new UseCommanderSkillCommand(0, 20.5f)));
            Assert.AreEqual(CommandResult.Ok, sim.Execute(new UseCommanderSkillCommand(0, 19f)), "적 진영도 조준 가능");
        }

        [Test]
        public void EMP로_쓰러진_적은_바로_빠진다()
        {
            var sim = TestHelper.NewSim();
            var weak = TestHelper.Spawn(sim, "shooter", Side.Enemy, 0, 12f);
            weak.Hp = 30f;
            sim.Execute(new UseCommanderSkillCommand(0, 12f));
            Assert.IsFalse(sim.Units.Contains(weak));
        }
    }
}
