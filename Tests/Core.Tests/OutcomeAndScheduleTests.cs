using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SlotBattle.Core.Battle;
using SlotBattle.Core.Data;

namespace SlotBattle.Core.Tests
{
    /// <summary>설계서 2장 "승패", 성 체력, 적 시간표.</summary>
    public class OutcomeAndScheduleTests
    {
        [Test]
        public void 성_체력은_양쪽_모두_battle_json의_값에서_시작한다()
        {
            var sim = TestHelper.NewSim();
            Assert.AreEqual(TestHelper.Data.Battle.CastleHp, sim.PlayerCastle.Hp);
            Assert.AreEqual(sim.PlayerCastle.Hp, sim.EnemyCastle.Hp, "양쪽 같다");
        }

        [Test]
        public void 스테이지가_적_성_체력을_정하면_그_값을_쓴다()
        {
            var sim = TestHelper.NewSim(TestHelper.EmptyStage(enemyCastleHp: 1500f));
            Assert.AreEqual(1500f, sim.EnemyCastle.MaxHp);
            Assert.AreEqual(TestHelper.Data.Battle.CastleHp, sim.PlayerCastle.MaxHp, "내 성은 그대로");
        }

        [Test]
        public void 적_성이_0이_되면_승리()
        {
            var sim = TestHelper.NewSim();
            sim.EnemyCastle.Hp = 10f;
            TestHelper.Spawn(sim, "shooter", Side.Player, 0, 17f);
            TestHelper.TickAndLand(sim);
            Assert.IsTrue(sim.IsOver);
            Assert.AreEqual(BattleOutcome.Win, sim.Outcome);
            Assert.AreEqual(1, sim.DrainEvents().OfType<BattleEndedEvent>().Count());
        }

        [Test]
        public void 내_성이_0이_되면_패배()
        {
            var sim = TestHelper.NewSim();
            sim.PlayerCastle.Hp = 10f;
            TestHelper.Spawn(sim, "blade", Side.Enemy, 1, 0.5f);
            sim.Tick();
            Assert.AreEqual(BattleOutcome.Lose, sim.Outcome);
        }

        [Test]
        public void 끝난_판은_더_진행하지_않고_명령도_받지_않는다()
        {
            var sim = TestHelper.NewSim();
            sim.EnemyCastle.Hp = 10f;
            TestHelper.Spawn(sim, "shooter", Side.Player, 0, 17f);
            TestHelper.TickAndLand(sim);
            int tick = sim.CurrentTick;
            sim.Tick();
            Assert.AreEqual(tick, sim.CurrentTick);
            Assert.AreEqual(CommandResult.BattleOver, sim.Execute(new SpinCommand()));
        }

        [Test]
        public void 삼분이_지나면_성_체력_비율이_높은_쪽이_이긴다()
        {
            // 체력 값이 아니라 비율을 비교한다: 내 성 60% > 적 성 50%(2,000/4,000)
            var win = TestHelper.NewSim(TestHelper.EmptyStage(enemyCastleHp: 4000f));
            win.PlayerCastle.Hp = win.PlayerCastle.MaxHp * 0.6f;
            win.EnemyCastle.Hp = 2000f;
            Assert.Greater(win.EnemyCastle.Hp, 0f);
            TestHelper.Ticks(win, 180 * 20 - 1);
            Assert.IsFalse(win.IsOver, "3분 직전에는 아직 진행 중");
            win.Tick();
            Assert.AreEqual(BattleOutcome.Win, win.Outcome);
            Assert.IsTrue(win.EndedByTimeout);

            var lose = TestHelper.NewSim();
            lose.PlayerCastle.Hp = lose.PlayerCastle.MaxHp * 0.4f;
            TestHelper.Ticks(lose, 180 * 20);
            Assert.AreEqual(BattleOutcome.Lose, lose.Outcome);
            Assert.IsTrue(lose.EndedByTimeout);
        }

        [Test]
        public void 삼분이_지나고_비율이_같으면_무승부()
        {
            var sim = TestHelper.NewSim();
            TestHelper.Ticks(sim, 180 * 20);
            Assert.AreEqual(BattleOutcome.Draw, sim.Outcome);
            Assert.AreEqual(180f, sim.ElapsedSeconds, 1e-4);
        }

        [Test]
        public void 같은_틱에_두_성이_모두_무너지면_무승부()
        {
            var sim = TestHelper.NewSim();
            sim.PlayerCastle.Hp = 10f;
            sim.EnemyCastle.Hp = 10f;
            TestHelper.Spawn(sim, "shooter", Side.Player, 0, 17f);
            TestHelper.Spawn(sim, "shooter", Side.Enemy, 1, 3f);
            TestHelper.TickAndLand(sim);
            Assert.AreEqual(BattleOutcome.Draw, sim.Outcome);
        }

        // ── 적 시간표 ──

        private static StageDef ScheduledStage()
        {
            return new StageDef
            {
                Id = 98,
                Name = "schedule",
                Spawns = new List<SpawnDef>
                {
                    new SpawnDef { Time = 1f, Lane = 1, Unit = "shield", Tier = 2, RepeatEvery = 2f, RepeatUntil = 5f },
                    new SpawnDef { Time = 2f, Lane = 0, Unit = "shooter" },
                },
            };
        }

        [Test]
        public void 적은_시간표대로_정한_라인에_정한_유닛과_단계를_내보낸다()
        {
            var sim = TestHelper.NewSim(ScheduledStage());
            TestHelper.Ticks(sim, 20);
            Assert.AreEqual(0, sim.Units.Count, "1초 전에는 아무도 없다");
            sim.Tick();
            var first = sim.Units.Single();
            Assert.AreEqual(Side.Enemy, first.Side);
            Assert.AreEqual("shield", first.Def.Id);
            Assert.AreEqual(2, first.Tier);
            Assert.AreEqual(1, first.Lane);
            Assert.AreEqual(250f * 2.2f, first.MaxHp, 1e-3);
            Assert.AreEqual(20f - sim.Config.EnemySpawnDistance, first.Y, 1e-4, "적 성 앞에서 나온다(실드봇은 방어진이라 움직이지 않음)");

            TestHelper.Ticks(sim, 20);
            Assert.AreEqual(1, sim.Units.Count(u => u.Def.Id == "shooter" && u.Lane == 0));
        }

        [Test]
        public void 반복_항목은_repeatUntil까지_반복한다()
        {
            var sim = TestHelper.NewSim(ScheduledStage());
            var ticks = sim.Schedule.Entries.Where(e => e.Unit.Id == "shield").Select(e => e.Tick).ToArray();
            CollectionAssert.AreEqual(new[] { 20, 60, 100 }, ticks);
            TestHelper.Ticks(sim, 20 * 10);
            Assert.AreEqual(3, sim.Units.Count(u => u.Def.Id == "shield"));
        }

        [Test]
        public void 적은_슬롯을_쓰지_않고_마나와_무관하다()
        {
            var sim = TestHelper.NewSim(ScheduledStage());
            sim.SetManaForTest(0);
            TestHelper.Ticks(sim, 61);
            Assert.AreEqual(3, sim.Units.Count);
            Assert.IsNull(sim.PendingSpin);
        }

        [Test]
        public void 실제_스테이지_시간표를_불러온다()
        {
            foreach (var stage in TestHelper.Data.Stages)
            {
                var sim = TestHelper.NewSim(stage);
                Assert.IsNotEmpty(sim.Schedule.Entries, "스테이지 " + stage.Id);
                Assert.That(sim.Schedule.Entries.Select(e => e.Tick), Is.Ordered);
                Assert.That(sim.Schedule.Entries.All(e => e.Tick <= sim.DurationTicks));
            }
        }
    }
}
