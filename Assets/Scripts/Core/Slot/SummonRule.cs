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
    /// 소환 규칙(설계서 2장): 스핀 한 번에 유닛은 1기.
    /// 같은 그림이 2칸 이상이면 가장 많이 나온 그림의 n단계 1기(나머지 칸은 버림).
    /// 모두 다르면 각 칸의 1단계 유닛이 후보가 되고, 플레이어가 그중 하나를 고른다.
    /// 후보는 칸 순서다(모두 다를 때 후보 i = 칸 i).
    /// </summary>
    public static class SummonRule
    {
        /// <summary>받을 수 있는 유닛 후보. 1개면 그대로 받고, 여러 개면 하나를 골라야 한다.</summary>
        public static List<SummonEntry> Resolve(int[] symbols, int maxTier)
        {
            var groups = Group(symbols, maxTier);
            int best = 0;
            for (int i = 1; i < groups.Count; i++)
            {
                if (groups[i].Tier > groups[best].Tier)
                {
                    best = i;
                }
            }
            if (groups[best].Tier >= 2)
            {
                return new List<SummonEntry> { groups[best] };
            }
            return groups;
        }

        /// <summary>그림별 묶음: 같은 그림이 n칸이면 n단계. 그림이 처음 나온 칸 순서.</summary>
        private static List<SummonEntry> Group(int[] symbols, int maxTier)
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
