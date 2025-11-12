using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSMM_UI.Services;
using System.Windows.Input;

namespace SSMM_UI.ViewModel;

public partial class InspectionViewModel : ObservableObject
{
    public InspectionViewModel(VideoPlayerService vidPlayer)
    {
        // ==== Stream Inspection window ====
        ToggleReceivingStreamCommand = new RelayCommand(ToggleReceivingStream);

        // ==== Service assignment ====
        _videoPlayerService = vidPlayer;
    }

    // ==== Services ====
    private readonly VideoPlayerService _videoPlayerService;

    // == Internal stream inspection toggle ==
    public ICommand ToggleReceivingStreamCommand { get; }


    // == bool toggler for the preview window for stream ==
    [ObservableProperty] private bool isReceivingStream;

    [ObservableProperty] private string? streamButtonText = "Start Receiving";
    private void ToggleReceivingStream()
    {
        IsReceivingStream = !IsReceivingStream;
        _videoPlayerService.ToggleVisibility(IsReceivingStream);
        StreamButtonText = IsReceivingStream ? "Stop Receiving" : "Start Receiving";
    }
}