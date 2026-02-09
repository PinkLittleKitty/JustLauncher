using System;

namespace JustLauncher.Models;

public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error
}

public class NotificationModel
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; } = NotificationType.Info;
    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(5);
}
