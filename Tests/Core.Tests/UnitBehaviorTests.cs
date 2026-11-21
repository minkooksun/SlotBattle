using System.Linq;
using NUnit.Framework;
using SlotBattle.Core.Battle;

namespace SlotBattle.Core.Tests
{
    /// <summary>설계서 2장 "유닛 행동": 방어진, 도발, 돌격, 원거리 사거리.</summary>
    public class UnitBehaviorTests
    {
        [Test]
        public void 실드봇은_방어진과_도발_대시봇은_돌격_키워드를_가진다()
        {
            var d = TestHelper.Data;
            Assert.IsTrue(d.GetUnit("shield").HasKeyword("hold"));
            Assert.IsTrue(d.GetUnit("shield").HasKeyword("taunt"));
            Assert.IsTrue(d.GetUnit("dash").HasKeyword("charge"));
            Assert.AreEqual(4f, d.Battle.Charge.Range, 1e-6);
            Assert.AreEqual(2f, d.Battle.Charge.SpeedMultiplier, 1e-6);
            Assert.AreEqual(2f, d.Battle.Charge.DamageMultiplier, 1e-6);
        }

        [Test]
        public void 방어진_유닛은_놓은_자리에서_움직이지_않는다()
        {
            var sim = TestHelper.NewSim();
            var shield = TestHelper.Spawn(sim, "shield", Side.Player, 0, 4f);
            TestHelper.Ticks(sim, 100);
            Assert.AreEqual(4f, shield.Y, 1e-6);
        }

        [Test]
        public void 방어진_유닛도_사거리_안의_적은_공격한다()
        {
            var sim = TestHelper.NewSimWithoutCastleFire();
            var shield = TestHelper.Spawn(sim, "shield", Side.Player, 0, 4f);
            var enemy = TestHelper.Spawn(sim, "blade", Side.Enemy, 0, 6f);
            TestHelper.Ticks(sim, 60);
            Assert.AreEqual(4f, shield.Y, 1e-6);
            Assert.Less(enemy.Hp, enemy.MaxHp, "다가온 적을 그 자리에서 친다");
        }

        [Test]
        public void 사거리_안에_도발_유닛이_있으면_더_가까운_적보다_먼저_노린다()
        {
            var sim = TestHelper.NewSimWithoutCastleFire();
            var shooter = TestHelper.Spawn(sim, "shooter", Side.Enemy, 0, 10f);
            shooter.StunTicks = 0;
            var near = TestHelper.Spawn(sim, "blade", Side.Player, 0, 8f);
            var shield = TestHelper.Spawn(sim, "shield", Side.Player, 0, 5f);
            near.StunTicks = 100000;
            Assert.AreSame(shield, sim.FindTarget(shooter), "도발 유닛이 사거리(6) 안에 있다");
            shield.Hp = 0f;
            sim.Tick();
            Assert.AreSame(near, sim.FindTarget(shooter), "도발 유닛이 없으면 가장 가까운 적");
        }

        [Test]
        public void 사거리_밖의_도발_유닛은_무시한다()
        {
            var sim = TestHelper.NewSimWithoutCastleFire();
            var blade = TestHelper.Spawn(sim, "blade", Side.Enemy, 0, 10f);
            var near = TestHelper.Spawn(sim, "twin", Side.Player, 0, 9.5f);
            TestHelper.Spawn(sim, "shield", Side.Player, 0, 6f);
            Assert.AreSame(near.Def, sim.FindTarget(blade).Def);
        }

        [Test]
        public void 돌격_유닛은_앞에_적이_보이면_빨라지고_첫_타격이_두_배다()
        {
            var sim = TestHelper.NewSimWithoutCastleFire();
            var dash = TestHelper.Spawn(sim, "dash", Side.Player, 0, 5f);
            var enemy = TestHelper.SpawnFrozenEnemy(sim, "blade", 0, 8.5f);
            sim.Tick();
            Assert.IsTrue(dash.IsCharging, "앞 4.0 안에 적");
            Assert.AreEqual(5f + 1.8f * 2f / 20f, dash.Y, 1e-4, "속도 2배");
            sim.DrainEvents();
            for (int i = 0; i < 60 && enemy.Hp >= enemy.MaxHp; i++)
            {
                sim.Tick();
            }
            var first = sim.DrainEvents().OfType<UnitHitEvent>().First(e => e.Attacker == dash);
            Assert.AreEqual(25f * 2f, first.Damage, 1e-4, "첫 타격 2배");
            Assert.IsFalse(dash.IsCharging, "타격하면 돌격이 풀린다");
            TestHelper.Ticks(sim, 18);
            var second = sim.DrainEvents().OfType<UnitHitEvent>().First(e => e.Attacker == dash);
            Assert.AreEqual(25f, second.Damage, 1e-4, "두 번째부터는 그대로");
        }

        [Test]
        public void 돌격_유닛은_앞에_적이_없으면_평소_속도다()
        {
            var sim = TestHelper.NewSimWithoutCastleFire();
            var dash = TestHelper.Spawn(sim, "dash", Side.Player, 0, 5f);
            TestHelper.SpawnFrozenEnemy(sim, "blade", 0, 9.5f);
            sim.Tick();
            Assert.IsFalse(dash.IsCharging);
            Assert.AreEqual(5f + 1.8f / 20f, dash.Y, 1e-4);
        }

        [Test]
        public void 원거리_유닛은_사거리_끝에서_멈춰_쏘고_탄이_날아가_맞는다()
        {
            var sim = TestHelper.NewSimWithoutCastleFire();
            var shooter = TestHelper.Spawn(sim, "shooter", Side.Player, 0, 3f);
            var enemy = TestHelper.SpawnFrozenEnemy(sim, "blade", 0, 9f);
            sim.Tick();
            Assert.AreEqual(3f, shooter.Y, 1e-6, "사거리 6 안이라 멈춘다");
            Assert.AreEqual(1, sim.Projectiles.Count, "쏜다");
            TestHelper.Ticks(sim, 12);
            Assert.AreEqual(1, sim.Projectiles.Count, "거리 6을 초당 9로 0.67초 동안 날아간다");
            TestHelper.Ticks(sim, 3);
            Assert.Less(enemy.Hp, enemy.MaxHp, "도착해서 맞는다");
        }
    }
}
