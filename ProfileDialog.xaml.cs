using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace JustLauncher
{
    public partial class ProfileDialog : Window
    {
        public string ProfileName { get; private set; }
        public string GameDirectory { get; private set; }
        public string Memory { get; private set; }
        public string Username { get; private set; }

        public ProfileDialog(GameProfile existingProfile = null)
        {
            InitializeComponent();
            
            if (existingProfile != null)
            {
                ProfileNameTextBox.Text = existingProfile.Name;
                GameDirectoryTextBox.Text = existingProfile.GameDirectory;
                UsernameTextBox.Text = existingProfile.LastUsedUsername ?? "Player";
                
                var memoryMatch = Regex.Match(existingProfile.JavaArgs ?? "", @"-Xmx(\d+)M");
                MemoryTextBox.Text = memoryMatch.Success ? memoryMatch.Groups[1].Value : "4096";
                
                Title = "Edit Profile";
                CreateButton.Content = "Save";
            }
            else
            {
                ProfileNameTextBox.Text = "New Profile";
                GameDirectoryTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
                UsernameTextBox.Text = "Player";
                MemoryTextBox.Text = "4096";
                
                Title = "Create New Profile";
                CreateButton.Content = "Create";
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BrowseDirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Game Directory",
                InitialDirectory = GameDirectoryTextBox.Text
            };

            if (dialog.ShowDialog() == true)
            {
                GameDirectoryTextBox.Text = dialog.FolderName;
            }
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ProfileNameTextBox.Text))
            {
                MessageBox.Show("Please enter a profile name.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(GameDirectoryTextBox.Text))
            {
                MessageBox.Show("Please select a game directory.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(MemoryTextBox.Text, out int memory) || memory < 512)
            {
                MessageBox.Show("Please enter a valid memory amount (minimum 512 MB).", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ProfileName = ProfileNameTextBox.Text;
            GameDirectory = GameDirectoryTextBox.Text;
            Memory = MemoryTextBox.Text;
            Username = UsernameTextBox.Text;

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}