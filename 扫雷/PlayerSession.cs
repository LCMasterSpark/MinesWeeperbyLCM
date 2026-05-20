namespace 扫雷
{
    /// <summary>
    /// 保存本次程序运行中的玩家身份。游客模式不写入本地排行榜。
    /// </summary>
    public static class PlayerSession
    {
        public static bool IsGuest { get; private set; } = true;
        public static string? CurrentPlayerName { get; private set; }

        public static string DisplayName => IsGuest || string.IsNullOrWhiteSpace(CurrentPlayerName)
            ? "游客"
            : CurrentPlayerName;

        public static void Login(string playerName)
        {
            CurrentPlayerName = playerName.Trim();
            IsGuest = false;
        }

        public static void UseGuest()
        {
            CurrentPlayerName = null;
            IsGuest = true;
        }
    }
}
