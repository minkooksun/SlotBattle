using System.Linq;
using NUnit.Framework;
using SlotBattle.Core.Battle;

namespace SlotBattle.Core.Tests
{
    /// <summary>설계서 2장 "전투 규칙"과 3장 단계 배율.</summary>
    public class CombatTests
    {
        [Test]
        public void 유닛은_다른_라인의_적을_공격하지_않는다()
        {
            var sim = TestHelper.NewSim();
            var mine = TestHelper.Spawn(sim, "shooter", Side.Player, 0, 5f);
            var other = TestHelper.Spawn(sim, "blade", Side.Enemy, 1, 6f);
            TestHelper.Ticks(sim, 5);
            Assert.AreEqual(100f, other.Hp);
            Assert.AreEqual(60f, mine.Hp);
            Assert.Greater(mine.Y, 5f, "공격 대상이 없으니 계속 전진한다");
        }

        [Test]
        public void 사거리_안의_가장_가까운_적을_공격한다()
        {
            var sim = TestHelper.NewSim();
            var shooter = TestHelper.Spawn(sim, "shooter", Side.Player, 0, 5f);
            var far = TestHelper.SpawnFrozenEnemy(sim, "blade", 0, 8.5f);
            var near = TestHelper.SpawnFrozenEnemy(sim, "blade", 0, 7f);
            TestHelper.TickAndLand(sim);
            Assert.AreEqual(85f, near.Hp);
            Assert.AreEqual(100f, far.Hp);
            Assert.AreEqual(5f, shooter.Y, "사거리 안에 적이 있으면 멈춰서 공격한다");
        }

        [Test]
        public void 사거리_밖의_적은_공격하지_않고_다가간다()
        {
            var sim = TestHelper.NewSim();
            var shooter = TestHelper.Spawn(sim, "shooter", Side.Player, 0, 5f);
            var enemy = TestHelper.SpawnFrozenEnemy(sim, "blade", 0, 11.5f);
            sim.Tick();
            Assert.AreEqual(100f, enemy.Hp);
            Assert.AreEqual(5.05f, shooter.Y, 1e-4);
        }

        [Test]
        public void 근접_유닛은_서로_만나면_멈춰서_싸운다()
        {
            var sim = TestHelper.NewSim();
            var mine = TestHelper.Spawn(sim, "blade", Side.Player, 0, 5f);
            var enemy = TestHelper.Spawn(sim, "blade", Side.Enemy, 0, 7f);
            TestHelper.Ticks(sim, 40);
            float gap = enemy.Y - mine.Y;
            Assert.LessOrEqual(gap, sim.Config.MeleeRange + 1e-4f);
            Assert.Greater(gap, 0f);
            Assert.Less(mine.Hp, 100f);
            Assert.Less(enemy.Hp, 100f);
        }

        [Test]
        public void 적_유닛이_없고_성이_사거리_안이면_성을_공격한다()
        {
            var sim = TestHelper.NewSim();
            TestHelper.Spawn(sim, "shooter", Side.Player, 0, 16.5f);
            TestHelper.TickAndLand(sim);
            Assert.AreEqual(sim.EnemyCastle.MaxHp - 15f, sim.EnemyCastle.Hp);
            Assert.AreEqual(1, sim.DrainEvents().OfType<CastleDamagedEvent>().Count());
        }

        [Test]
        public void 사거리_안에_적_유닛이_있으면_성보다_유닛을_먼저_공격한다()
        {
            var sim = TestHelper.NewSimWithoutCastleFire();
            TestHelper.Spawn(sim, "shooter", Side.Player, 0, 16.5f);
            var enemy = TestHelper.SpawnFrozenEnemy(sim, "shield", 0, 18f);
            TestHelper.TickAndLand(sim);
            Assert.AreEqual(sim.EnemyCastle.MaxHp, sim.EnemyCastle.Hp);
            Assert.AreEqual(250f - 15f * 1.5f, enemy.Hp, 1e-4, "슈터봇은 실드봇에 강하다(상성 피해)");
        }

        [Test]
        public void 적_유닛도_내_성을_공격한다()
        {
            var sim = TestHelper.NewSim();
            TestHelper.Spawn(sim, "blade", Side.Enemy, 1, 0.5f);
            sim.Tick();
            Assert.AreEqual(sim.PlayerCastle.MaxHp - 20f, sim.PlayerCastle.Hp);
        }

        [Test]
        public void 공격은_공격_간격마다_한_번이다()
        {
            var sim = TestHelper.NewSimWithoutCastleFire();
            float max = sim.EnemyCastle.MaxHp;
            TestHelper.Spawn(sim, "blade", Side.Player, 0, 19.5f);
            TestHelper.Ticks(sim, 20);
            Assert.AreEqual(max - 20f, sim.EnemyCastle.Hp, "1.0초 동안 1회");
            sim.Tick();
            Assert.AreEqual(max - 40f, sim.EnemyCastle.Hp, "1.0초가 지나면 다음 공격");
        }

        [Test]
        public void 쓰러진_유닛은_전장에서_빠지고_사망_사건이_나온다()
        {
            var sim = TestHelper.NewSim();
            TestHelper.Spawn(sim, "blade", Side.Player, 0, 5f, tier: 3);
            var enemy = TestHelper.SpawnFrozenEnemy(sim, "twin", 0, 5.5f);
            sim.DrainEvents();
            sim.Tick();
            Assert.IsFalse(enemy.IsAlive);
            Assert.IsFalse(sim.Units.Contains(enemy));
            Assert.AreEqual(1, sim.DrainEvents().OfType<UnitDiedEvent>().Count(e => e.Unit == enemy));
        }

        [Test]
        public void 같은_틱의_공격은_동시에_처리된다()
        {
            // 서로 한 방에 쓰러뜨리는 두 유닛은 먼저 만든 쪽이 이기지 않고 함께 쓰러진다.
            var sim = TestHelper.NewSim();
            var mine = TestHelper.Spawn(sim, "shooter", Side.Player, 0, 5f);
            var enemy = TestHelper.Spawn(sim, "shooter", Side.Enemy, 0, 8f);
            mine.Hp = 10f;
            enemy.Hp = 10f;
            TestHelper.TickAndLand(sim);
            Assert.IsFalse(mine.IsAlive);
            Assert.IsFalse(enemy.IsAlive);
        }

        [TestCase(1, 100f, 20f)]
        [TestCase(2, 220f, 44f)]
        [TestCase(3, 500f, 100f)]
        public void 단계별_체력과_공격력은_1배_2_2배_5배(int tier, float hp, float attack)
        {
            var sim = TestHelper.NewSim();
            var unit = TestHelper.Spawn(sim, "blade", Side.Player, 0, 1f, tier);
            Assert.AreEqual(hp, unit.MaxHp, 1e-3);
            Assert.AreEqual(attack, unit.Attack, 1e-3);
        }

        [Test]
        public void 근접_사거리는_battle_json의_meleeRange다()
        {
            var sim = TestHelper.NewSim();
            var unit = TestHelper.Spawn(sim, "blade", Side.Player, 0, 1f);
            Assert.AreEqual(sim.Config.MeleeRange, unit.Range);
        }
    }
}
