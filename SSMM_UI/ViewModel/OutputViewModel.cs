using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SSMM_UI.ViewModel;

public partial class OutputViewModel : ObservableObject, IDisposable
{
    public OutputViewModel(string header, Process process)
    {
        Header = header;
        StartReadingOutput(process, _cts.Token);
        localProcess = process;
    }

    [ObservableProperty] string? header;
    [ObservableProperty] string? content;
    [ObservableProperty] string? selectedMessage;
    private Task? _processreader;
    private readonly CancellationTokenSource _cts = new();
    private Process localProcess;
    public ICommand RestartProcessCMD => new AsyncRelayCommand (RestartProcess);
    private string restarted = string.Empty;

    /// Take Task of stream with the output ? 
    public ObservableCollection<string> LogMessages { get; } = [];

    private async Task RestartProcess()
    {
        localProcess.Kill();
        var success = localProcess.Start();
        if (success) 
        {

        }
    }

    private void StartReadingOutput(Process process, CancellationToken token)
    {
        _processreader = Task.Run(async () =>
        {
            try
            {
                string? line;
                while (!token.IsCancellationRequested &&
                       (line = await process.StandardError.ReadLineAsync()) != null)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        LogMessages.Add($"[{DateTime.Now:HH:mm:ss}] {line}");
                        ScrollToEnd();
                        if (LogMessages.Count > 500)
                            LogMessages.RemoveAt(0);
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Output read stopped: {ex.Message}");
            }
        }, token);
    }

    public void ScrollToEnd()
    {
        if (LogMessages.Count > 0)
        {
            SelectedMessage = LogMessages[^1]; // Senaste item
        }
    }

    public void Dispose()
    {
        try
        {
            _cts.Cancel();
            if (_processreader != null)
            {
                try
                {
                    _processreader.Wait(500); 
                }
                catch (AggregateException) { }
            }
        }
        finally
        {
            _cts.Dispose();
        }
    }
}