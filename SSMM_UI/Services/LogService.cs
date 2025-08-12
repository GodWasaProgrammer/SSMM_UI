using System;
using System.Collections.ObjectModel;

namespace SSMM_UI.Services;

public static class LogService
{
    public static ObservableCollection<string> Messages { get; } = [];

    public static void Log(string message)
    {
        var timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";
        Messages.Add(timestamped);

        // Behåll inte oändligt många rader
        if (Messages.Count > 500)
            Messages.RemoveAt(0);
    }
}
