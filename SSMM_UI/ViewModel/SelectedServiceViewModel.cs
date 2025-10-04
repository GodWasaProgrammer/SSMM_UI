using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSMM_UI.RTMP;
using System.ComponentModel;
using System.Threading.Tasks;
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

    public SelectedServiceViewModel(SelectedService selection)
    {
        _original = selection;

        if(_original != null)
        {
            DisplayName = selection.DisplayName;
            StreamKey = selection.StreamKey;

            _serviceGroup = selection.ServiceGroup?.Clone();
        }
        SaveCMD = new RelayCommand(Save);
        ShowServers = new RelayCommand(() => ShowServerList = !ShowServerList);
    }

    // commands
    public ICommand SaveCMD { get; }
    public ICommand ShowServers { get; }
    public void Save()
    {
        if(_original == null) return;
        _original.StreamKey = StreamKey;
        _original.SelectedServer = SelectedServer;
    }

    public void Cancel()
    {

    }
}
