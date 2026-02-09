using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;

namespace JustLauncher
{
    public partial class ConsolePage : UserControl
    {
        public ConsolePage()
        {
            InitializeComponent();
            
            // Initial load
            var console = this.FindControl<SelectableTextBlock>("ConsoleOutput");
            if (console != null)
            {
                console.Text = ConsoleService.Instance.FullLog;
                ScrollToEnd();
            }

            // Subscribe to new logs
            ConsoleService.Instance.MessageLogged += OnMessageLogged;
        }

        private void OnMessageLogged(string? message)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var console = this.FindControl<SelectableTextBlock>("ConsoleOutput");
                if (console == null) return;

                if (message == null) // Clear signal
                {
                    console.Text = "";
                }
                else
                {
                    console.Text += message + "\n";
                    ScrollToEnd();
                }
            });
        }

        private void ScrollToEnd()
        {
            var scroll = this.FindControl<ScrollViewer>("ConsoleScrollViewer");
            scroll?.ScrollToEnd();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            var clearBtn = this.FindControl<Button>("ClearConsoleButton");
            if (clearBtn != null) clearBtn.Click += (s, e) => ConsoleService.Instance.Clear();

            var copyBtn = this.FindControl<Button>("CopyConsoleButton");
            if (copyBtn != null) copyBtn.Click += async (s, e) => {
                var console = this.FindControl<SelectableTextBlock>("ConsoleOutput");
                if (console != null && !string.IsNullOrEmpty(console.Text))
                {
                    var topLevel = TopLevel.GetTopLevel(this);
                    if (topLevel?.Clipboard != null)
                    {
                        await topLevel.Clipboard.SetTextAsync(console.Text);
                    }
                }
            };
        }

        // Unsubscribe when detached to avoid memory leaks
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            ConsoleService.Instance.MessageLogged -= OnMessageLogged;
            base.OnDetachedFromVisualTree(e);
        }
    }
}
