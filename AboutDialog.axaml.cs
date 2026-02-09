using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace JustLauncher
{
    public partial class AboutDialog : Window
    {
        public AboutDialog()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            var closeButton = this.FindControl<Button>("CloseButton");
            if (closeButton != null)
            {
                closeButton.Click += (sender, e) => Close();
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
