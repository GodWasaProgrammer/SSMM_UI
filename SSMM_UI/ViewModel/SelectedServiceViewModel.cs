using CommunityToolkit.Mvvm.ComponentModel;
using SSMM_UI.RTMP;

namespace SSMM_UI.ViewModel;

public partial class SelectedServiceViewModel : ObservableObject
{
    [ObservableProperty] SelectedService viewedService;

    public SelectedServiceViewModel(SelectedService selection)
    {
        viewedService = selection;
    }
}
