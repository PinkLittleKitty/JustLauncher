using System;
using System.Collections.ObjectModel;
using JustLauncher.Models;

namespace JustLauncher.Services;

public class NotificationService
{
    private static NotificationService? _instance;
    public static NotificationService Instance => _instance ??= new NotificationService();

    public ObservableCollection<NotificationModel> ActiveNotifications { get; } = new();

    public void Show(string title, string message, NotificationType type = NotificationType.Info, int durationSeconds = 5)
    {
        var notification = new NotificationModel
        {
            Title = title,
            Message = message,
            Type = type,
            Duration = TimeSpan.FromSeconds(durationSeconds)
        };

        ActiveNotifications.Add(notification);
    }

    public void ShowInfo(string title, string message) => Show(title, message, NotificationType.Info);
    public void ShowSuccess(string title, string message) => Show(title, message, NotificationType.Success);
    public void ShowWarning(string title, string message) => Show(title, message, NotificationType.Warning);
    public void ShowError(string title, string message) => Show(title, message, NotificationType.Error);

    public void Remove(NotificationModel notification)
    {
        ActiveNotifications.Remove(notification);
    }
}
