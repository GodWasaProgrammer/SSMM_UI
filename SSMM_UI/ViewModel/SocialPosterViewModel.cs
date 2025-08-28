using CommunityToolkit.Mvvm.ComponentModel;

namespace SSMM_UI.ViewModel;

public partial class SocialPosterViewModel : ObservableObject
{
    public SocialPosterViewModel() 
    {

    }

    [ObservableProperty] bool postToX;
    [ObservableProperty] bool postToFB;
    [ObservableProperty] bool postToDiscord;
}
