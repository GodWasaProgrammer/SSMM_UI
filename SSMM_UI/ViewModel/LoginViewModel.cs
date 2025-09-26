using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Google.Apis.YouTube.v3;
using SSMM_UI.Services;
using SSMM_UI.Settings;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SSMM_UI.ViewModel;

public partial class LoginViewModel: ObservableObject
{
    public LoginViewModel(CentralAuthService centralAuthService, StateService stateService)
    {
        _centralAuthService = centralAuthService;
        _stateService = stateService;
        LoginWithGoogleCommand = new AsyncRelayCommand(OnLoginWithGoogleClicked);
        LoginWithKickCommand = new AsyncRelayCommand(LoginWithKick);
        LoginWithTwitchCommand = new AsyncRelayCommand(LoginWithTwitch);
        _userSettings = _stateService.UserSettingsObj;
        // Auto-login if possible
        _ = Initialize();

    }

    public async Task Initialize()
    {
        if (_userSettings != null)
        {
            if (_userSettings.SaveTokens)
            {
                await AutoLoginIfTokenized();
            }
        }
    }

    // == Login ===
    public ICommand LoginWithGoogleCommand { get; }
    public ICommand LoginWithKickCommand { get; }
    public ICommand LoginWithTwitchCommand { get; }

    // ==== Services ====
    private readonly CentralAuthService _centralAuthService;

    // Settings
    private readonly UserSettings? _userSettings;
    private readonly StateService _stateService;
    public YouTubeService? YTService;

    // ==== Login Status ====
    [ObservableProperty] private string? youtubeLoginStatus = "";
    [ObservableProperty] private string? kickLoginStatus = "";
    [ObservableProperty] private string? twitchLoginStatus = "";

    private async Task OnLoginWithGoogleClicked()
    {

        YoutubeLoginStatus = "Logging in...";

        var (userName, ytservice) = await _centralAuthService.LoginWithYoutube();
        if (ytservice != null)
        {
            YTService = ytservice;
        }
        if (userName != null)
        {
            YoutubeLoginStatus = $"✅ Logged in as {userName}";
        }
        else
        {
            YoutubeLoginStatus = "Login failed";
        }
    }

    private async Task LoginWithTwitch()
    {
        TwitchLoginStatus = "Logging in...";
        TwitchLoginStatus = await _centralAuthService.LoginWithTwitch();
    }

    private async Task LoginWithKick()
    {
        KickLoginStatus = "Logging in...";
        KickLoginStatus = await _centralAuthService.LoginWithKick();
    }

    private async Task AutoLoginIfTokenized()
    {
        var (results, ytService) = await _centralAuthService.TryAutoLoginAllAsync();

        if (ytService != null)
        {
            YTService = ytService;
        }
        foreach (var result in results)
        {
            if (result != null)
            {

                var message = result.Success
                    ? $"✅ Logged in as: {result.Username}"
                    : $"❌ {result.ErrorMessage}";

                switch (result.Provider)
                {
                    case AuthProvider.Twitch:
                        TwitchLoginStatus = message;
                        break;
                    case AuthProvider.YouTube:
                        YoutubeLoginStatus = message;
                        break;
                    case AuthProvider.Kick:
                        KickLoginStatus = message;
                        break;
                }
            }
        }
    }
}
