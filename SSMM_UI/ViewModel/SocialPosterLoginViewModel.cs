using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSMM_UI.Enums;
using SSMM_UI.Services;
using SSMM_UI.Settings;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SSMM_UI.ViewModel;

public partial class SocialPosterLoginViewModel : ObservableObject
{
    public SocialPosterLoginViewModel(CentralAuthService authService, StateService stateService)
    {
        _AuthService = authService;
        _StateService = stateService;
        _settings = stateService.UserSettingsObj;
        _StateService.OnAuthObjectsUpdated += RefreshStatusesFromState;
        RefreshStatusesFromState();
        _ = AutoLoginIfTokenized();
    }
    readonly CentralAuthService _AuthService;
    readonly StateService _StateService;
    readonly UserSettings _settings;
    public ICommand LoginWithXCommand => new AsyncRelayCommand(LoginWithX);
    public ICommand LoginWithFacebook => new AsyncRelayCommand(FacebookLogin);

    public async Task FacebookLogin()
    {
        FacebookLoginStatus = await _AuthService.FacebookLogin();
        RefreshStatusesFromState();
    }
    private async Task LoginWithX()
    {
        var result = await _AuthService.LoginWithX();
        XLoginStatus = result;
        RefreshStatusesFromState();
        // Handle result (e.g., update UI or log)
    }
    [ObservableProperty] string xLoginStatus = "❌ Not logged in.";
    [ObservableProperty] string facebookLoginStatus = "❌ Not logged in.";


    private async Task AutoLoginIfTokenized()
    {
        if (_settings.SaveTokens)
        {
            var results = await _AuthService.TryAutoLoginSocialMediaAsync();

            foreach (var result in results)
            {
                if (result != null)
                {

                    var message = result.Success
                        ? $"✅ Logged in as: {result.Username}"
                        : $"❌ {result.ErrorMessage}";

                    switch (result.Provider)
                    {
                        case AuthProvider.X:
                            XLoginStatus = message;
                            break;
                        case AuthProvider.Facebook:
                            FacebookLoginStatus = message;
                            break;
                    }
                }
            }
        }

        RefreshStatusesFromState();
    }

    private void RefreshStatusesFromState()
    {
        XLoginStatus = BuildLoginStatus(AuthProvider.X, "❌ Not logged in.");
        FacebookLoginStatus = BuildLoginStatus(AuthProvider.Facebook, "❌ Not logged in.");
    }

    private string BuildLoginStatus(AuthProvider provider, string defaultMessage)
    {
        if (_StateService.AuthObjects.TryGetValue(provider, out var token))
        {
            var hasUsableToken = provider == AuthProvider.Facebook
                ? !string.IsNullOrWhiteSpace(token.AccessToken)
                : token.IsValid;

            if (!hasUsableToken)
            {
                return defaultMessage;
            }

            var username = string.IsNullOrWhiteSpace(token.Username) ? provider.ToString() : token.Username;
            return $"✅ Logged in as: {username}";
        }

        return defaultMessage;
    }
}
