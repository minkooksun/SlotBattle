using System;

namespace SlotBattle.Core.Util
{
    /// <summary>
    /// Core에서 쓰는 유일한 난수 생성기. 같은 시드면 항상 같은 순서의 값을 낸다.
    /// xorshift64* 기반이며 플랫폼이나 런타임 구현에 의존하지 않는다.
    /// </summary>
    public sealed class SeededRandom
    {
        private ulong _state;

        public SeededRandom(ulong seed)
        {
            _state = Mix(seed);
            if (_state == 0)
            {
                _state = 0x9E3779B97F4A7C15UL;
            }
        }

        /// <summary>현재 내부 상태. 결정성 검사에 쓴다.</summary>
        public ulong State => _state;

        /// <summary>한 시드에서 독립된 하위 시드를 만든다(슬롯용, 자동 플레이어용 등).</summary>
        public static ulong Derive(ulong seed, ulong stream)
        {
            return Mix(seed ^ Mix(stream + 0x632BE59BD9B4E019UL));
        }

        public uint NextUInt()
        {
            ulong x = _state;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            _state = x;
            return (uint)((x * 0x2545F4914F6CDD1DUL) >> 32);
        }

        /// <summary>[0, maxExclusive) 범위의 정수.</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            }
            return (int)(((ulong)NextUInt() * (ulong)maxExclusive) >> 32);
        }

        /// <summary>[minInclusive, maxExclusive) 범위의 정수.</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            return minInclusive + NextInt(maxExclusive - minInclusive);
        }

        /// <summary>[0, 1) 범위의 실수.</summary>
        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        /// <summary>[min, max) 범위의 실수.</summary>
        public float NextFloat(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }

        private static ulong Mix(ulong z)
        {
            z += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
