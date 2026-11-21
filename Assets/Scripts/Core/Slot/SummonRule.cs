using System;
using System.Collections.Generic;

namespace SlotBattle.Core.Slot
{
    /// <summary>슬롯 결과로 나오는 유닛 한 묶음. Symbol은 덱 인덱스.</summary>
    public struct SummonEntry : IEquatable<SummonEntry>
    {
        public int Symbol;
        public int Tier;

        public SummonEntry(int symbol, int tier)
        {
            Symbol = symbol;
            Tier = tier;
        }

        public bool Equals(SummonEntry other) => Symbol == other.Symbol && Tier == other.Tier;
        public override bool Equals(object obj) => obj is SummonEntry other && Equals(other);
        public override int GetHashCode() => Symbol * 31 + Tier;
        public override string ToString() => "(" + Symbol + ", " + Tier + "단계)";
    }

    /// <summary>
    /// 소환 규칙: 같은 그림이 n칸이면 그 그림의 n단계 유닛 1기.
    /// 3칸 기준으로 모두 다름 → 1단계 3기, 두 칸 같음 → 2단계 1기 + 1단계 1기, 세 칸 같음 → 3단계 1기.
    /// 결과는 그림이 처음 나온 칸 순서로 정렬한다.
    /// </summary>
    public static class SummonRule
    {
        public static List<SummonEntry> Resolve(int[] symbols, int maxTier)
        {
            var result = new List<SummonEntry>(symbols.Length);
            for (int i = 0; i < symbols.Length; i++)
            {
                bool seenBefore = false;
                for (int j = 0; j < i; j++)
                {
                    if (symbols[j] == symbols[i])
                    {
                        seenBefore = true;
                        break;
                    }
                }
                if (seenBefore)
                {
                    continue;
                }
                int count = 0;
                for (int j = i; j < symbols.Length; j++)
                {
                    if (symbols[j] == symbols[i])
                    {
                        count++;
                    }
                }
                result.Add(new SummonEntry(symbols[i], Math.Min(count, maxTier)));
            }
            return result;
        }

        /// <summary>모든 칸이 같은 그림이면 잭팟.</summary>
        public static bool IsJackpot(int[] symbols)
        {
            for (int i = 1; i < symbols.Length; i++)
            {
                if (symbols[i] != symbols[0])
                {
                    return false;
                }
            }
            return symbols.Length > 1;
        }
    }
}
