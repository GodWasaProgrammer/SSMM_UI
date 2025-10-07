using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SSMM_UI.Messenger;
using SSMM_UI.RTMP;
using SSMM_UI.Services;
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
    ILogService _logService;
    StateService _stateservice;

    public SelectedServiceViewModel(SelectedService selection, ILogService logservice, StateService stateservice)
    {
        _original = selection;
        _logService = logservice;
        _stateservice = stateservice;

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
        RemoveSelectedServiceCommand = new RelayCommand(RemoveSelectedService);
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


    // ==== Service Selections ====
    [ObservableProperty] private SelectedService? _selectedService;
    public ICommand RemoveSelectedServiceCommand { get; }
    private void RemoveSelectedService()
    {
        SelectedService = _original;
        if (SelectedService == null)
        {
            _logService.Log("No Service selected for removal");
            return;
        }

        if (!_stateservice.SelectedServicesToStream.Contains(SelectedService))
        {
            _logService.Log("The selected service doesnt exist in the list");
            return;
        }

        var serviceName = SelectedService.DisplayName;
        _stateservice.SelectedServicesToStream.Remove(SelectedService);
        _logService.Log($"Removed Service: {serviceName}");
        SelectedService = null;
        WeakReferenceMessenger.Default.Send(new CloseWindowMessage
        {
            Sender = new WeakReference(this)
        });
    }
}
