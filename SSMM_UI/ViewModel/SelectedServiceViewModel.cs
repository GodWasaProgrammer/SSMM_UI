using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSMM_UI.RTMP;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SSMM_UI.ViewModel;

public partial class SelectedServiceViewModel : ObservableObject
{
    private readonly SelectedService _original;
    [ObservableProperty] SelectedService? viewedService;
    [ObservableProperty] string? _displayName;
    [ObservableProperty] string? _streamKey;
    [ObservableProperty] RtmpServiceGroup? _serviceGroup;

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
    }

    public ICommand SaveCMD { get; }

    public void Save()
    {
        _original.StreamKey = StreamKey;
    }

    public void Cancel()
    {

    }
}
