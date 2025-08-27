using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SSMM_UI.ViewModel;

public partial class OutputViewModel : ObservableObject
{
    public OutputViewModel(string header, Process process)
    {
        Header = header;
        StartReadingOutput(process);
    }

    [ObservableProperty] string header;
    [ObservableProperty] string content;

    /// Take Task of stream with the output ? 
    public ObservableCollection<string> LogMessages { get; }

    private void StartReadingOutput(Process process)
    {
        _ = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardError.ReadLineAsync()) != null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    LogMessages.Add($"[{DateTime.Now:HH:mm:ss}] {line}");
                    if (LogMessages.Count > 500)
                        LogMessages.RemoveAt(0);
                });
            }
        });
    }
}
