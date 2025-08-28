using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SSMM_UI.Services;

namespace SSMM_UI.ViewModel;

public partial class LogViewModel : ObservableObject
{
    private readonly ILogService _logService;

    public LogViewModel(ILogService logService, DisplayStreamOutputViewModel StreamOutPutDisplay)
    {
        _logService = logService;
        _streamOutputVM = StreamOutPutDisplay;
        _logService.OnLogAdded = ScrollToEnd;
    }

    public ObservableCollection<string> LogMessages { get; } = new ObservableCollection<string>();

    [ObservableProperty] string? _selectedLogItem;
    [ObservableProperty] DisplayStreamOutputViewModel? _streamOutputVM;

    public void ScrollToEnd()
    {
        if ( LogMessages.Count > 0)
        {
            SelectedLogItem = LogMessages[^1];
        }
    }
}
