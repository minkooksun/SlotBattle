using NUnit.Framework;
using SlotBattle.Core.Util;

namespace SlotBattle.Core.Tests
{
    public class SeededRandomTests
    {
        [Test]
        public void 같은_시드는_같은_수열을_낸다()
        {
            var a = new SeededRandom(1234);
            var b = new SeededRandom(1234);
            for (int i = 0; i < 1000; i++)
            {
                Assert.AreEqual(a.NextUInt(), b.NextUInt());
            }
        }

        [Test]
        public void 다른_시드는_다른_수열을_낸다()
        {
            var a = new SeededRandom(1);
            var b = new SeededRandom(2);
            int same = 0;
            for (int i = 0; i < 100; i++)
            {
                if (a.NextUInt() == b.NextUInt())
                {
                    same++;
                }
            }
            Assert.Less(same, 3);
        }

        [Test]
        public void 수열은_고정된_값이다()
        {
            // 구현이 바뀌면 기존 리플레이가 깨지므로 첫 값들을 고정해 둔다.
            var rng = new SeededRandom(42);
            Assert.AreEqual(833678567u, rng.NextUInt());
            Assert.AreEqual(2416485297u, rng.NextUInt());
            Assert.AreEqual(2087809963u, rng.NextUInt());
        }

        [Test]
        public void 범위_정수와_실수는_범위를_벗어나지_않는다()
        {
            var rng = new SeededRandom(7);
            var seen = new bool[6];
            for (int i = 0; i < 10000; i++)
            {
                int n = rng.NextInt(6);
                Assert.That(n, Is.InRange(0, 5));
                seen[n] = true;
                float f = rng.NextFloat();
                Assert.That(f, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                int m = rng.NextInt(3, 5);
                Assert.That(m, Is.InRange(3, 4));
            }
            Assert.That(seen, Has.All.True);
        }

        [Test]
        public void 하위_시드는_시드와_흐름마다_다르다()
        {
            Assert.AreEqual(SeededRandom.Derive(1, 1), SeededRandom.Derive(1, 1));
            Assert.AreNotEqual(SeededRandom.Derive(1, 1), SeededRandom.Derive(1, 2));
            Assert.AreNotEqual(SeededRandom.Derive(1, 1), SeededRandom.Derive(2, 1));
        }
    }
}
