using System.Linq;
using NUnit.Framework;
using SlotBattle.Core.Battle;

namespace SlotBattle.Core.Tests
{
    /// <summary>설계서 2장 전투 규칙: 성은 사거리 안의 적 유닛을 약하게 포격한다. 수치는 battle.json(3장).</summary>
    public class CastleDefenseTests
    {
        private static float Damage => TestHelper.Data.Battle.CastleDefense.Damage;
        private static float Range => TestHelper.Data.Battle.CastleDefense.Range;

        [Test]
        public void 성은_사거리_안의_가장_가까운_적_하나를_포격한다()
        {
            var sim = TestHelper.NewSim();
            var near = TestHelper.SpawnFrozenEnemy(sim, "shield", 1, 2.0f, tier: 3);
            var far = TestHelper.SpawnFrozenEnemy(sim, "shield", 0, 3.0f, tier: 3);
            sim.Tick();
            Assert.AreEqual(near.MaxHp - Damage, near.Hp, "두 라인 중 가장 가까운 적");
            Assert.AreEqual(far.MaxHp, far.Hp, "한 번에 하나만");
        }

        [Test]
        public void 사거리_밖의_적은_포격하지_않는다()
        {
            var sim = TestHelper.NewSim();
            var outside = TestHelper.SpawnFrozenEnemy(sim, "shield", 0, Range + 0.05f, tier: 3);
            var edge = TestHelper.SpawnFrozenEnemy(sim, "shield", 1, Range, tier: 3);
            sim.Tick();
            Assert.AreEqual(outside.MaxHp, outside.Hp);
            Assert.AreEqual(edge.MaxHp - Damage, edge.Hp, "사거리 끝은 포함");
        }

        [Test]
        public void 성_포격은_공중_유닛도_맞힌다()
        {
            var sim = TestHelper.NewSim();
            var drone = TestHelper.SpawnFrozenEnemy(sim, "drone", 0, 2.0f, tier: 3);
            sim.Tick();
            Assert.AreEqual(drone.MaxHp - Damage, drone.Hp);
        }

        [Test]
        public void 성_포격은_공격_간격마다_한_번이다()
        {
            var sim = TestHelper.NewSim();
            var target = TestHelper.SpawnFrozenEnemy(sim, "shield", 0, 2.0f, tier: 3);
            int interval = (int)System.Math.Round(TestHelper.Data.Battle.CastleDefense.AttackInterval * BattleSimulation.TickRate);
            TestHelper.Ticks(sim, interval);
            Assert.AreEqual(target.MaxHp - Damage, target.Hp, "첫 간격 동안 1회");
            sim.Tick();
            Assert.AreEqual(target.MaxHp - 2 * Damage, target.Hp, "간격이 지나면 다음 포격");
        }

        [Test]
        public void 적_성도_내_유닛을_포격한다()
        {
            var sim = TestHelper.NewSim();
            var mine = TestHelper.Spawn(sim, "shield", Side.Player, 0, 17.5f, tier: 3);
            mine.StunTicks = 100000;
            sim.Tick();
            Assert.AreEqual(mine.MaxHp - Damage, mine.Hp);
        }

        [Test]
        public void 성은_자기_편_유닛을_포격하지_않는다()
        {
            var sim = TestHelper.NewSim();
            var mine = TestHelper.Spawn(sim, "shield", Side.Player, 0, 1.0f);
            mine.StunTicks = 100000;
            sim.Tick();
            Assert.AreEqual(mine.MaxHp, mine.Hp);
        }

        [Test]
        public void 거리가_같으면_먼저_나온_유닛을_포격한다()
        {
            var sim = TestHelper.NewSim();
            var first = TestHelper.SpawnFrozenEnemy(sim, "shield", 1, 2.0f, tier: 3);
            var second = TestHelper.SpawnFrozenEnemy(sim, "shield", 0, 2.0f, tier: 3);
            sim.Tick();
            Assert.AreEqual(first.MaxHp - Damage, first.Hp);
            Assert.AreEqual(second.MaxHp, second.Hp);
        }

        [Test]
        public void 성_포격은_단계_배율과_무관하게_같은_피해다()
        {
            var sim = TestHelper.NewSim();
            var tier1 = TestHelper.SpawnFrozenEnemy(sim, "shield", 0, 2.0f, tier: 1);
            sim.Tick();
            Assert.AreEqual(tier1.MaxHp - Damage, tier1.Hp);
        }

        [Test]
        public void 성_포격은_사건으로_알린다()
        {
            var sim = TestHelper.NewSim();
            var target = TestHelper.SpawnFrozenEnemy(sim, "shield", 0, 2.0f, tier: 3);
            sim.DrainEvents();
            sim.Tick();
            var hit = sim.DrainEvents().OfType<UnitHitEvent>().Single();
            Assert.AreSame(sim.PlayerCastle, hit.SourceCastle);
            Assert.IsNull(hit.Attacker);
            Assert.AreSame(target, hit.Target);
            Assert.AreEqual(Damage, hit.Damage);
        }

        [Test]
        public void 성_포격과_유닛_공격은_같은_틱에_동시에_처리된다()
        {
            // 포격으로 쓰러질 유닛도 그 틱의 공격은 성에 들어간다.
            var sim = TestHelper.NewSim();
            var blade = TestHelper.Spawn(sim, "blade", Side.Enemy, 0, 0.5f);
            sim.Tick();
            Assert.IsFalse(blade.IsAlive);
            Assert.AreEqual(sim.PlayerCastle.MaxHp - 20f, sim.PlayerCastle.Hp);
        }

        [Test]
        public void 원거리_유닛은_성_포격_사거리_밖에서_성을_공격할_수_있다()
        {
            var shooter = TestHelper.Data.GetUnit("shooter");
            var plasma = TestHelper.Data.GetUnit("plasma");
            Assert.Greater(shooter.Range, Range);
            Assert.Greater(plasma.Range, Range);

            var sim = TestHelper.NewSim();
            var unit = TestHelper.Spawn(sim, "shooter", Side.Player, 0, 10f);
            TestHelper.Ticks(sim, 20 * 30);
            Assert.AreEqual(20f - shooter.Range, unit.Y, 0.06f, "사거리 끝에서 멈춘다");
            Assert.AreEqual(unit.MaxHp, unit.Hp, "포격을 받지 않는다");
            Assert.Less(sim.EnemyCastle.Hp, sim.EnemyCastle.MaxHp);
        }

        [Test]
        public void 시간_초과가_아니라_성이_무너져_끝나면_EndedByTimeout은_거짓이다()
        {
            var sim = TestHelper.NewSim();
            sim.EnemyCastle.Hp = 10f;
            TestHelper.Spawn(sim, "shooter", Side.Player, 0, 16.5f);
            sim.Tick();
            Assert.AreEqual(BattleOutcome.Win, sim.Outcome);
            Assert.IsFalse(sim.EndedByTimeout);
        }
    }
}
