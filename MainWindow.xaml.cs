using System;
using System.Windows;
using System.Windows.Controls;

namespace JustLauncher
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            MainContent.Content = new HomePage();
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new PlayPage("Player");
        }

        private void AccountsButton_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new AccountsPage();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new SettingsPage();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}