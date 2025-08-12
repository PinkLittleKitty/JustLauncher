using System.Configuration;
using System.Windows;
using System.Windows.Controls;

namespace JustLauncher
{
    public partial class HomePage : UserControl
    {
        public HomePage()
        {
            InitializeComponent();
            UsernameTextBox.Text = ConfigurationManager.AppSettings["LastUsedUsername"] ?? "Player";
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(username))
            {
                MessageBox.Show("Please enter a username.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings.Remove("LastUsedUsername");
            config.AppSettings.Settings.Add("LastUsedUsername", username);
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");

            var playPage = new PlayPage(username);

            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow != null)
                mainWindow.MainContent.Content = playPage;
        }
    }
}