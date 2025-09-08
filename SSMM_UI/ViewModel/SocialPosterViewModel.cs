using CommunityToolkit.Mvvm.ComponentModel;
using SSMM_UI.Services;
using SSMM_UI.Settings;
using Tweetinvi.Core.Models;

namespace SSMM_UI.ViewModel;

public partial class SocialPosterViewModel : ObservableObject
{
    private readonly UserSettings _settings;

    public SocialPosterViewModel(UserSettings settings) 
    {
        _settings = settings;
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
}
