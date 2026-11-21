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
            var sim = new BattleSimulation(data, stage, deck, seed);
            playerUnits = 0;
            enemyUnits = 0;
            while (!sim.IsOver)
            {
                player.Act(sim);
                sim.Tick();
                foreach (var e in sim.DrainEvents())
                {
                    if (e is UnitSummonedEvent summoned)
                    {
                        if (summoned.Unit.Side == Side.Player)
                        {
                            playerUnits++;
                        }
                        else
                        {
                            enemyUnits++;
                        }
                    }
                }
            }
            sim.DrainEvents();
            return sim;
        }
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
