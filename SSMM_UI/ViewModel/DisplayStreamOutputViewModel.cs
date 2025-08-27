using System.Collections.ObjectModel;

namespace SSMM_UI.ViewModel;

public class DisplayStreamOutputViewModel
{
    public ObservableCollection<OutputViewModel>? Outputs { get; set; }

    public DisplayStreamOutputViewModel()
    {
        Outputs = new ObservableCollection<OutputViewModel>();
        Outputs.Add(new OutputViewModel("Example", "test"));
        Outputs.Add(new OutputViewModel("Example", "test"));
        Outputs.Add(new OutputViewModel("Example", "test"));
    }
}
