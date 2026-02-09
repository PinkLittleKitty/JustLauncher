using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia.Controls.Documents;
using System;
using System.Linq;

namespace JustLauncher
{
    public partial class ConsolePage : UserControl
    {
        public ConsolePage()
        {
            InitializeComponent();
            
            var console = this.FindControl<SelectableTextBlock>("ConsoleOutput");
            if (console != null)
            {
                console.Inlines?.Clear();
                var logs = ConsoleService.Instance.FullLog.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in logs)
                {
                    AppendFormattedLine(console, line);
                }
                ScrollToEnd();
            }

            ConsoleService.Instance.MessageLogged += OnMessageLogged;
        }

        private void OnMessageLogged(string? message)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var console = this.FindControl<SelectableTextBlock>("ConsoleOutput");
                if (console == null) return;

                if (message == null)
                {
                    console.Inlines?.Clear();
                }
                else
                {
                    AppendFormattedLine(console, message);
                    ScrollToEnd();
                }
            });
        }

        private void AppendFormattedLine(SelectableTextBlock console, string line)
        {
            if (console.Inlines == null) return;

            var timestampColor = Color.Parse("#8E9297");
            var typeColor = Color.Parse("#5865F2");
            var messageColor = Color.Parse("#FFFFFF");

            if (Application.Current?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Light)
            {
                timestampColor = Color.Parse("#4F5660");
                typeColor = Color.Parse("#404EED");
                messageColor = Color.Parse("#060607");
            }

            var tsBrush = new SolidColorBrush(timestampColor);
            var typeBrush = new SolidColorBrush(typeColor);
            var msgBrush = new SolidColorBrush(messageColor);

            int firstClose = line.IndexOf(']');
            if (firstClose > 0)
            {
                string timestamp = line.Substring(0, firstClose + 1);
                console.Inlines.Add(new Run { Text = timestamp + " ", Foreground = tsBrush });
                
                string remaining = line.Substring(firstClose + 1).TrimStart();
                int nextClose = remaining.IndexOf(']');
                if (nextClose > 0)
                {
                    string type = remaining.Substring(0, nextClose + 1);
                    var brush = typeBrush;
                    
                    if (type.Contains("ERROR")) brush = new SolidColorBrush(Color.Parse("#ED4245"));
                    else if (type.Contains("WARNING")) brush = new SolidColorBrush(Color.Parse("#FFA500"));
                    else if (type.Contains("GAME")) brush = new SolidColorBrush(Color.Parse("#5865F2"));

                    console.Inlines.Add(new Run { Text = type + " ", Foreground = brush, FontWeight = FontWeight.Bold });
                    remaining = remaining.Substring(nextClose + 1).TrimStart();
                }
                
                console.Inlines.Add(new Run { Text = remaining + "\n", Foreground = msgBrush });
            }
            else
            {
                console.Inlines.Add(new Run { Text = line + "\n", Foreground = msgBrush });
            }
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
                if (console != null)
                {
                    var topLevel = TopLevel.GetTopLevel(this);
                    if (topLevel?.Clipboard != null)
                    {
                        string logs = ConsoleService.Instance.FullLog;
                        if (!string.IsNullOrEmpty(logs))
                        {
                            await topLevel.Clipboard.SetTextAsync(logs);
                        }
                    }
                }
            };
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            ConsoleService.Instance.MessageLogged -= OnMessageLogged;
            base.OnDetachedFromVisualTree(e);
        }
    }
}
