using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SlotBattle.Core.Battle;
using SlotBattle.Core.Data;

namespace SlotBattle.Core.Tests
{
    /// <summary>설계서 2장 "공격 방식과 탄".</summary>
    public class ProjectileTests
    {
        private static int FlightTicks(float distance, float speed)
        {
            // 쏜 다음 틱부터 움직이고, 남은 거리가 한 틱 이동 거리 이하가 되는 틱에 도착한다.
            float step = speed / BattleSimulation.TickRate;
            return (int)Math.Ceiling(distance / step - 1e-4);
        }

        // ── 공격 방식 데이터 ──

        [TestCase("blade", AttackType.Melee)]
        [TestCase("shooter", AttackType.Bolt)]
        [TestCase("shield", AttackType.Melee)]
        [TestCase("swarm", AttackType.Melee)]
        [TestCase("boom", AttackType.Explode)]
        [TestCase("plasma", AttackType.Lob)]
        [TestCase("dash", AttackType.Charge)]
        [TestCase("drone", AttackType.Bolt)]
        public void 유닛마다_공격_방식이_있다(string id, AttackType type)
        {
            Assert.AreEqual(type, TestHelper.Data.GetUnit(id).AttackType);
        }

        [Test]
        public void 탄을_쏘는_유닛은_탄_속도를_가진다()
        {
            foreach (var unit in TestHelper.Data.Units.Where(u => u.FiresProjectile))
            {
                Assert.Greater(unit.ProjectileSpeed, 0f, unit.Id);
            }
            Assert.Greater(TestHelper.Data.Battle.CastleDefense.ProjectileSpeed, 0f);
        }

        [Test]
        public void 근접_계열은_근접_사거리를_쓴다()
        {
            foreach (var id in new[] { "blade", "boom", "dash" })
            {
                var sim = TestHelper.NewSim();
                Assert.AreEqual(sim.Config.MeleeRange, TestHelper.Spawn(sim, id, Side.Player, 0, 1f).Range, id);
            }
        }

        [TestCase("bolt", "\"range\": 4", "projectileSpeed")]
        [TestCase("lob", "\"range\": 4, \"projectileSpeed\": 5", "splashRadius")]
        [TestCase("explode", "", "splashRadius")]
        public void 공격_방식에_필요한_값이_없으면_데이터를_거부한다(string type, string extra, string missing)
        {
            string unit = "{ \"id\": \"x\", \"rarity\": \"common\", \"hp\": 10, \"attack\": 1, \"attackInterval\": 1, \"moveSpeed\": 1, \"attackType\": \"" + type + "\"" + (extra.Length > 0 ? ", " + extra : "") + " }";
            var units = Newtonsoft.Json.JsonConvert.DeserializeObject<List<UnitDef>>("[" + unit + "]");
            var all = TestHelper.Data.Units.Concat(units).ToList();
            var ex = Assert.Throws<InvalidOperationException>(() => new GameData(all, TestHelper.Data.Slot, TestHelper.Data.Battle, TestHelper.Data.Stages));
            StringAssert.Contains(missing, ex.Message);
        }

        [Test]
        public void 공격_방식이_없으면_데이터를_거부한다()
        {
            var unit = new UnitDef { Id = "x", Rarity = "common", Hp = 10, Attack = 1, AttackInterval = 1, MoveSpeed = 1 };
            var all = TestHelper.Data.Units.Concat(new[] { unit }).ToList();
            Assert.Throws<InvalidOperationException>(() => new GameData(all, TestHelper.Data.Slot, TestHelper.Data.Battle, TestHelper.Data.Stages));
        }

        // ── 직사탄 ──

        [Test]
        public void 직사탄은_쏠_때가_아니라_도착할_때_피해를_준다()
        {
            var sim = TestHelper.NewSim();
            var shooter = TestHelper.Spawn(sim, "shooter", Side.Player, 0, 5f);
            var target = TestHelper.SpawnFrozenEnemy(sim, "shield", 0, 8f);
            int flight = FlightTicks(3f, shooter.Def.ProjectileSpeed);

            sim.Tick();
            Assert.AreEqual(250f, target.Hp, "쏜 틱에는 아직 맞지 않는다");
            Assert.AreEqual(1, sim.Projectiles.Count);
            TestHelper.Ticks(sim, flight - 1);
            Assert.AreEqual(250f, target.Hp, "날아가는 중");
            sim.Tick();
            Assert.AreEqual(235f, target.Hp, "도착한 틱에 피해");
            Assert.AreEqual(0, sim.Projectiles.Count);
        }

        [Test]
        public void 직사탄은_틱마다_탄_속도만큼_움직인다()
        {
            var sim = TestHelper.NewSim();
            var shooter = TestHelper.Spawn(sim, "shooter", Side.Player, 0, 5f);
            TestHelper.SpawnFrozenEnemy(sim, "shield", 0, 8f);
            sim.Tick();
            var p = sim.Projectiles.Single();
            Assert.AreEqual(5f, p.Y, 1e-5, "쏜 틱에는 쏜 자리에 있다");
            sim.Tick();
            Assert.AreEqual(5f + shooter.Def.ProjectileSpeed / BattleSimulation.TickRate, p.Y, 1e-4);
        }

        [Test]
        public void 직사탄은_움직이는_대상을_따라가_반드시_맞는다()
        {
            var sim = TestHelper.NewSimWithoutCastleFire();
            TestHelper.Spawn(sim, "shooter", Side.Player, 0, 5f);
            var runner = TestHelper.Spawn(sim, "dash", Side.Enemy, 0, 9f);
            sim.Tick();
            Assert.AreEqual(1, sim.Projectiles.Count);
            float launchY = runner.Y;
            TestHelper.Ticks(sim, 10);
            Assert.AreNotEqual(launchY, runner.Y, "대상은 그사이 움직였다");
            Assert.AreEqual(runner.MaxHp - 15f, runner.Hp, "그래도 맞는다");
        }

        [Test]
        public void 대상이_먼저_죽으면_직사탄은_사라진다()
        {
            var sim = TestHelper.NewSim();
            TestHelper.Spawn(sim, "shooter", Side.Player, 0, 5f);
            var target = TestHelper.SpawnFrozenEnemy(sim, "blade", 0, 8.5f);
            var behind = TestHelper.SpawnFrozenEnemy(sim, "blade", 0, 8.6f);
            sim.Tick();
            var p = sim.Projectiles.Single();
            target.Hp = 0f; // 탄이 닿기 전에 다른 이유로 쓰러졌다고 본다
            sim.DrainEvents();
            sim.Tick();
            var arrived = sim.DrainEvents().OfType<ProjectileArrivedEvent>().Single();
            Assert.AreSame(p, arrived.Projectile);
            Assert.IsFalse(arrived.Hit);
            Assert.AreEqual(0, sim.Projectiles.Count);
            TestHelper.Ticks(sim, 10);
            Assert.AreEqual(100f, behind.Hp, "다른 유닛에게 옮겨 가지 않는다");
        }

        [Test]
        public void 쏜_유닛이_죽어도_탄은_날아가_맞는다()
        {
            var sim = TestHelper.NewSim();
            var shooter = TestHelper.Spawn(sim, "shooter", Side.Player, 0, 5f);
            var target = TestHelper.SpawnFrozenEnemy(sim, "shield", 0, 8f);
            sim.Tick();
            shooter.Hp = 0f;
            TestHelper.TickAndLand(sim);
            Assert.AreEqual(235f, target.Hp);
        }

        [Test]
        public void 피해량은_쏘는_순간의_공격력이다()
        {
            var sim = TestHelper.NewSim();
            TestHelper.Spawn(sim, "shooter", Side.Player, 0, 5f, tier: 2);
            var target = TestHelper.SpawnFrozenEnemy(sim, "shield", 0, 8f, tier: 3);
            sim.Tick();
            Assert.AreEqual(15f * 2.2f, sim.Projectiles.Single().Damage, 1e-3);
        }

        [Test]
        public void 성을_노린_직사탄은_도착할_때_성에_피해를_준다()
        {
            var sim = TestHelper.NewSim();
            TestHelper.Spawn(sim, "shooter", Side.Player, 0, 16.5f);
            sim.Tick();
            Assert.AreEqual(sim.EnemyCastle.MaxHp, sim.EnemyCastle.Hp);
            TestHelper.TickAndLand(sim);
            Assert.AreEqual(sim.EnemyCastle.MaxHp - 15f, sim.EnemyCastle.Hp);
        }

        // ── 곡사탄 ──

        [Test]
        public void 곡사탄은_쏠_때의_대상_위치에_떨어진다()
        {
            var sim = TestHelper.NewSimWithoutCastleFire();
            var plasma = TestHelper.Spawn(sim, "plasma", Side.Player, 0, 5f);
            var runner = TestHelper.Spawn(sim, "dash", Side.Enemy, 0, 8.4f);
            sim.Tick();
            var p = sim.Projectiles.Single();
            float aimed = p.TargetY;
            Assert.AreEqual(8.4f, aimed, 1e-4, "쏜 틱의 대상 위치(대상이 움직이기 전)");
            TestHelper.Ticks(sim, 3);
            Assert.AreEqual(aimed, p.TargetY, "날아가는 동안 떨어질 자리는 바뀌지 않는다");
        }

        [Test]
        public void 곡사탄은_빗나갈_수_있다()
        {
            // 쏜 뒤 대상이 폭발 반경 밖으로 벗어나면 맞지 않는다.
            var sim = TestHelper.NewSimWithoutCastleFire();
            TestHelper.Spawn(sim, "plasma", Side.Player, 0, 5f);
            var target = TestHelper.SpawnFrozenEnemy(sim, "shield", 0, 8f);
            sim.Tick();
            target.Y = 9.5f; // 반경 1.0 밖으로 옮긴다
            TestHelper.TickAndLand(sim);
            Assert.AreEqual(250f, target.Hp);
        }

        [Test]
        public void 곡사탄은_떨어진_자리의_다른_적도_맞힌다()
        {
            var sim = TestHelper.NewSimWithoutCastleFire();
            TestHelper.Spawn(sim, "plasma", Side.Player, 0, 5f);
            var target = TestHelper.SpawnFrozenEnemy(sim, "shield", 0, 8f);
            sim.Tick();
            target.Y = 9.5f;
            var newcomer = TestHelper.SpawnFrozenEnemy(sim, "shield", 0, 8.3f);
            TestHelper.TickAndLand(sim);
            Assert.AreEqual(220f, newcomer.Hp);
        }

        [Test]
        public void 곡사탄은_도착할_때_폭발_사건을_알린다()
        {
            var sim = TestHelper.NewSimWithoutCastleFire();
            var plasma = TestHelper.Spawn(sim, "plasma", Side.Player, 0, 5f);
            TestHelper.SpawnFrozenEnemy(sim, "shield", 0, 8f);
            sim.Tick();
            Assert.AreEqual(0, sim.DrainEvents().OfType<ExplosionEvent>().Count(), "쏜 틱에는 폭발하지 않는다");
            TestHelper.TickAndLand(sim);
            var boom = sim.DrainEvents().OfType<ExplosionEvent>().Single();
            Assert.AreEqual(8f, boom.Y, 1e-4);
            Assert.AreEqual(plasma.Def.SplashRadius, boom.Radius);
            Assert.AreSame(plasma, boom.Source);
        }

        [Test]
        public void 곡사탄은_성을_노렸을_때만_성에_피해를_준다()
        {
            var sim = TestHelper.NewSimWithoutCastleFire();
            TestHelper.Spawn(sim, "plasma", Side.Player, 0, 16.6f);
            TestHelper.TickAndLand(sim);
            Assert.AreEqual(sim.EnemyCastle.MaxHp - 30f, sim.EnemyCastle.Hp, "성을 노린 탄");

            var sim2 = TestHelper.NewSimWithoutCastleFire();
            TestHelper.Spawn(sim2, "plasma", Side.Player, 0, 15.8f);
            TestHelper.SpawnFrozenEnemy(sim2, "shield", 0, 19.2f);
            TestHelper.TickAndLand(sim2);
            Assert.AreEqual(sim2.EnemyCastle.MaxHp, sim2.EnemyCastle.Hp, "유닛을 노린 탄은 성 가까이 떨어져도 성은 맞지 않는다");
        }

        // ── 사건 ──

        [Test]
        public void 원거리_공격의_사건은_공격_시작_발사_도착_타격_순서다()
        {
            var sim = TestHelper.NewSim();
            var shooter = TestHelper.Spawn(sim, "shooter", Side.Player, 0, 5f);
            var target = TestHelper.SpawnFrozenEnemy(sim, "shield", 0, 8f);
            sim.DrainEvents();
            TestHelper.TickAndLand(sim);
            var events = sim.DrainEvents().Where(e =>
                e is AttackStartedEvent || e is ProjectileLaunchedEvent || e is ProjectileArrivedEvent || e is UnitHitEvent).ToList();
            Assert.IsInstanceOf<AttackStartedEvent>(events[0]);
            Assert.IsInstanceOf<ProjectileLaunchedEvent>(events[1]);
            Assert.IsInstanceOf<ProjectileArrivedEvent>(events[2]);
            Assert.IsInstanceOf<UnitHitEvent>(events[3]);

            var started = (AttackStartedEvent)events[0];
            Assert.AreSame(shooter, started.Attacker);
            Assert.AreSame(target, started.TargetUnit);
            Assert.AreEqual(AttackType.Bolt, started.Type);
            Assert.AreEqual(started.Tick, events[1].Tick, "공격 시작과 발사는 같은 틱");
            Assert.Greater(events[2].Tick, events[1].Tick);
            Assert.AreEqual(events[2].Tick, events[3].Tick, "도착과 타격은 같은 틱");
            Assert.AreSame(shooter, ((UnitHitEvent)events[3]).Attacker);
        }

        [TestCase("blade", AttackType.Melee)]
        [TestCase("dash", AttackType.Charge)]
        public void 근접과_돌진은_공격_시작과_같은_틱에_피해를_준다(string id, AttackType type)
        {
            var sim = TestHelper.NewSim();
            var unit = TestHelper.Spawn(sim, id, Side.Player, 0, 5f);
            var target = TestHelper.SpawnFrozenEnemy(sim, "shield", 0, 5.5f);
            sim.DrainEvents();
            sim.Tick();
            var events = sim.DrainEvents();
            var started = events.OfType<AttackStartedEvent>().Single();
            Assert.AreEqual(type, started.Type);
            Assert.AreSame(target, started.TargetUnit);
            Assert.AreEqual(250f - unit.Attack, target.Hp);
            Assert.AreEqual(0, events.OfType<ProjectileLaunchedEvent>().Count());
        }

        [Test]
        public void 자폭은_공격_시작과_폭발을_같은_틱에_알린다()
        {
            var sim = TestHelper.NewSim();
            var boom = TestHelper.Spawn(sim, "boom", Side.Player, 0, 5f);
            TestHelper.SpawnFrozenEnemy(sim, "shield", 0, 5.5f);
            sim.DrainEvents();
            sim.Tick();
            var events = sim.DrainEvents();
            Assert.AreEqual(AttackType.Explode, events.OfType<AttackStartedEvent>().Single().Type);
            var explosion = events.OfType<ExplosionEvent>().Single();
            Assert.AreEqual(5f, explosion.Y, 1e-5);
            Assert.AreEqual(boom.Def.SplashRadius, explosion.Radius);
        }

        // ── 결정성 ──

        [Test]
        public void 탄이_날고_있는_상태까지_같은_시드면_같다()
        {
            ulong Run()
            {
                var sim = TestHelper.NewSim(seed: 7);
                TestHelper.Spawn(sim, "shooter", Side.Player, 0, 5f);
                TestHelper.Spawn(sim, "plasma", Side.Player, 1, 4f);
                TestHelper.Spawn(sim, "dash", Side.Enemy, 0, 12f);
                TestHelper.Spawn(sim, "shield", Side.Enemy, 1, 9f);
                var hashes = new List<ulong>();
                for (int i = 0; i < 200; i++)
                {
                    sim.Tick();
                    hashes.Add(sim.ComputeStateHash());
                }
                ulong h = 0;
                foreach (var x in hashes)
                {
                    h = h * 31 + x;
                }
                return h;
            }
            Assert.AreEqual(Run(), Run());
        }
    }
}
