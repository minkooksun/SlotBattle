using NUnit.Framework;
using SlotBattle.Core.Battle;
using SlotBattle.Core.Util;
using SlotBattle.Tools.BalanceSim;

namespace SlotBattle.Core.Tests
{
    /// <summary>M1 완료 기준: 같은 시드 → 같은 결과, 명령 기록으로 리플레이, 숙련형이 초보형보다 25%p 이상.</summary>
    public class DeterminismAndBalanceTests
    {
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void 같은_시드로_두_번_돌린_결과는_같다(int stageId)
        {
            var data = TestHelper.Data;
            var stage = data.GetStage(stageId);
            for (ulong seed = 1; seed <= 5; seed++)
            {
                var a = BattleRunner.Run(data, stage, TestHelper.Deck, seed, new SkilledPlayer());
                var b = BattleRunner.Run(data, stage, TestHelper.Deck, seed, new SkilledPlayer());
                Assert.AreEqual(a.ComputeStateHash(), b.ComputeStateHash());
                Assert.AreEqual(a.Outcome, b.Outcome);
                Assert.AreEqual(a.CurrentTick, b.CurrentTick);

                var c = BattleRunner.Run(data, stage, TestHelper.Deck, seed, new NovicePlayer(SeededRandom.Derive(seed, 2)));
                var d = BattleRunner.Run(data, stage, TestHelper.Deck, seed, new NovicePlayer(SeededRandom.Derive(seed, 2)));
                Assert.AreEqual(c.ComputeStateHash(), d.ComputeStateHash());
            }
        }

        [Test]
        public void 다른_시드는_다른_판이_된다()
        {
            var data = TestHelper.Data;
            var stage = data.GetStage(1);
            var a = BattleRunner.Run(data, stage, TestHelper.Deck, 1, new SkilledPlayer());
            var b = BattleRunner.Run(data, stage, TestHelper.Deck, 2, new SkilledPlayer());
            Assert.AreNotEqual(a.ComputeStateHash(), b.ComputeStateHash());
        }

        [Test]
        public void 명령_기록만으로_같은_판을_다시_재생할_수_있다()
        {
            var data = TestHelper.Data;
            var stage = data.GetStage(2);
            var original = BattleRunner.Run(data, stage, TestHelper.Deck, 77, new SkilledPlayer());
            Assert.IsNotEmpty(original.CommandLog);

            var replay = new BattleSimulation(data, stage, TestHelper.Deck, 77);
            int next = 0;
            var log = original.CommandLog;
            while (!replay.IsOver)
            {
                while (next < log.Count && log[next].Tick == replay.CurrentTick)
                {
                    Assert.AreEqual(CommandResult.Ok, replay.Execute(log[next].Command));
                    next++;
                }
                replay.Tick();
            }
            Assert.AreEqual(log.Count, next);
            Assert.AreEqual(original.ComputeStateHash(), replay.ComputeStateHash());
            Assert.AreEqual(original.Outcome, replay.Outcome);
        }

        [Test]
        public void 같은_시드에서_숙련형의_승률이_초보형보다_25퍼센트포인트_이상_높다()
        {
            var data = TestHelper.Data;
            const int games = 200;
            foreach (var stage in data.Stages)
            {
                int novice = 0, skilled = 0;
                for (ulong seed = 1; seed <= games; seed++)
                {
                    if (BattleRunner.Run(data, stage, TestHelper.Deck, seed, new NovicePlayer(SeededRandom.Derive(seed, 2))).Outcome == BattleOutcome.Win)
                    {
                        novice++;
                    }
                    if (BattleRunner.Run(data, stage, TestHelper.Deck, seed, new SkilledPlayer()).Outcome == BattleOutcome.Win)
                    {
                        skilled++;
                    }
                }
                double gap = (double)(skilled - novice) / games;
                TestContext.WriteLine("스테이지 " + stage.Id + ": 초보형 " + novice + ", 숙련형 " + skilled + " / " + games + ", 차이 " + (gap * 100).ToString("0.0") + "%p");
                Assert.GreaterOrEqual(gap, 0.25, "스테이지 " + stage.Id);
            }
        }
    }
}
