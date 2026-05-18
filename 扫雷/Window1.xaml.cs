using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace 扫雷
{
    /// <summary>
    /// 游戏窗口：负责生成棋盘、处理点击、判断胜负，并写入本局战绩。
    /// </summary>
    public partial class Window1 : Window
    {
        // 窗口层保留模式配置和按钮矩阵，具体规则都委托给 Gaming。
        private readonly int rows;
        private readonly int columns;
        private readonly int mineCount;
        private readonly string difficulty;
        private readonly bool isPrankMode;
        private readonly bool isArcadeMode;
        private readonly Gaming game;
        private readonly Button[,] buttons;
        private readonly DateTime startTime = DateTime.Now;

        private bool gameEnded;

        /// <summary>
        /// 创建一局游戏窗口；普通/街机/？？？模式共用同一套棋盘 UI。
        /// </summary>
        public Window1(int mineCount, string difficulty, int rows, int columns, bool isPrankMode = false, bool isArcadeMode = false)
        {
            InitializeComponent();

            this.rows = rows;
            this.columns = columns;
            this.mineCount = Math.Min(mineCount, rows * columns - 1);
            this.difficulty = difficulty;
            this.isPrankMode = isPrankMode;
            this.isArcadeMode = isArcadeMode;
            game = new Gaming(rows, columns, this.mineCount, isArcadeMode);
            buttons = new Button[rows, columns];

            InitializeGameBoard();

            // “？？？”模式不等待第一次点击，进入游戏时就布雷。
            if (isPrankMode)
            {
                game.PrepareBoard();
                Broadcast("？？？模式已启动：第一步也可能是雷。");
            }
            else if (isArcadeMode)
            {
                Broadcast("街机模式已启动：你有 3 颗心，第一步会避开地雷。");
            }

            UpdateFlagText();
        }

        private void InitializeGameBoard()
        {
            // 先建立选择尺寸的网格，再把每个格子对应的 Button 放进去。
            GameGrid.RowDefinitions.Clear();
            GameGrid.ColumnDefinitions.Clear();

            for (int i = 0; i < rows; i++)
            {
                GameGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            for (int j = 0; j < columns; j++)
            {
                GameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            }

            CreateButtons();
        }

        private void CreateButtons()
        {
            // Tag 保存格子的坐标，点击事件里再取出来定位 Gaming 和按钮矩阵。
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    Button button = new Button
                    {
                        Width = 40,
                        Height = 40,
                        Margin = new Thickness(2),
                        Tag = (row, col),
                        FontWeight = FontWeights.SemiBold,
                        Background = Brushes.LightGray,
                        Foreground = Brushes.Black,
                        Content = string.Empty
                    };

                    button.Click += GameButton_Click;
                    button.MouseRightButtonUp += GameButton_RightClick;
                    buttons[row, col] = button;

                    Grid.SetRow(button, row);
                    Grid.SetColumn(button, col);
                    GameGrid.Children.Add(button);
                }
            }
        }

        private void GameButton_Click(object sender, RoutedEventArgs e)
        {
            // 左键负责揭开格子；非“？？？”模式会在第一次点击后再布雷，以便避开首点。
            if (gameEnded || sender is not Button button) return;
            var (row, col) = ((int Row, int Col))button.Tag;

            if (game.IsFlagged(row, col))
            {
                Broadcast("这个方块已经被标记，先右键取消标记再揭开。");
                return;
            }

            if (!game.MinesPlaced)
            {
                game.PrepareBoard(row, col);
                Broadcast(isArcadeMode ? "已开启街机第一步避雷，地雷和隐藏补给心已生成。" : "已开启第一步避雷，本局地雷已生成。");
            }

            if (game.IsMine(row, col))
            {
                // 街机模式踩雷只扣心，普通模式踩雷立即结束。
                button.Content = "💣";
                button.Background = Brushes.IndianRed;
                button.IsEnabled = false;

                bool shouldEnd = game.RevealMine(row, col);
                if (isArcadeMode && !shouldEnd)
                {
                    Broadcast($"踩到地雷，失去 1 颗心。剩余生命：{game.Lives}/{game.MaxLives}。");
                    UpdateFlagText();
                }
                else
                {
                    RevealAllMines();
                    EndGame(false, isArcadeMode ? "游戏结束！你的心用完了。" : "游戏结束！你踩到地雷了。");
                }
                return;
            }

            RevealCells(game.RevealCell(row, col));
            CheckWinCondition();
        }

        private void GameButton_RightClick(object sender, MouseButtonEventArgs e)
        {
            // 右键只显示插旗状态，不告诉玩家标记是否正确。
            e.Handled = true;
            if (gameEnded || sender is not Button button) return;

            var (row, col) = ((int Row, int Col))button.Tag;
            if (game.IsRevealed(row, col))
            {
                Broadcast("已经揭开的方块不能再标记。");
                return;
            }

            if (!game.MinesPlaced)
            {
                Broadcast("请先左键揭开第一格，系统会自动避开第一步地雷。");
                return;
            }

            bool isFlagged = game.ToggleFlag(row, col);
            if (isFlagged)
            {
                button.Content = "🚩";
                button.Background = Brushes.Khaki;
                Broadcast($"已标记 ({row + 1}, {col + 1})。剩余可标记：{Math.Max(mineCount - game.FlagCount, 0)}。");
            }
            else
            {
                button.Content = string.Empty;
                button.Background = Brushes.LightGray;
                Broadcast($"已取消 ({row + 1}, {col + 1}) 的标记。");
            }

            UpdateFlagText();
            CheckWinCondition();
        }

        private void RevealCells(System.Collections.Generic.IEnumerable<RevealedCell> revealedCells)
        {
            // Gaming 返回所有被连锁揭开的格子，窗口层只负责把它们画出来。
            foreach (RevealedCell cell in revealedCells)
            {
                Button button = buttons[cell.Row, cell.Col];
                if (button == null) continue;

                button.IsEnabled = false;
                if (cell.HasHeart)
                {
                    button.Content = "❤";
                    button.Foreground = Brushes.DeepPink;
                    Broadcast(cell.RecoveredLife ? $"捡到一颗心，生命恢复到 {game.Lives}/{game.MaxLives}。" : "捡到一颗心，但生命已经满了。");
                    UpdateFlagText();
                }
                else
                {
                    button.Content = cell.AdjacentCount > 0 ? cell.AdjacentCount.ToString() : string.Empty;
                }

                button.Background = Brushes.WhiteSmoke;
            }
        }

        private void RevealAllMines()
        {
            // 结算时把全部地雷显示出来，给玩家看最终棋盘。
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    if (game.IsMine(row, col))
                    {
                        Button mineButton = buttons[row, col];
                        if (mineButton != null)
                        {
                            mineButton.Content = "💣";
                            mineButton.Background = Brushes.IndianRed;
                        }
                    }
                }
            }
        }

        private void CheckWinCondition()
        {
            // 胜利条件：揭开所有安全格，或者所有雷都被正确标记。
            if (gameEnded) return;

            if (game.HasWon())
            {
                RevealAllMines();
                EndGame(true, "恭喜你，成功排除所有地雷！");
            }
        }

        private void EndGame(bool isWin, string message)
        {
            // 结算时写入全局战绩；街机模式还会弹出“再来一局/回菜单”窗口。
            gameEnded = true;
            GameStats.AddResult(new GameResult
            {
                Difficulty = difficulty,
                BoardSize = $"{rows}x{columns}",
                IsArcadeMode = isArcadeMode,
                IsWin = isWin,
                Duration = DateTime.Now - startTime,
                CorrectFlags = game.CorrectFlagCount,
                TotalMines = mineCount,
                RevealedSafeCells = game.RevealedSafeCount,
                HeartsRemaining = isArcadeMode ? Math.Max(game.Lives, 0) : 0
            });

            DisableBoard();
            MenuButton.Content = isArcadeMode ? "返回主菜单" : "再来一把";
            Broadcast(isArcadeMode ? $"{message} 战绩已记录。" : $"{message} 战绩已记录，点击“再来一把”返回开始菜单。");
            UpdateFlagText();

            if (isArcadeMode)
            {
                ShowArcadeResultDialog(isWin, message);
            }
        }

        private void DisableBoard()
        {
            foreach (Button button in buttons)
            {
                if (button != null)
                {
                    button.IsEnabled = false;
                }
            }
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            ReturnToMainMenu();
        }

        private void ReturnToMainMenu()
        {
            var mainWindow = new MainWindow
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            mainWindow.Show();
            Close();
        }

        private void RestartArcadeGame()
        {
            // 街机“再来一局”保留当前棋盘尺寸和模式配置，不回主菜单。
            var gameWindow = new Window1(mineCount, difficulty, rows, columns, isPrankMode, isArcadeMode)
            {
                Title = Title,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            gameWindow.Show();
            Close();
        }

        private void ShowArcadeResultDialog(bool isWin, string message)
        {
            // 简单的代码构建弹窗，避免为了一个结算框再新增 XAML 文件。
            var dialog = new Window
            {
                Title = "街机模式战绩",
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                SizeToContent = SizeToContent.WidthAndHeight,
                Background = Brushes.White
            };

            var panel = new StackPanel
            {
                Width = 360,
                Margin = new Thickness(22)
            };

            panel.Children.Add(new TextBlock
            {
                Text = isWin ? "街机模式胜利" : "街机模式结束",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = isWin ? Brushes.SeaGreen : Brushes.IndianRed,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            });

            panel.Children.Add(new TextBlock
            {
                Text = $"{message}\n棋盘：{rows}x{columns}\n用时：{DateTime.Now - startTime:mm\\:ss}\n生命：{Math.Max(game.Lives, 0)}/{game.MaxLives}\n探明雷数：{game.CorrectFlagCount}/{mineCount}\n揭开安全格：{game.RevealedSafeCount}",
                FontSize = 14,
                Foreground = Brushes.Black,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 18)
            });

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var replayButton = new Button
            {
                Content = "再来一局",
                MinWidth = 100,
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 10, 0)
            };
            replayButton.Click += (_, _) =>
            {
                dialog.Close();
                RestartArcadeGame();
            };

            var menuButton = new Button
            {
                Content = "返回主菜单",
                MinWidth = 100,
                Padding = new Thickness(12, 8, 12, 8)
            };
            menuButton.Click += (_, _) =>
            {
                dialog.Close();
                ReturnToMainMenu();
            };

            buttons.Children.Add(replayButton);
            buttons.Children.Add(menuButton);
            panel.Children.Add(buttons);
            dialog.Content = panel;
            dialog.ShowDialog();
        }

        private void Broadcast(string message)
        {
            StatusText.Text = $"{message}";
        }

        private void UpdateFlagText()
        {
            string lifeText = isArcadeMode ? $"，生命：{Math.Max(game.Lives, 0)}/{game.MaxLives}" : string.Empty;
            FlagText.Text = $"已标记：{game.FlagCount}/{mineCount}{lifeText}";
        }
    }
}
