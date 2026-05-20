using System;
using System.Windows;
using System.Windows.Controls;

namespace 扫雷
{
    /// <summary>
    /// 启动登录页：选择游客模式，或登录/创建玩家以保存战绩。
    /// </summary>
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            LoadPlayers();
        }

        private void LoadPlayers()
        {
            // 进入程序时先确保 stats.json 可读；没有文件时会自动创建空档。
            try
            {
                StatsStorage.Load();
                PlayerComboBox.ItemsSource = StatsStorage.GetPlayerNames();
                LoginMessageText.Text = "输入新玩家名会自动创建档案。";
            }
            catch (Exception ex)
            {
                LoginMessageText.Text = $"读取战绩文件失败：{ex.Message}";
            }
        }

        private void PlayerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 从下拉框选择旧玩家时，同步填入输入框，方便直接登录。
            if (PlayerComboBox.SelectedItem is string playerName)
            {
                PlayerNameTextBox.Text = playerName;
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            // 登录会创建或复用玩家档案；后续结算由 GameStats 写入该玩家。
            string playerName = PlayerNameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(playerName))
            {
                LoginMessageText.Text = "请输入玩家名，或者从已有玩家中选择一个。";
                return;
            }

            try
            {
                StatsStorage.EnsurePlayer(playerName);
                PlayerSession.Login(playerName);
                OpenMainMenu();
            }
            catch (Exception ex)
            {
                LoginMessageText.Text = $"登录失败：{ex.Message}";
            }
        }

        private void GuestButton_Click(object sender, RoutedEventArgs e)
        {
            // 游客模式不写入 stats.json，只保留本次运行期内的临时战绩。
            PlayerSession.UseGuest();
            OpenMainMenu();
        }

        private void OpenMainMenu()
        {
            var mainWindow = new MainWindow
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            mainWindow.Show();
            Close();
        }
    }
}
