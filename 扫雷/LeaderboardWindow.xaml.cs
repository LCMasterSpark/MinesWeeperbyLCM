using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace 扫雷
{
    /// <summary>
    /// 排行榜窗口：显示所有本地玩家，并标出当前 MVP。
    /// </summary>
    public partial class LeaderboardWindow : Window
    {
        public LeaderboardWindow()
        {
            InitializeComponent();
            LoadLeaderboard();
        }

        private void LoadLeaderboard()
        {
            // 排序规则由 StatsStorage 统一维护，窗口只负责转换成表格行。
            IReadOnlyList<PlayerStats> players = StatsStorage.GetLeaderboard();
            PlayerStats? mvp = players.FirstOrDefault();
            MvpText.Text = mvp == null
                ? "暂无玩家战绩"
                : $"MVP：{mvp.Name} | 总分：{mvp.TotalScore} | 胜局：{mvp.Wins} | 胜率：{FormatWinRate(mvp)}";

            LeaderboardGrid.ItemsSource = players
                .Select((player, index) => new LeaderboardRow(player, index + 1))
                .ToList();
        }

        private static string FormatWinRate(PlayerStats player)
        {
            return player.GamesPlayed == 0 ? "0%" : $"{player.WinRate:P0}";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    /// <summary>
    /// DataGrid 专用展示模型，把存档数据转换成适合界面绑定的文本。
    /// </summary>
    public sealed class LeaderboardRow
    {
        private readonly PlayerStats player;

        public LeaderboardRow(PlayerStats player, int rank)
        {
            this.player = player;
            Rank = rank;
        }

        public int Rank { get; }
        public string Name => player.Name;
        public int TotalScore => player.TotalScore;
        public int GamesPlayed => player.GamesPlayed;
        public int Wins => player.Wins;
        public int ClassicGames => player.ClassicGames;
        public int ArcadeGames => player.ArcadeGames;
        public string WinRateText => player.GamesPlayed == 0 ? "0%" : $"{player.WinRate:P0}";
        public string LastPlayedText => player.LastPlayedAt == default ? "暂无" : player.LastPlayedAt.ToString("yyyy-MM-dd HH:mm");
    }
}
