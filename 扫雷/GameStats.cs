using System;
using System.Collections.Generic;
using System.Linq;

namespace 扫雷
{
    /// <summary>
    /// 保存最近战绩，供游戏窗口和主菜单窗口之间传递数据。
    /// </summary>
    public static class GameStats
    {
        // 街机模式只保留最近 5 局，用来在主菜单做简洁总览。
        private const int MaxArcadeResults = 5;
        private static readonly List<GameResult> ArcadeResults = new();

        public static GameResult? LastClassicResult { get; private set; }
        public static IReadOnlyList<GameResult> LastArcadeResults => ArcadeResults;

        public static void AddResult(GameResult result)
        {
            // 普通模式看最后一局；街机模式更像连续挑战，所以按队列保存最近几局。
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
        }

        public static string BuildArcadeSummary()
        {
            // 主菜单直接调用这个方法生成街机汇总文案。
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
                   $"总用时：{totalDuration:mm\\:ss} | 探明雷数：{totalFlags}/{totalMines} | 揭开安全格：{totalSafeCells}";
        }
    }

    public sealed class GameResult
    {
        // 一局游戏的结算快照，普通模式和街机模式共用。
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
