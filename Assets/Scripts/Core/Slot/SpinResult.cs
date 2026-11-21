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
        public IReadOnlyList<SummonEntry> Summons => _summons;
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
