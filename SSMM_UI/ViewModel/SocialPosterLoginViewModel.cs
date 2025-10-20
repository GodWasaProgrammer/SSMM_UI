using Autofac.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSMM_UI.Enums;
using SSMM_UI.Services;
using SSMM_UI.Settings;
using System.Threading.Tasks;
using System.Windows.Input;
using Tmds.DBus.Protocol;

namespace SSMM_UI.ViewModel;

public partial class SocialPosterLoginViewModel : ObservableObject
{
    public SocialPosterLoginViewModel(CentralAuthService authService, StateService stateService)
    {
        _AuthService = authService;
        _StateService = stateService;
        _settings = stateService.UserSettingsObj;
        _ = AutoLoginIfTokenized();
    }
    CentralAuthService _AuthService;
    StateService _StateService;
    UserSettings _settings;
    public ICommand LoginWithXCommand => new AsyncRelayCommand(LoginWithX);
    public ICommand LoginWithFacebook => new AsyncRelayCommand(FacebookLogin);

    public async Task FacebookLogin()
    {
        FacebookLoginStatus = await _AuthService.FacebookLogin();
    }
    private async Task LoginWithX()
    {
        var result = await _AuthService.LoginWithX();
        XLoginStatus = result;
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
    }
}