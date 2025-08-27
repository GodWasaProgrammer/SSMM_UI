using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSMM_UI.Dialogs;
using SSMM_UI.RTMP;

namespace SSMM_UI.ViewModel;

public partial class ServerDetailsWindowViewModel : ObservableObject
{
    private readonly Window _window;
    private readonly Action<bool, string, RtmpServerInfo> _callback;

    public ServerDetailsWindowViewModel(RtmpServiceGroup serviceGroup, Window window, Action<bool, string, RtmpServerInfo> callback) 
    {
        _serviceGroup = serviceGroup;
        _window = window;
        _callback = callback;
    }
    [ObservableProperty] private RtmpServiceGroup _serviceGroup;
    [ObservableProperty] private RtmpServerInfo _SelectedServer;

    [RelayCommand]
    private async Task Ok()
    {
        if (SelectedServer == null)
        {
            await MessageBox.Show(_window, "Please select a server first.", "No Server Selected");
            return;
        }

        var dialog = new TextInputDialog
        {
            Title = "Enter Stream Key",
            Watermark = $"Stream key for {SelectedServer.ServerName}"
        };

        var streamKey = await dialog.ShowDialog<string>(_window);

        if (!string.IsNullOrWhiteSpace(streamKey))
        {
            _callback?.Invoke(true, streamKey, SelectedServer);
            _window.Close();
        }
        else
        {
            _callback?.Invoke(false, null, null);
            _window.Close();
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _callback?.Invoke(false, null, null);
        _window.Close();
    }
}
