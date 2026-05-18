using System;
using System.Collections.Generic;

namespace 扫雷
{
    /// <summary>
    /// 扫雷的纯规则层：只保存棋盘状态和处理游戏动作，不直接操作任何 WPF 控件。
    /// </summary>
    public sealed class Gaming
    {
        // 街机模式的生命和补给概率集中放在这里，方便之后调手感。
        private const int ArcadeStartLives = 3;
        private const int ArcadeMaxLives = 3;
        private const double HeartSpawnChance = 0.08;

        // 棋盘状态数组：UI 只通过公开方法读取，不直接改这些数据。
        private readonly bool[,] mines;
        private readonly bool[,] hearts;
        private readonly bool[,] flagged;
        private readonly bool[,] revealed;
        private readonly int[,] adjacentCounts;
        private readonly Random random = new Random();

        public Gaming(int rows, int columns, int mineCount, bool isArcadeMode)
        {
            Rows = rows;
            Columns = columns;
            MineCount = Math.Min(mineCount, rows * columns - 1);
            IsArcadeMode = isArcadeMode;
            Lives = isArcadeMode ? ArcadeStartLives : 0;

            mines = new bool[rows, columns];
            hearts = new bool[rows, columns];
            flagged = new bool[rows, columns];
            revealed = new bool[rows, columns];
            adjacentCounts = new int[rows, columns];
        }

        public int Rows { get; }
        public int Columns { get; }
        public int MineCount { get; }
        public bool IsArcadeMode { get; }
        public int MaxLives => ArcadeMaxLives;
        public int Lives { get; private set; }
        public int FlagCount { get; private set; }
        public int CorrectFlagCount { get; private set; }
        public int RevealedSafeCount { get; private set; }
        public bool MinesPlaced { get; private set; }

        public bool IsMine(int row, int col) => mines[row, col];
        public bool IsHeart(int row, int col) => hearts[row, col];
        public bool IsFlagged(int row, int col) => flagged[row, col];
        public bool IsRevealed(int row, int col) => revealed[row, col];
        public int AdjacentCount(int row, int col) => adjacentCounts[row, col];

        /// <summary>
        /// 首次点击后再生成棋盘；safeRow/safeCol 用来实现第一步避雷。
        /// “？？？”模式会在窗口创建时不传安全格直接调用。
        /// </summary>
        public void PrepareBoard(int? safeRow = null, int? safeCol = null)
        {
            if (MinesPlaced)
            {
                return;
            }

            PlaceMines(safeRow, safeCol);
            PlaceHearts();
            CalculateAdjacentCounts();
        }

        /// <summary>
        /// 切换插旗状态，同时维护正确标记数；正确与否不返回给 UI，避免给玩家剧透。
        /// </summary>
        public bool ToggleFlag(int row, int col)
        {
            flagged[row, col] = !flagged[row, col];
            if (flagged[row, col])
            {
                FlagCount++;
                if (mines[row, col])
                {
                    CorrectFlagCount++;
                }
            }
            else
            {
                FlagCount--;
                if (mines[row, col])
                {
                    CorrectFlagCount--;
                }
            }

            return flagged[row, col];
        }

        /// <summary>
        /// 揭开地雷。普通模式一定结束；街机模式扣心，返回是否已经耗尽生命。
        /// </summary>
        public bool RevealMine(int row, int col)
        {
            revealed[row, col] = true;
            if (!IsArcadeMode)
            {
                return true;
            }

            Lives--;
            return Lives <= 0;
        }

        /// <summary>
        /// 揭开安全格，并返回本次连锁展开的格子列表，交给窗口层统一渲染。
        /// </summary>
        public IReadOnlyList<RevealedCell> RevealCell(int row, int col)
        {
            var revealedCells = new List<RevealedCell>();
            RevealCell(row, col, revealedCells);
            return revealedCells;
        }

        /// <summary>
        /// 普通模式允许“全插对旗”胜利；街机模式必须揭开全部安全格。
        /// </summary>
        public bool HasWon()
        {
            bool allSafeCellsRevealed = RevealedSafeCount == Rows * Columns - MineCount;
            bool allMinesFlagged = !IsArcadeMode && CorrectFlagCount == MineCount && FlagCount == MineCount;
            return allSafeCellsRevealed || allMinesFlagged;
        }

        private void PlaceMines(int? safeRow, int? safeCol)
        {
            // 如果传入了安全格，布雷时会跳过它，保证第一步不踩雷。
            int placed = 0;
            while (placed < MineCount)
            {
                int row = random.Next(Rows);
                int col = random.Next(Columns);

                bool isSafeCell = safeRow == row && safeCol == col;
                if (isSafeCell || mines[row, col])
                {
                    continue;
                }

                mines[row, col] = true;
                placed++;
            }

            MinesPlaced = true;
        }

        private void PlaceHearts()
        {
            // 心心只存在于街机模式，并且只会生成在非雷格上。
            if (!IsArcadeMode)
            {
                return;
            }

            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
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
            // 预先缓存数字，之后 UI 渲染时无需重复扫描周围八格。
            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
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
                            if (r >= 0 && r < Rows && c >= 0 && c < Columns && mines[r, c])
                            {
                                count++;
                            }
                        }
                    }

                    adjacentCounts[row, col] = count;
                }
            }
        }

        private void RevealCell(int row, int col, List<RevealedCell> revealedCells)
        {
            // 递归展开空白区域；心心格即使数字为 0 也停止展开，让补给有明确反馈。
            if (row < 0 || row >= Rows || col < 0 || col >= Columns) return;
            if (revealed[row, col] || flagged[row, col] || mines[row, col]) return;

            revealed[row, col] = true;
            RevealedSafeCount++;

            int previousLives = Lives;
            if (hearts[row, col])
            {
                Lives = Math.Min(Lives + 1, ArcadeMaxLives);
            }

            int count = adjacentCounts[row, col];
            revealedCells.Add(new RevealedCell(row, col, count, hearts[row, col], Lives > previousLives));

            if (count == 0 && !hearts[row, col])
            {
                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        if (dr == 0 && dc == 0) continue;
                        RevealCell(row + dr, col + dc, revealedCells);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 一次揭开动作产生的 UI 渲染信息。
    /// </summary>
    public sealed record RevealedCell(int Row, int Col, int AdjacentCount, bool HasHeart, bool RecoveredLife);
}
