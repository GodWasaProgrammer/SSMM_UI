using System;
using System.Collections.ObjectModel;
using Avalonia.Threading;

namespace SSMM_UI.Services;

public interface ILogService
{
    void Log(string message);
    ObservableCollection<string> Messages { get; }
    public Action? OnLogAdded { get; set; }
}

// 2. Implementera
public class LogService : ILogService
{
    public ObservableCollection<string> Messages { get; } = new();
    public Action? OnLogAdded { get; set; }

    public void Log(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Messages.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            if (Messages.Count > 500) Messages.RemoveAt(0);
            OnLogAdded?.Invoke();
        });
    }
}