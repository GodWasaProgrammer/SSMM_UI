using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SSMM_UI.ViewModel;

public partial class SettingsViewModel : ObservableObject
{
    public SettingsViewModel()
    {
        SaveSettingsCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(Cancel);
    }

    [ObservableProperty] private bool serverPolling;
    [ObservableProperty] private bool streamFeedPolling;
    [ObservableProperty] private bool saveTokens;
    [ObservableProperty] private bool saveMetaData;
    [ObservableProperty] private bool saveServices;

    public ICommand SaveSettingsCommand { get; }
    public ICommand CancelCommand { get; }
    private Window? _hostWindow;

    public void SetHostWindow(Window window)
    {
        _hostWindow = window;
    }

    private void Save()
    {
        _hostWindow?.Close(true);
    }

    private void Cancel()
    {
        _hostWindow?.Close(false);
    }
}