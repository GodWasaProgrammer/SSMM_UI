using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSMM_UI.Services;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SSMM_UI.ViewModel;

public partial class SocialPosterLoginViewModel : ObservableObject
{
    public SocialPosterLoginViewModel(CentralAuthService authService)
    {
        _AuthService = authService;
    }
    CentralAuthService _AuthService;

    public ICommand LoginWithXCommand => new AsyncRelayCommand(LoginWithX);
    private async Task LoginWithX()
    {
        var result = await _AuthService.LoginWithX();
        XLoginStatus = result;
        // Handle result (e.g., update UI or log)
    }
    [ObservableProperty] string xLoginStatus = "Not logged in.";

}
