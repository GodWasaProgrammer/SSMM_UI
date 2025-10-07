using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SSMM_UI.Messenger;
using SSMM_UI.RTMP;
using System;
using System.Windows.Input;

namespace SSMM_UI.ViewModel;

public partial class SelectedServiceViewModel : ObservableObject
{
    private readonly SelectedService? _original;
    [ObservableProperty] SelectedService? viewedService;
    [ObservableProperty] string? _displayName;
    [ObservableProperty] string? _streamKey;
    [ObservableProperty] RtmpServiceGroup? _serviceGroup;
    [ObservableProperty] RtmpServerInfo? _selectedServer;
    [ObservableProperty] bool _showServerList = false;
    [ObservableProperty] bool isActive;

    public SelectedServiceViewModel(SelectedService selection)
    {
        _original = selection;

        if(_original != null)
        {
            DisplayName = selection.DisplayName;
            StreamKey = selection.StreamKey;
            IsActive = selection.IsActive; 

            _serviceGroup = selection.ServiceGroup?.Clone();
        }
        SaveCMD = new RelayCommand(Save);
        ShowServers = new RelayCommand(() => ShowServerList = !ShowServerList);
        CancelCMD = new RelayCommand(Cancel);
    }

    // commands
    public ICommand SaveCMD { get; }
    public ICommand ShowServers { get; }
    public ICommand CancelCMD {  get; }
    public void Save()
    {
        if(_original == null) return;
        _original.StreamKey = StreamKey;
        _original.SelectedServer = SelectedServer;
        _original.IsActive = IsActive;

        // close when saving is done
        WeakReferenceMessenger.Default.Send(new CloseWindowMessage
        {
            Sender = new WeakReference(this)
        });
    }

    public void Cancel()
    {
        WeakReferenceMessenger.Default.Send(new CloseWindowMessage
        {
            Sender = new WeakReference(this)
        });
    }
}
