using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSMM_UI.Services;
using SSMM_UI.Settings;

namespace SSMM_UI.ViewModel;

public partial class SocialPosterViewModel : ObservableObject
{
    private readonly UserSettings _settings;
    private readonly SocialPosterService _poster;
    public SocialPosterViewModel(UserSettings settings, SocialPosterService poster) 
    {
        _settings = settings;
        _poster = poster;
        TestClick = new AsyncRelayCommand(Test);
    }

    [ObservableProperty] bool postToX;
    [ObservableProperty] bool postToFB;
    [ObservableProperty] bool postToDiscord;

    partial void OnPostToXChanged(bool value)
    {
        _settings.PostToX = value;
    }

    partial void OnPostToFBChanged(bool value)
    {
        _settings.PostToFB = value;
    }

    partial void OnPostToDiscordChanged(bool value)
    {
        _settings.PostToDiscord = value;
    }

    public ICommand TestClick { get; }

    public async Task Test()
    {
        await _poster.RunPoster(PostToDiscord, PostToFB, PostToX);
    }
}
