using Autofac.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSMM_UI.Enums;
using SSMM_UI.Services;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SSMM_UI.ViewModel;

public partial class SocialPosterLoginViewModel : ObservableObject
{
    public SocialPosterLoginViewModel(CentralAuthService authService)
    {
        _AuthService = authService;
        _ = AutoLoginIfTokenized();
    }
    CentralAuthService _AuthService;

    public ICommand LoginWithXCommand => new AsyncRelayCommand(LoginWithX);
    public ICommand LoginWithFacebook => new AsyncRelayCommand(FacebookLogin);

    public async Task FacebookLogin()
    {
        await _AuthService.FacebookLogin();
    }
    private async Task LoginWithX()
    {
        var result = await _AuthService.LoginWithX();
        XLoginStatus = result;
        // Handle result (e.g., update UI or log)
    }
    [ObservableProperty] string xLoginStatus = "Not logged in.";
    [ObservableProperty] string facebookLoginStatus = "Not logged in.";


    private async Task AutoLoginIfTokenized()
    {
        var results = await _AuthService.TryAutoLoginXAsync();

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
                }
            }
        }
    }

}
