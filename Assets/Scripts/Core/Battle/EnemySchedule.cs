using System;
using System.Collections.Generic;
using SlotBattle.Core.Data;

namespace SlotBattle.Core.Battle
{
    /// <summary>스테이지 시간표를 틱 단위 소환 목록으로 펼친 것.</summary>
    public sealed class EnemySchedule
    {
        public struct Entry
        {
            public int Tick;
            public int Lane;
            public UnitDef Unit;
            public int Tier;
            public float Distance;
            /// <summary>같은 틱 안에서 시간표에 적힌 순서. 정렬을 안정적으로 하기 위해 둔다.</summary>
            public int Order;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private int _next;

        public EnemySchedule(StageDef stage, GameData data, int tickRate)
        {
            int order = 0;
            foreach (var spawn in stage.Spawns)
            {
                var def = data.GetUnit(spawn.Unit);
                float distance = spawn.Distance > 0f ? spawn.Distance : data.Battle.EnemySpawnDistance;
                float time = spawn.Time;
                float until = spawn.RepeatEvery > 0f
                    ? (spawn.RepeatUntil > 0f ? spawn.RepeatUntil : data.Battle.DurationSeconds)
                    : spawn.Time;
                while (time <= until + 1e-4f)
                {
                    _entries.Add(new Entry
                    {
                        Tick = (int)Math.Round(time * tickRate),
                        Lane = spawn.Lane,
                        Unit = def,
                        Tier = spawn.Tier,
                        Distance = distance,
                        Order = order++,
                    });
                    if (spawn.RepeatEvery <= 0f)
                    {
                        break;
                    }
                    time += spawn.RepeatEvery;
                }
            }
            _entries.Sort((a, b) => a.Tick != b.Tick ? a.Tick.CompareTo(b.Tick) : a.Order.CompareTo(b.Order));
        }

        public IReadOnlyList<Entry> Entries => _entries;

        /// <summary>이 틱까지 나와야 할 소환을 순서대로 꺼낸다.</summary>
        internal void TakeDue(int tick, List<Entry> output)
        {
            while (_next < _entries.Count && _entries[_next].Tick <= tick)
            {
                output.Add(_entries[_next]);
                _next++;
            }
        }
    }
}
