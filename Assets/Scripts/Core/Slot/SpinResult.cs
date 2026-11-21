using System.Collections.Generic;

namespace SlotBattle.Core.Slot
{
    /// <summary>확정을 기다리는 스핀 결과.</summary>
    public sealed class SpinResult
    {
        private int[] _symbols;
        private List<SummonEntry> _summons;

        public SpinResult(int[] symbols, int maxTier)
        {
            Set(symbols, maxTier);
        }

        /// <summary>칸별 그림(덱 인덱스).</summary>
        public IReadOnlyList<int> Symbols => _symbols;
        /// <summary>받을 수 있는 유닛 후보. 1개면 그것을 받고, 여러 개(세 칸이 모두 다름)면 칸 하나를 골라야 한다.</summary>
        public IReadOnlyList<SummonEntry> Options => _summons;
        /// <summary>세 칸이 모두 달라 확정할 때 칸 하나를 골라야 하는가.</summary>
        public bool RequiresChoice => _summons.Count > 1;
        public bool IsJackpot { get; private set; }
        public int RespinsUsed { get; internal set; }

        internal int[] SymbolArray => _symbols;

        internal void Set(int[] symbols, int maxTier)
        {
            _symbols = symbols;
            _summons = SummonRule.Resolve(symbols, maxTier);
            IsJackpot = SummonRule.IsJackpot(symbols);
        }
    }
}
