using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SlotBattle.Core.Battle;
using SlotBattle.Core.Slot;
using SlotBattle.Core.Util;

namespace SlotBattle.Core.Tests
{
    /// <summary>설계서 2장 "한 판의 흐름" 2~4번과 "슬롯 소환 규칙".</summary>
    public class SlotTests
    {
        // ── 소환 규칙(순수 함수) ──

        [Test]
        public void 세_칸이_모두_다르면_각_그림의_1단계_3기()
        {
            var result = SummonRule.Resolve(new[] { 0, 1, 2 }, 3);
            CollectionAssert.AreEqual(new[] { new SummonEntry(0, 1), new SummonEntry(1, 1), new SummonEntry(2, 1) }, result);
        }

        [TestCase(new[] { 3, 3, 1 })]
        [TestCase(new[] { 3, 1, 3 })]
        [TestCase(new[] { 1, 3, 3 })]
        public void 두_칸이_같으면_2단계_1기와_나머지_1단계_1기(int[] symbols)
        {
            var result = SummonRule.Resolve(symbols, 3);
            Assert.AreEqual(2, result.Count);
            CollectionAssert.Contains(result, new SummonEntry(3, 2));
            CollectionAssert.Contains(result, new SummonEntry(1, 1));
        }

        [Test]
        public void 세_칸이_모두_같으면_3단계_1기와_잭팟()
        {
            var result = SummonRule.Resolve(new[] { 4, 4, 4 }, 3);
            CollectionAssert.AreEqual(new[] { new SummonEntry(4, 3) }, result);
            Assert.IsTrue(SummonRule.IsJackpot(new[] { 4, 4, 4 }));
            Assert.IsFalse(SummonRule.IsJackpot(new[] { 4, 4, 1 }));
        }

        // ── 슬롯 확률 ──

        [Test]
        public void 칸마다_등급_가중치를_덱_가중치_합으로_나눈_확률로_나온다()
        {
            // 시작 덱: 일반 4종(25) + 희귀 1종(18) = 118
            var weights = new[] { 25, 25, 25, 25, 18 };
            var slot = new SlotMachine(weights, 3, new SeededRandom(99));
            Assert.AreEqual(118, slot.TotalWeight);
            Assert.AreEqual(25.0 / 118, slot.Probability(0), 1e-9);
            Assert.AreEqual(18.0 / 118, slot.Probability(4), 1e-9);

            const int rolls = 200000;
            var counts = new int[5];
            for (int i = 0; i < rolls; i++)
            {
                counts[slot.RollSymbol()]++;
            }
            for (int s = 0; s < 5; s++)
            {
                Assert.AreEqual(slot.Probability(s), (double)counts[s] / rolls, 0.005, "그림 " + s);
            }
        }

        [Test]
        public void 영웅_등급은_가중치_12로_덜_나온다()
        {
            Assert.AreEqual(12, TestHelper.Data.Slot.RarityWeights["epic"]);
            var slot = new SlotMachine(new[] { 25, 18, 12 }, 3, new SeededRandom(1));
            Assert.Less(slot.Probability(2), slot.Probability(1));
            Assert.Less(slot.Probability(1), slot.Probability(0));
        }

        [Test]
        public void 슬롯에는_덱에_넣은_5종의_그림만_나온다()
        {
            var deck = new[] { "dash", "drone", "boom", "blade", "shield" };
            var sim = TestHelper.NewSim(deck: deck);
            for (int i = 0; i < 300; i++)
            {
                sim.SetManaForTest(10);
                Assert.AreEqual(CommandResult.Ok, sim.Execute(new SpinCommand()));
                foreach (int symbol in sim.PendingSpin.Symbols)
                {
                    CollectionAssert.Contains(deck, sim.Deck[symbol].Id);
                }
                sim.Execute(new ConfirmSpinCommand());
                while (sim.Queue.Count > 0)
                {
                    sim.Execute(new PlaceUnitCommand(0, 0, 0f));
                }
            }
        }

        [Test]
        public void 덱은_서로_다른_5종이어야_한다()
        {
            Assert.Throws<System.ArgumentException>(() => TestHelper.NewSim(deck: new[] { "blade", "shooter" }));
            Assert.Throws<System.ArgumentException>(() => TestHelper.NewSim(deck: new[] { "blade", "blade", "shield", "swarm", "plasma" }));
        }

        // ── 스핀 ──

        [Test]
        public void 스핀은_마나_3을_쓴다()
        {
            var sim = TestHelper.NewSim();
            Assert.AreEqual(5, sim.Mana);
            Assert.AreEqual(CommandResult.Ok, sim.Execute(new SpinCommand()));
            Assert.AreEqual(2, sim.Mana);
            Assert.IsNotNull(sim.PendingSpin);
            Assert.AreEqual(3, sim.PendingSpin.Symbols.Count);
        }

        [Test]
        public void 마나가_모자라면_돌릴_수_없다()
        {
            var sim = TestHelper.NewSim();
            sim.SetManaForTest(2.95f);
            Assert.AreEqual(CommandResult.NotEnoughMana, sim.Execute(new SpinCommand()));
            Assert.IsNull(sim.PendingSpin);
            Assert.AreEqual(2.95f, sim.ManaExact, 1e-4);
        }

        [Test]
        public void 확정하기_전에는_다시_스핀할_수_없다()
        {
            var sim = TestHelper.NewSim();
            sim.SetManaForTest(10);
            sim.Execute(new SpinCommand());
            Assert.AreEqual(CommandResult.SpinPending, sim.Execute(new SpinCommand()));
        }

        [Test]
        public void 스핀하면_릴_정지_사건이_칸마다_나온다()
        {
            var sim = TestHelper.NewSim();
            sim.Execute(new SpinCommand());
            var stops = sim.DrainEvents().OfType<ReelStoppedEvent>().ToList();
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, stops.Select(e => e.ReelIndex));
            CollectionAssert.AreEqual(sim.PendingSpin.Symbols, stops.Select(e => e.Symbol));
        }

        [Test]
        public void 세_칸이_같으면_잭팟_사건이_나온다()
        {
            ulong seed = TestHelper.FindSeed(TestHelper.Triple);
            var sim = TestHelper.NewSim(seed: seed);
            sim.Execute(new SpinCommand());
            Assert.IsTrue(sim.PendingSpin.IsJackpot);
            Assert.AreEqual(1, sim.DrainEvents().OfType<JackpotEvent>().Count());
            sim.Execute(new ConfirmSpinCommand());
            Assert.AreEqual(1, sim.Queue.Count);
            Assert.AreEqual(3, sim.Queue[0].Tier);
        }

        // ── 확정과 대기 칸 ──

        [Test]
        public void 확정한_유닛은_싸우지_않고_대기_칸에_들어간다()
        {
            ulong seed = TestHelper.FindSeed(TestHelper.AllDifferent);
            var sim = TestHelper.NewSim(seed: seed);
            sim.Execute(new SpinCommand());
            var symbols = sim.PendingSpin.Symbols.ToArray();
            Assert.AreEqual(CommandResult.Ok, sim.Execute(new ConfirmSpinCommand()));
            Assert.IsNull(sim.PendingSpin);
            Assert.AreEqual(0, sim.Units.Count);
            Assert.AreEqual(3, sim.Queue.Count);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(sim.Deck[symbols[i]], sim.Queue[i].Def);
                Assert.AreEqual(1, sim.Queue[i].Tier);
            }
        }

        [Test]
        public void 두_칸이_같으면_대기_칸에_2단계와_1단계가_들어간다()
        {
            ulong seed = TestHelper.FindSeed(TestHelper.ExactlyPair);
            var sim = TestHelper.NewSim(seed: seed);
            sim.Execute(new SpinCommand());
            sim.Execute(new ConfirmSpinCommand());
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, sim.Queue.Select(q => q.Tier));
        }

        [Test]
        public void 대기_칸은_최대_4기이고_가득_차면_스핀할_수_없다()
        {
            var sim = TestHelper.NewSim();
            sim.SetManaForTest(10);
            for (int i = 0; i < 4; i++)
            {
                sim.AddToQueueForTest("blade", 1);
            }
            Assert.AreEqual(4, sim.QueueCapacity);
            Assert.IsFalse(sim.CanSpin);
            Assert.AreEqual(CommandResult.QueueFull, sim.Execute(new SpinCommand()));
            Assert.AreEqual(10, sim.Mana);
        }

        [Test]
        public void 대기_칸이_모자라면_확정할_수_없고_자리가_나면_확정된다()
        {
            ulong seed = TestHelper.FindSeed(TestHelper.AllDifferent);
            var sim = TestHelper.NewSim(seed: seed);
            sim.AddToQueueForTest("blade", 1);
            sim.AddToQueueForTest("blade", 1);
            sim.Execute(new SpinCommand());
            Assert.AreEqual(CommandResult.NotEnoughQueueSpace, sim.Execute(new ConfirmSpinCommand()));
            Assert.IsNotNull(sim.PendingSpin);
            Assert.AreEqual(CommandResult.Ok, sim.Execute(new PlaceUnitCommand(0, 0, 1f)));
            Assert.AreEqual(CommandResult.Ok, sim.Execute(new ConfirmSpinCommand()));
            Assert.AreEqual(4, sim.Queue.Count);
        }

        [Test]
        public void 결과가_없으면_확정할_수_없다()
        {
            var sim = TestHelper.NewSim();
            Assert.AreEqual(CommandResult.NoPendingSpin, sim.Execute(new ConfirmSpinCommand()));
        }

        // ── 고정 후 다시 돌리기 ──

        [Test]
        public void 한_칸을_고정하고_마나_1로_나머지_두_칸을_다시_돌린다()
        {
            var sim = TestHelper.NewSim();
            sim.SetManaForTest(10);
            sim.Execute(new SpinCommand());
            int locked = sim.PendingSpin.Symbols[1];
            sim.DrainEvents();
            Assert.AreEqual(CommandResult.Ok, sim.Execute(new RespinCommand(1)));
            Assert.AreEqual(6, sim.Mana);
            Assert.AreEqual(locked, sim.PendingSpin.Symbols[1]);
            var stops = sim.DrainEvents().OfType<ReelStoppedEvent>().ToList();
            CollectionAssert.AreEqual(new[] { 0, 2 }, stops.Select(e => e.ReelIndex));
            Assert.IsTrue(stops.All(e => e.IsRespin));
        }

        [Test]
        public void 다시_돌리기는_스핀당_1회()
        {
            var sim = TestHelper.NewSim();
            sim.SetManaForTest(10);
            sim.Execute(new SpinCommand());
            Assert.AreEqual(CommandResult.Ok, sim.Execute(new RespinCommand(0)));
            Assert.AreEqual(CommandResult.RespinLimitReached, sim.Execute(new RespinCommand(0)));
            sim.Execute(new ConfirmSpinCommand());
            while (sim.Queue.Count > 0)
            {
                sim.Execute(new PlaceUnitCommand(0, 0, 0f));
            }
            sim.Execute(new SpinCommand());
            Assert.AreEqual(CommandResult.Ok, sim.Execute(new RespinCommand(2)), "새 스핀에서는 다시 할 수 있다");
        }

        [Test]
        public void 고정은_정확히_한_칸만_허용한다()
        {
            var sim = TestHelper.NewSim();
            sim.SetManaForTest(10);
            sim.Execute(new SpinCommand());
            Assert.AreEqual(CommandResult.InvalidLock, sim.Execute(new RespinCommand()));
            Assert.AreEqual(CommandResult.InvalidLock, sim.Execute(new RespinCommand(0, 1)));
            Assert.AreEqual(CommandResult.InvalidLock, sim.Execute(new RespinCommand(3)));
            Assert.AreEqual(7, sim.Mana, "실패한 요청은 마나를 쓰지 않는다");
        }

        [Test]
        public void 스핀_없이는_다시_돌릴_수_없고_마나가_모자라도_안_된다()
        {
            var sim = TestHelper.NewSim();
            Assert.AreEqual(CommandResult.NoPendingSpin, sim.Execute(new RespinCommand(0)));
            sim.SetManaForTest(3);
            sim.Execute(new SpinCommand());
            Assert.AreEqual(CommandResult.NotEnoughMana, sim.Execute(new RespinCommand(0)));
        }

        [Test]
        public void 다시_돌려_세_칸이_같아지면_잭팟이다()
        {
            // 첫 스핀이 두 칸 같음이고, 같은 칸을 고정해 다시 돌렸을 때 셋이 맞는 시드를 찾는다.
            for (ulong seed = 1; seed < 100000; seed++)
            {
                var sim = TestHelper.NewSim(seed: seed);
                sim.Execute(new SpinCommand());
                var s = sim.PendingSpin.Symbols.ToArray();
                if (!TestHelper.ExactlyPair(s))
                {
                    continue;
                }
                int lockReel = s[0] == s[1] || s[0] == s[2] ? 0 : 1;
                sim.DrainEvents();
                sim.Execute(new RespinCommand(lockReel));
                if (!sim.PendingSpin.IsJackpot)
                {
                    continue;
                }
                Assert.AreEqual(1, sim.DrainEvents().OfType<JackpotEvent>().Count());
                Assert.AreEqual(1, sim.PendingSpin.Summons.Count);
                Assert.AreEqual(3, sim.PendingSpin.Summons[0].Tier);
                return;
            }
            Assert.Fail("시드를 찾지 못했습니다.");
        }
    }
}
