using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using SlotBattle.Core.Battle;
using SlotBattle.Core.Data;
using SlotBattle.Core.Util;

namespace SlotBattle.Tools.BalanceSim
{
    /// <summary>
    /// 사용법: dotnet run --project Tools/BalanceSim -- [--games 1000] [--stage 1] [--seed 1] [--enemy-mult 1.0]
    /// 스테이지마다 초보형과 숙련형을 같은 시드 목록으로 돌려 승률과 평균 판 길이를 출력한다.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            int games = 1000;
            int? onlyStage = null;
            ulong baseSeed = 1;
            float? enemyMultiplier = null;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--games":
                        games = int.Parse(args[++i]);
                        break;
                    case "--stage":
                        onlyStage = int.Parse(args[++i]);
                        break;
                    case "--seed":
                        baseSeed = ulong.Parse(args[++i]);
                        break;
                    case "--enemy-mult":
                        // 조정용: 스테이지의 enemyStatMultiplier를 덮어쓴다.
                        enemyMultiplier = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    default:
                        Console.Error.WriteLine("알 수 없는 인자: " + args[i]);
                        return 1;
                }
            }

            var data = DataFiles.Load();
            var deck = data.Slot.StartingDeck;
            var watch = Stopwatch.StartNew();

            Console.WriteLine("SlotBattle BalanceSim — 스테이지별 " + games + "판, 시드 " + baseSeed + "부터, 덱: " + string.Join(", ", deck));
            Console.WriteLine();
            Console.WriteLine("스테이지          | 플레이어 | 승률    | 평균 길이 | 시간 초과 | 승/패/무");
            Console.WriteLine("------------------+----------+---------+-----------+-----------+-------------");

            foreach (var sourceStage in data.Stages)
            {
                if (onlyStage.HasValue && sourceStage.Id != onlyStage.Value)
                {
                    continue;
                }
                var stage = sourceStage;
                if (enemyMultiplier.HasValue)
                {
                    stage = new StageDef
                    {
                        Id = sourceStage.Id,
                        Name = sourceStage.Name,
                        EnemyCastleHp = sourceStage.EnemyCastleHp,
                        EnemyStatMultiplier = enemyMultiplier.Value,
                        RewardGold = sourceStage.RewardGold,
                        Spawns = sourceStage.Spawns,
                    };
                }
                var novice = RunMany(data, stage, deck, games, baseSeed, seed => new NovicePlayer(SeededRandom.Derive(seed, 2)));
                var skilled = RunMany(data, stage, deck, games, baseSeed, seed => new SkilledPlayer());
                double gap = (skilled.WinRate - novice.WinRate) * 100.0;
                string title = (stage.Id + ". " + stage.Name).PadRight(18);
                Console.WriteLine(Row(title, "초보형", novice));
                Console.WriteLine(Row("".PadRight(18), "숙련형", skilled));
                Console.WriteLine("".PadRight(18) + "| 차이 " + gap.ToString("+0.0;-0.0") + "%p");
            }
            Console.WriteLine();
            Console.WriteLine("소요 시간: " + watch.Elapsed.TotalSeconds.ToString("0.0") + "초");
            return 0;
        }

        private sealed class Summary
        {
            public int Wins;
            public int Losses;
            public int Draws;
            public double TotalSeconds;
            public int Games;
            public int Timeouts;
            public long PlayerUnits;
            public long EnemyUnits;

            public double WinRate => Games == 0 ? 0 : (double)Wins / Games;
            public double AverageSeconds => Games == 0 ? 0 : TotalSeconds / Games;
            public double TimeoutRate => Games == 0 ? 0 : (double)Timeouts / Games;
        }

        private static Summary RunMany(GameData data, StageDef stage, IReadOnlyList<string> deck, int games, ulong baseSeed, Func<ulong, IAutoPlayer> factory)
        {
            var outcomes = new BattleOutcome[games];
            var lengths = new double[games];
            var timeouts = new bool[games];
            var playerUnits = new int[games];
            var enemyUnits = new int[games];
            Parallel.For(0, games, i =>
            {
                ulong seed = baseSeed + (ulong)i;
                var sim = BattleRunner.Run(data, stage, deck, seed, factory(seed), out playerUnits[i], out enemyUnits[i]);
                outcomes[i] = sim.Outcome;
                lengths[i] = sim.ElapsedSeconds;
                timeouts[i] = sim.EndedByTimeout;
            });
            var summary = new Summary { Games = games };
            for (int i = 0; i < games; i++)
            {
                summary.TotalSeconds += lengths[i];
                summary.PlayerUnits += playerUnits[i];
                summary.EnemyUnits += enemyUnits[i];
                if (timeouts[i])
                {
                    summary.Timeouts++;
                }
                switch (outcomes[i])
                {
                    case BattleOutcome.Win:
                        summary.Wins++;
                        break;
                    case BattleOutcome.Lose:
                        summary.Losses++;
                        break;
                    default:
                        summary.Draws++;
                        break;
                }
            }
            return summary;
        }

        private static string Row(string title, string player, Summary s)
        {
            return title + "| " + player.PadRight(6) + " | " + Percent(s.WinRate).PadLeft(7) + " | " +
                Seconds(s.AverageSeconds).PadLeft(9) + " | " + Percent(s.TimeoutRate).PadLeft(9) + " | " +
                s.Wins + "/" + s.Losses + "/" + s.Draws + "  | 판당 유닛 내 " + (s.PlayerUnits / (double)s.Games).ToString("0") + " · 적 " + (s.EnemyUnits / (double)s.Games).ToString("0");
        }

        private static string Percent(double rate)
        {
            return (rate * 100.0).ToString("0.0") + "%";
        }

        private static string Seconds(double seconds)
        {
            return seconds.ToString("0.0") + "초";
        }
    }
}
