using System;
using System.Collections.Generic;
using SlotBattle.Core.Data;
using SlotBattle.Core.Util;

namespace SlotBattle.Core.Slot
{
    /// <summary>
    /// 덱의 그림만 나오는 슬롯. 칸마다 독립적으로 등급 가중치에 따라 그림(덱 인덱스)을 뽑는다.
    /// </summary>
    public sealed class SlotMachine
    {
        private readonly int[] _weights;
        private readonly SeededRandom _rng;

        public int ReelCount { get; }
        public int TotalWeight { get; }
        public int SymbolCount => _weights.Length;

        public SlotMachine(IReadOnlyList<int> symbolWeights, int reelCount, SeededRandom rng)
        {
            if (symbolWeights == null || symbolWeights.Count == 0)
            {
                throw new ArgumentException("그림이 없습니다.", nameof(symbolWeights));
            }
            _weights = new int[symbolWeights.Count];
            for (int i = 0; i < _weights.Length; i++)
            {
                if (symbolWeights[i] <= 0)
                {
                    throw new ArgumentException("가중치는 0보다 커야 합니다.", nameof(symbolWeights));
                }
                _weights[i] = symbolWeights[i];
                TotalWeight += _weights[i];
            }
            ReelCount = reelCount;
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        public int Weight(int symbol)
        {
            return _weights[symbol];
        }

        /// <summary>한 칸에 그 그림이 나올 확률.</summary>
        public double Probability(int symbol)
        {
            return (double)_weights[symbol] / TotalWeight;
        }

        public int RollSymbol()
        {
            int roll = _rng.NextInt(TotalWeight);
            for (int i = 0; i < _weights.Length; i++)
            {
                roll -= _weights[i];
                if (roll < 0)
                {
                    return i;
                }
            }
            return _weights.Length - 1;
        }

        public int[] Spin()
        {
            var result = new int[ReelCount];
            for (int i = 0; i < ReelCount; i++)
            {
                result[i] = RollSymbol();
            }
            return result;
        }

        /// <summary>고정하지 않은 칸만 다시 뽑은 새 배열을 돌려준다.</summary>
        public int[] Respin(int[] current, bool[] locked)
        {
            var result = new int[ReelCount];
            for (int i = 0; i < ReelCount; i++)
            {
                result[i] = locked[i] ? current[i] : RollSymbol();
            }
            return result;
        }
    }
}
