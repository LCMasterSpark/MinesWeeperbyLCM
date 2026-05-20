using System;
using System.Windows;
using System.Windows.Controls;

namespace 扫雷
{
    /// <summary>
    /// 主菜单窗口：负责选择模式、难度、棋盘尺寸，以及展示战绩。
    /// </summary>
    public partial class MainWindow : Window
    {
        // 街机模式固定安全格数量，因此雷数会随棋盘尺寸变化。
        private const int ArcadeSafeCellCount = 40;
        private RadioButton[] difficultyButtons = Array.Empty<RadioButton>();

        public MainWindow()
        {
            InitializeComponent();
            difficultyButtons = new[] { rbBaby, rbEasy, rbMedium, rbHard, rbLarge, rbHuge, rbExtreme };
            PlayerText.Text = PlayerSession.IsGuest ? "当前身份：游客（不保存战绩）" : $"当前玩家：{PlayerSession.DisplayName}";
            ShowLastGameStats();
            UpdateArcadeOptions();
        }

        private void BtnStartGame_Click(object sender, RoutedEventArgs e)
        {
            bool isArcadeMode = cbArcadeMode.IsChecked == true;
            int boardSize = GetSelectedBoardSize();
            if (boardSize == 0)
            {
                MenuMessageText.Text = "请选择棋盘尺寸。";
                return;
            }

            if (isArcadeMode && boardSize == 5)
            {
                MenuMessageText.Text = "街机模式不能使用 5 x 5 棋盘。";
                return;
            }

            int mineCount;
            string difficulty;
            bool isPrankMode = false;

            if (isArcadeMode)
            {
                // 例如 8x8 是 24 雷，12x12 是 104 雷，始终留下 40 个安全格。
                mineCount = boardSize * boardSize - ArcadeSafeCellCount;
                difficulty = "街机模式";
            }
            else if (!TryGetClassicDifficulty(boardSize, out mineCount, out difficulty, out isPrankMode))
            {
                MenuMessageText.Text = "请选择难度级别。";
                return;
            }

            var gameWindow = new GameWindow(mineCount, difficulty, boardSize, boardSize, isPrankMode, isArcadeMode)
            {
                Title = $"扫雷 - {difficulty} - {boardSize}x{boardSize}",
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            gameWindow.Show();
            Close();
        }

        private void LeaderboardButton_Click(object sender, RoutedEventArgs e)
        {
            var leaderboardWindow = new LeaderboardWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            leaderboardWindow.ShowDialog();
        }

        private void ArcadeMode_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized || difficultyButtons.Length == 0)
            {
                return;
            }

            UpdateArcadeOptions();
        }

        private void UpdateArcadeOptions()
        {
            // 街机模式有独立规则，所以禁用普通难度，并禁止过小的 5x5 棋盘。
            bool isArcadeMode = cbArcadeMode.IsChecked == true;

            foreach (RadioButton button in difficultyButtons)
            {
                button.IsEnabled = !isArcadeMode;
            }

            rbSize5.IsEnabled = !isArcadeMode;
            if (isArcadeMode)
            {
                if (rbSize5.IsChecked == true)
                {
                    rbSize8.IsChecked = true;
                }

                MenuMessageText.Text = "街机模式：固定 40 个安全格，3 颗心，棋盘不能小于 8 x 8。";
            }
            else
            {
                ShowLastGameStats();
            }
        }

        private int GetSelectedBoardSize()
        {
            if (rbSize5.IsChecked == true) return 5;
            if (rbSize8.IsChecked == true) return 8;
            if (rbSize12.IsChecked == true) return 12;
            if (rbSize15.IsChecked == true) return 15;
            return 0;
        }

        private bool TryGetClassicDifficulty(int boardSize, out int mineCount, out string difficulty, out bool isPrankMode)
        {
            // 普通模式难度只决定目标雷数；小棋盘会自动压到最大可放雷数。
            int maxMineCount = boardSize * boardSize - 1;
            isPrankMode = false;

            if (rbBaby.IsChecked == true)
            {
                mineCount = 5;
                difficulty = "宝宝模式";
            }
            else if (rbEasy.IsChecked == true)
            {
                mineCount = 10;
                difficulty = "简单";
            }
            else if (rbMedium.IsChecked == true)
            {
                mineCount = 25;
                difficulty = "中等";
            }
            else if (rbHard.IsChecked == true)
            {
                mineCount = 50;
                difficulty = "困难";
            }
            else if (rbLarge.IsChecked == true)
            {
                mineCount = 70;
                difficulty = "大棋盘";
            }
            else if (rbHuge.IsChecked == true)
            {
                mineCount = 110;
                difficulty = "巨型棋盘";
            }
            else if (rbExtreme.IsChecked == true)
            {
                mineCount = maxMineCount;
                difficulty = "？？？";
                isPrankMode = true;
            }
            else
            {
                mineCount = 0;
                difficulty = string.Empty;
                return false;
            }

            mineCount = Math.Min(mineCount, maxMineCount);
            return true;
        }

        private void ShowLastGameStats()
        {
            // 普通模式展示最后一局，街机模式展示最近 5 局汇总。
            if (GameStats.LastClassicResult == null)
            {
                MenuMessageText.Text = "准备就绪，选择一个难度开始游戏。";
                LastStatsText.Text = GameStats.BuildArcadeSummary();
                return;
            }

            var result = GameStats.LastClassicResult;
            string outcome = result.IsWin ? "胜利" : "失败";
            MenuMessageText.Text = "普通模式显示最后一局，街机模式显示最近 5 局总览。";
            LastStatsText.Text =
                $"普通模式最后一局：{outcome} | 难度：{result.Difficulty} | 棋盘：{result.BoardSize} | 用时：{result.Duration:mm\\:ss} | " +
                $"探明雷数：{result.CorrectFlags}/{result.TotalMines} | 揭开安全格：{result.RevealedSafeCells}\n" +
                GameStats.BuildArcadeSummary();
        }
    }
}
