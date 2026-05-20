using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace 扫雷
{
    /// <summary>
    /// 负责 stats.json 的读写、玩家创建、单局计分和排行榜排序。
    /// </summary>
    public static class StatsStorage
    {
        private const int CurrentVersion = 1;
        private const int RecentGameLimit = 10;
        private static readonly string StatsPath = Path.Combine(AppContext.BaseDirectory, "stats.json");
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static StatsFile Load()
        {
            EnsureFileExists();

            try
            {
                string json = File.ReadAllText(StatsPath);
                StatsFile? stats = JsonSerializer.Deserialize<StatsFile>(json, JsonOptions);
                if (stats == null)
                {
                    throw new InvalidDataException("stats.json is empty.");
                }

                stats.Players ??= new List<PlayerStats>();
                foreach (PlayerStats player in stats.Players)
                {
                    player.RecentGames ??= new List<SavedGameRecord>();
                }

                return stats;
            }
            catch
            {
                BackupCorruptedFile();
                var freshStats = CreateEmptyStats();
                Save(freshStats);
                return freshStats;
            }
        }

        public static IReadOnlyList<string> GetPlayerNames()
        {
            return Load()
                .Players
                .OrderBy(player => player.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(player => player.Name)
                .ToList();
        }

        public static PlayerStats EnsurePlayer(string playerName)
        {
            string name = NormalizePlayerName(playerName);
            StatsFile stats = Load();
            PlayerStats? player = stats.Players.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            if (player != null)
            {
                return player;
            }

            player = new PlayerStats
            {
                Name = name,
                CreatedAt = DateTime.Now,
                LastPlayedAt = DateTime.Now
            };

            stats.Players.Add(player);
            Save(stats);
            return player;
        }

        public static void RecordGame(string playerName, GameResult result)
        {
            string name = NormalizePlayerName(playerName);
            StatsFile stats = Load();
            PlayerStats player = stats.Players.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
                ?? new PlayerStats
                {
                    Name = name,
                    CreatedAt = DateTime.Now
                };

            if (!stats.Players.Contains(player))
            {
                stats.Players.Add(player);
            }

            int score = CalculateScore(result);
            var record = new SavedGameRecord
            {
                Mode = result.IsArcadeMode ? "街机模式" : "普通模式",
                Difficulty = result.Difficulty,
                BoardSize = result.BoardSize,
                IsWin = result.IsWin,
                DurationSeconds = (int)Math.Round(result.Duration.TotalSeconds),
                Score = score,
                TotalMines = result.TotalMines,
                CorrectFlags = result.CorrectFlags,
                RevealedSafeCells = result.RevealedSafeCells,
                HeartsRemaining = result.HeartsRemaining,
                FinishedAt = DateTime.Now
            };

            player.TotalScore += score;
            player.GamesPlayed++;
            if (result.IsWin)
            {
                player.Wins++;
            }
            else
            {
                player.Losses++;
            }

            if (result.IsArcadeMode)
            {
                player.ArcadeGames++;
            }
            else
            {
                player.ClassicGames++;
            }

            player.LastPlayedAt = record.FinishedAt;
            player.RecentGames.Insert(0, record);
            if (player.RecentGames.Count > RecentGameLimit)
            {
                player.RecentGames.RemoveRange(RecentGameLimit, player.RecentGames.Count - RecentGameLimit);
            }

            stats.UpdatedAt = DateTime.Now;
            Save(stats);
        }

        public static IReadOnlyList<PlayerStats> GetLeaderboard()
        {
            return Load()
                .Players
                .OrderByDescending(player => player.TotalScore)
                .ThenByDescending(player => player.Wins)
                .ThenByDescending(player => player.WinRate)
                .ThenByDescending(player => player.LastPlayedAt)
                .ToList();
        }

        public static PlayerStats? GetMvp()
        {
            return GetLeaderboard().FirstOrDefault();
        }

        private static int CalculateScore(GameResult result)
        {
            int winBase = result.IsWin
                ? result.IsArcadeMode ? 1500 : 1000
                : 0;
            int speedBonus = result.IsWin ? Math.Max(0, 600 - (int)result.Duration.TotalSeconds) : 0;
            int score = result.IsArcadeMode
                ? winBase + result.RevealedSafeCells * 8 + result.CorrectFlags * 15 + result.TotalMines * 2 + result.HeartsRemaining * 100 + speedBonus
                : winBase + result.RevealedSafeCells * 5 + result.CorrectFlags * 20 + result.TotalMines * 2 + speedBonus;

            return Math.Max(0, score);
        }

        private static void EnsureFileExists()
        {
            if (!File.Exists(StatsPath))
            {
                Save(CreateEmptyStats());
            }
        }

        private static StatsFile CreateEmptyStats()
        {
            return new StatsFile
            {
                Version = CurrentVersion,
                UpdatedAt = DateTime.Now,
                Players = new List<PlayerStats>()
            };
        }

        private static void Save(StatsFile stats)
        {
            stats.Version = CurrentVersion;
            stats.UpdatedAt = DateTime.Now;
            string json = JsonSerializer.Serialize(stats, JsonOptions);
            File.WriteAllText(StatsPath, json);
        }

        private static void BackupCorruptedFile()
        {
            if (!File.Exists(StatsPath))
            {
                return;
            }

            string backupPath = Path.Combine(AppContext.BaseDirectory, "stats.json.bak");
            File.Copy(StatsPath, backupPath, true);
        }

        private static string NormalizePlayerName(string playerName)
        {
            string name = playerName.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("玩家名不能为空。", nameof(playerName));
            }

            return name;
        }
    }

    public sealed class StatsFile
    {
        public int Version { get; set; }
        public List<PlayerStats> Players { get; set; } = new();
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class PlayerStats
    {
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastPlayedAt { get; set; }
        public int TotalScore { get; set; }
        public int GamesPlayed { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int ClassicGames { get; set; }
        public int ArcadeGames { get; set; }
        public List<SavedGameRecord> RecentGames { get; set; } = new();

        [JsonIgnore]
        public double WinRate => GamesPlayed == 0 ? 0 : (double)Wins / GamesPlayed;
    }

    public sealed class SavedGameRecord
    {
        public string Mode { get; set; } = string.Empty;
        public string Difficulty { get; set; } = string.Empty;
        public string BoardSize { get; set; } = string.Empty;
        public bool IsWin { get; set; }
        public int DurationSeconds { get; set; }
        public int Score { get; set; }
        public int TotalMines { get; set; }
        public int CorrectFlags { get; set; }
        public int RevealedSafeCells { get; set; }
        public int HeartsRemaining { get; set; }
        public DateTime FinishedAt { get; set; }
    }
}
