using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SSMM_UI.ViewModel;

public partial class OutputViewModel : ObservableObject
{
    public OutputViewModel(string header, string content)
    {
        Header = header;
        Content = content;
    }

    [ObservableProperty] string header;
    [ObservableProperty] string content;

    /// Take Task of stream with the output ? 
    public ObservableCollection<string> LogMessages { get; }
}
