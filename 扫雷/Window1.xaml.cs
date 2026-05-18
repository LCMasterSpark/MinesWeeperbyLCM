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
        private readonly int rows;
        private readonly int columns;
        private readonly int mineCount;
        private readonly string difficulty;
        private readonly bool isPrankMode;
        private readonly bool isArcadeMode;
        private readonly bool[,] mines;
        private readonly bool[,] hearts;
        private readonly bool[,] flagged;
        private readonly bool[,] revealed;
        private readonly int[,] adjacentCounts;
        private readonly Button[,] buttons;
        private readonly Random random = new Random();
        private readonly DateTime startTime = DateTime.Now;

        private bool minesPlaced;
        private bool gameEnded;
        private int flagCount;
        private int correctFlagCount;
        private int revealedSafeCount;
        private int lives;

        private const int ArcadeStartLives = 3;
        private const int ArcadeMaxLives = 3;
        private const double HeartSpawnChance = 0.08;

        public Window1(int mineCount, string difficulty, int rows, int columns, bool isPrankMode = false, bool isArcadeMode = false)
        {
            InitializeComponent();

            this.rows = rows;
            this.columns = columns;
            this.mineCount = Math.Min(mineCount, rows * columns - 1);
            this.difficulty = difficulty;
            this.isPrankMode = isPrankMode;
            this.isArcadeMode = isArcadeMode;
            lives = isArcadeMode ? ArcadeStartLives : 0;
            mines = new bool[rows, columns];
            hearts = new bool[rows, columns];
            flagged = new bool[rows, columns];
            revealed = new bool[rows, columns];
            adjacentCounts = new int[rows, columns];
            buttons = new Button[rows, columns];

            InitializeGameBoard();

            // “？？？”模式不等待第一次点击，进入游戏时就布雷。
            if (isPrankMode)
            {
                PlaceMines();
                PlaceHearts();
                CalculateAdjacentCounts();
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

        private void PlaceMines(int? safeRow = null, int? safeCol = null)
        {
            // 普通模式第一次点击时会传入安全坐标，避免第一步踩雷。
            int placed = 0;
            while (placed < mineCount)
            {
                int row = random.Next(rows);
                int col = random.Next(columns);

                bool isSafeCell = safeRow == row && safeCol == col;
                if (isSafeCell || mines[row, col])
                {
                    continue;
                }

                mines[row, col] = true;
                placed++;
            }

            minesPlaced = true;
        }

        private void PlaceHearts()
        {
            if (!isArcadeMode)
            {
                return;
            }

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    if (!mines[row, col] && random.NextDouble() < HeartSpawnChance)
                    {
                        hearts[row, col] = true;
                    }
                }
            }
        }

        private void CalculateAdjacentCounts()
        {
            // 每个非雷格记录周围八格的地雷数量，雷格用 -1 表示。
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    if (mines[row, col])
                    {
                        adjacentCounts[row, col] = -1;
                        continue;
                    }

                    int count = 0;
                    for (int dr = -1; dr <= 1; dr++)
                    {
                        for (int dc = -1; dc <= 1; dc++)
                        {
                            if (dr == 0 && dc == 0) continue;
                            int r = row + dr;
                            int c = col + dc;
                            if (r >= 0 && r < rows && c >= 0 && c < columns && mines[r, c])
                            {
                                count++;
                            }
                        }
                    }

                    adjacentCounts[row, col] = count;
                }
            }
        }

        private void CreateButtons()
        {
            // Tag 保存格子的坐标，点击事件里再取出来定位数组位置。
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
            // 左键负责揭开格子；如果是普通模式第一步，会在这里延迟布雷。
            if (gameEnded || sender is not Button button) return;
            var (row, col) = ((int Row, int Col))button.Tag;

            if (flagged[row, col])
            {
                Broadcast("这个方块已经被标记，先右键取消标记再揭开。");
                return;
            }

            if (!minesPlaced)
            {
                PlaceMines(row, col);
                PlaceHearts();
                CalculateAdjacentCounts();
                Broadcast(isArcadeMode ? "已开启街机第一步避雷，地雷和隐藏补给心已生成。" : "已开启第一步避雷，本局地雷已生成。");
            }

            if (mines[row, col])
            {
                button.Content = "💣";
                button.Background = Brushes.IndianRed;
                revealed[row, col] = true;
                button.IsEnabled = false;

                if (isArcadeMode)
                {
                    lives--;
                    if (lives <= 0)
                    {
                        RevealAllMines();
                        EndGame(false, "游戏结束！你的心用完了。");
                    }
                    else
                    {
                        Broadcast($"踩到地雷，失去 1 颗心。剩余生命：{lives}/{ArcadeMaxLives}。");
                        UpdateFlagText();
                    }
                }
                else
                {
                    RevealAllMines();
                    EndGame(false, "游戏结束！你踩到地雷了。");
                }
                return;
            }

            RevealCell(row, col);
            CheckWinCondition();
        }

        private void GameButton_RightClick(object sender, MouseButtonEventArgs e)
        {
            // 右键负责插旗或取消插旗，并同步统计正确标记数。
            e.Handled = true;
            if (gameEnded || sender is not Button button) return;

            var (row, col) = ((int Row, int Col))button.Tag;
            if (revealed[row, col])
            {
                Broadcast("已经揭开的方块不能再标记。");
                return;
            }

            if (!minesPlaced)
            {
                Broadcast("请先左键揭开第一格，系统会自动避开第一步地雷。");
                return;
            }

            flagged[row, col] = !flagged[row, col];
            if (flagged[row, col])
            {
                flagCount++;
                button.Content = "🚩";
                button.Background = Brushes.Khaki;

                if (mines[row, col])
                {
                    correctFlagCount++;
                }

                Broadcast($"已标记 ({row + 1}, {col + 1})。剩余可标记：{Math.Max(mineCount - flagCount, 0)}。");
            }
            else
            {
                flagCount--;
                button.Content = string.Empty;
                button.Background = Brushes.LightGray;
                Broadcast($"已取消 ({row + 1}, {col + 1}) 的标记。");

                if (mines[row, col])
                {
                    correctFlagCount--;
                }
            }

            UpdateFlagText();
            CheckWinCondition();
        }

        private void RevealCell(int row, int col)
        {
            // 递归揭开空白区域；遇到数字格、边界、已揭开或已标记格就停止。
            if (row < 0 || row >= rows || col < 0 || col >= columns) return;
            if (revealed[row, col] || flagged[row, col] || mines[row, col]) return;

            Button button = buttons[row, col];
            if (button == null) return;

            revealed[row, col] = true;
            revealedSafeCount++;
            button.IsEnabled = false;
            int count = adjacentCounts[row, col];
            if (hearts[row, col])
            {
                int oldLives = lives;
                lives = Math.Min(lives + 1, ArcadeMaxLives);
                button.Content = "❤";
                button.Foreground = Brushes.DeepPink;
                Broadcast(lives > oldLives ? $"捡到一颗心，生命恢复到 {lives}/{ArcadeMaxLives}。" : "捡到一颗心，但生命已经满了。");
                UpdateFlagText();
            }
            else
            {
                button.Content = count > 0 ? count.ToString() : string.Empty;
            }
            button.Background = Brushes.WhiteSmoke;

            if (count == 0 && !hearts[row, col])
            {
                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        if (dr == 0 && dc == 0) continue;
                        RevealCell(row + dr, col + dc);
                    }
                }
            }
        }

        private void RevealAllMines()
        {
            // 结算时把全部地雷显示出来，给玩家看最终棋盘。
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    if (mines[row, col])
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

            bool allSafeCellsRevealed = revealedSafeCount == rows * columns - mineCount;
            bool allMinesFlagged = !isArcadeMode && correctFlagCount == mineCount && flagCount == mineCount;

            if (allSafeCellsRevealed || allMinesFlagged)
            {
                RevealAllMines();
                EndGame(true, "恭喜你，成功排除所有地雷！");
            }
        }

        private void EndGame(bool isWin, string message)
        {
            // 结算时写入全局战绩，并把按钮改成回主菜单入口。
            gameEnded = true;
            GameStats.AddResult(new GameResult
            {
                Difficulty = difficulty,
                BoardSize = $"{rows}x{columns}",
                IsArcadeMode = isArcadeMode,
                IsWin = isWin,
                Duration = DateTime.Now - startTime,
                CorrectFlags = correctFlagCount,
                TotalMines = mineCount,
                RevealedSafeCells = revealedSafeCount,
                HeartsRemaining = isArcadeMode ? Math.Max(lives, 0) : 0
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
                Text = $"{message}\n棋盘：{rows}x{columns}\n用时：{DateTime.Now - startTime:mm\\:ss}\n生命：{Math.Max(lives, 0)}/{ArcadeMaxLives}\n探明雷数：{correctFlagCount}/{mineCount}\n揭开安全格：{revealedSafeCount}",
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
            string lifeText = isArcadeMode ? $"，生命：{Math.Max(lives, 0)}/{ArcadeMaxLives}" : string.Empty;
            FlagText.Text = $"已标记：{flagCount}/{mineCount}{lifeText}";
        }
    }
}
