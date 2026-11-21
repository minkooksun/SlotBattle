using System;
using System.Collections.Generic;
using System.IO;
using SlotBattle.Core.Battle;
using SlotBattle.Core.Data;

namespace SlotBattle.Tools.BalanceSim
{
    /// <summary>자동 플레이어. 매 틱 직전에 불려 명령을 낸다.</summary>
    public interface IAutoPlayer
    {
        string Name { get; }
        void Act(BattleSimulation sim);
    }

    public static class BattleRunner
    {
        /// <summary>자동 플레이어로 한 판을 끝까지 돌린다.</summary>
        public static BattleSimulation Run(GameData data, StageDef stage, IReadOnlyList<string> deck, ulong seed, IAutoPlayer player)
        {
            return Run(data, stage, deck, seed, player, out _, out _);
        }

        /// <summary>한 판을 돌리며 양쪽에 나온 유닛 수(물량 유닛은 기체마다)를 센다.</summary>
        public static BattleSimulation Run(GameData data, StageDef stage, IReadOnlyList<string> deck, ulong seed, IAutoPlayer player,
            out int playerUnits, out int enemyUnits)
        {
            var sim = Run(data, stage, deck, seed, player, out BattleStats stats);
            playerUnits = stats.PlayerSummoned;
            enemyUnits = stats.EnemySummoned;
            return sim;
        }

        /// <summary>한 판을 돌리며 배치 횟수와 틱마다의 동시 유닛 수(기체 단위)를 잰다.</summary>
        public static BattleSimulation Run(GameData data, StageDef stage, IReadOnlyList<string> deck, ulong seed, IAutoPlayer player,
            out BattleStats stats)
        {
            var sim = new BattleSimulation(data, stage, deck, seed);
            stats = new BattleStats();
            int logged = 0;
            while (!sim.IsOver)
            {
                player.Act(sim);
                var log = sim.CommandLog;
                for (; logged < log.Count; logged++)
                {
                    if (log[logged].Command is PlaceUnitCommand)
                    {
                        stats.Placements++;
                    }
                }
                sim.Tick();
                foreach (var e in sim.DrainEvents())
                {
                    if (e is UnitSummonedEvent summoned)
                    {
                        if (summoned.Unit.Side == Side.Player)
                        {
                            stats.PlayerSummoned++;
                        }
                        else
                        {
                            stats.EnemySummoned++;
                        }
                    }
                }
                int mine = 0;
                int theirs = 0;
                foreach (var unit in sim.Units)
                {
                    if (unit.Side == Side.Player)
                    {
                        mine++;
                    }
                    else
                    {
                        theirs++;
                    }
                }
                stats.Ticks++;
                stats.PlayerUnitTicks += mine;
                stats.EnemyUnitTicks += theirs;
                stats.PlayerMax = Math.Max(stats.PlayerMax, mine);
                stats.EnemyMax = Math.Max(stats.EnemyMax, theirs);
            }
            sim.DrainEvents();
            return sim;
        }
    }

    /// <summary>한 판의 측정값. 동시 유닛 수는 전장에 있는 기체 수(트윈봇은 2기)다.</summary>
    public sealed class BattleStats
    {
        public int Placements;
        public int PlayerSummoned;
        public int EnemySummoned;
        public long Ticks;
        public long PlayerUnitTicks;
        public long EnemyUnitTicks;
        public int PlayerMax;
        public int EnemyMax;

        public double PlayerAverage => Ticks == 0 ? 0 : (double)PlayerUnitTicks / Ticks;
        public double EnemyAverage => Ticks == 0 ? 0 : (double)EnemyUnitTicks / Ticks;
    }

    /// <summary>Assets/Data의 JSON을 디스크에서 읽는다(테스트와 BalanceSim 전용).</summary>
    public static class DataFiles
    {
        public static GameData Load(string dataDirectory = null)
        {
            string dir = dataDirectory ?? FindDataDirectory();
            return GameData.FromJson(
                File.ReadAllText(Path.Combine(dir, "units.json")),
                File.ReadAllText(Path.Combine(dir, "slot.json")),
                File.ReadAllText(Path.Combine(dir, "battle.json")),
                File.ReadAllText(Path.Combine(dir, "stages.json")));
        }

        public static string FindDataDirectory()
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    string candidate = Path.Combine(dir.FullName, "Assets", "Data");
                    if (File.Exists(Path.Combine(candidate, "units.json")))
                    {
                        return candidate;
                    }
                    dir = dir.Parent;
                }
            }
            throw new DirectoryNotFoundException("Assets/Data 폴더를 찾지 못했습니다.");
        }
    }
}
