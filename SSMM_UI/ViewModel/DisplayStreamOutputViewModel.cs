using System.Collections.ObjectModel;

namespace SSMM_UI.ViewModel;

public class DisplayStreamOutputViewModel
{
    public ObservableCollection<OutputViewModel>? Outputs { get; set; }

    public DisplayStreamOutputViewModel()
    {
        Outputs = [];
    }
}