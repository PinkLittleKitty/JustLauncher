using System;
using System.Collections.Generic;
using System.Text;
using Avalonia.Threading;

namespace JustLauncher;

public class ConsoleService
{
    private static readonly Lazy<ConsoleService> _instance = new(() => new ConsoleService());
    public static ConsoleService Instance => _instance.Value;

    private readonly List<string> _logs = new();
    private readonly StringBuilder _buffer = new();
    public string FullLog => _buffer.ToString();

    public event Action<string>? MessageLogged;

    private ConsoleService() { }

    public void Log(string message)
    {
        string timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _logs.Add(timestamped);
        _buffer.AppendLine(timestamped);

        Dispatcher.UIThread.Post(() => MessageLogged?.Invoke(timestamped));
    }

    public void Clear()
    {
        _logs.Clear();
        _buffer.Clear();
        Dispatcher.UIThread.Post(() => MessageLogged?.Invoke(null!));
    }
}
