using System;
using System.Collections.Generic;
using System.Linq;

namespace 扫雷
{
    /// <summary>
    /// 保存当前程序运行期的最近战绩，并在登录玩家模式下同步写入 stats.json。
    /// </summary>
    public static class GameStats
    {
        private const int MaxArcadeResults = 5;
        private static readonly List<GameResult> ArcadeResults = new();

        public static GameResult? LastClassicResult { get; private set; }
        public static IReadOnlyList<GameResult> LastArcadeResults => ArcadeResults;

        public static void AddResult(GameResult result)
        {
            if (result.IsArcadeMode)
            {
                ArcadeResults.Insert(0, result);
                if (ArcadeResults.Count > MaxArcadeResults)
                {
                    ArcadeResults.RemoveRange(MaxArcadeResults, ArcadeResults.Count - MaxArcadeResults);
                }
            }
            else
            {
                LastClassicResult = result;
            }

            if (!PlayerSession.IsGuest && !string.IsNullOrWhiteSpace(PlayerSession.CurrentPlayerName))
            {
                StatsStorage.RecordGame(PlayerSession.CurrentPlayerName, result);
            }
        }

        public static string BuildArcadeSummary()
        {
            if (ArcadeResults.Count == 0)
            {
                return "街机模式最近 5 局：暂无";
            }

            int wins = ArcadeResults.Count(result => result.IsWin);
            int totalFlags = ArcadeResults.Sum(result => result.CorrectFlags);
            int totalMines = ArcadeResults.Sum(result => result.TotalMines);
            int totalSafeCells = ArcadeResults.Sum(result => result.RevealedSafeCells);
            TimeSpan totalDuration = TimeSpan.FromTicks(ArcadeResults.Sum(result => result.Duration.Ticks));

            return $"街机模式最近 {ArcadeResults.Count} 局：{wins} 胜 {ArcadeResults.Count - wins} 负 | " +
                   $"总用时：{FormatDuration(totalDuration)} | 探明雷数：{totalFlags}/{totalMines} | 揭开安全格：{totalSafeCells}";
        }

        private static string FormatDuration(TimeSpan duration)
        {
            int totalHours = (int)duration.TotalHours;
            return totalHours > 0
                ? $"{totalHours}:{duration.Minutes:00}:{duration.Seconds:00}"
                : $"{duration.Minutes:00}:{duration.Seconds:00}";
        }
    }

    public sealed class GameResult
    {
        public required string Difficulty { get; init; }
        public required string BoardSize { get; init; }
        public required bool IsArcadeMode { get; init; }
        public required bool IsWin { get; init; }
        public required TimeSpan Duration { get; init; }
        public required int CorrectFlags { get; init; }
        public required int TotalMines { get; init; }
        public required int RevealedSafeCells { get; init; }
        public required int HeartsRemaining { get; init; }
    }
}
