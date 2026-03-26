using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSMM_UI.Interfaces;
using SSMM_UI.Services;
using SSMM_UI.Settings;

namespace SSMM_UI.ViewModel;

public partial class SocialPosterViewModel : ObservableObject
{
    private readonly UserSettings _settings;
    private readonly IDialogService _dialogService;

    public SocialPosterViewModel(StateService stateService, IDialogService dialogService)
    {
        _dialogService = dialogService;
        _settings = stateService.UserSettingsObj;

        PostToX = _settings.PostToX;
        PostToFB = _settings.PostToFB;
        PostToDiscord = _settings.PostToDiscord;
        AutoPost = _settings.AutoPost;
        CustomMessage = _settings.CustomSocialMessage ?? string.Empty;

        EditMessageCommand = new AsyncRelayCommand(EditMessageAsync);
    }

    [ObservableProperty] bool postToX;
    [ObservableProperty] bool postToFB;
    [ObservableProperty] bool postToDiscord;
    [ObservableProperty] bool autoPost;
    [ObservableProperty] string customMessage = string.Empty;

    partial void OnPostToXChanged(bool value) => _settings.PostToX = value;

    partial void OnPostToFBChanged(bool value) => _settings.PostToFB = value;

    partial void OnPostToDiscordChanged(bool value) => _settings.PostToDiscord = value;

    partial void OnAutoPostChanged(bool value) => _settings.AutoPost = value;

    partial void OnCustomMessageChanged(string value)
    {
        _settings.CustomSocialMessage = value;
        OnPropertyChanged(nameof(CustomMessagePreview));
    }

    public string CustomMessagePreview =>
        string.IsNullOrWhiteSpace(CustomMessage)
            ? "Using auto-generated live announcement."
            : CustomMessage;

    public ICommand EditMessageCommand { get; }

    private async Task EditMessageAsync()
    {
        var result = await _dialogService.EditSocialPostMessageAsync(CustomMessage);
        if (result != null)
        {
            CustomMessage = result;
        }
    }
}
