using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSMM_UI.Enums;
using SSMM_UI.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace SSMM_UI.ViewModel;

public partial class PurgeTokenViewModel : ObservableObject
{
    private readonly StateService _stateService;
    private readonly DialogService _dialogService;
    public PurgeTokenViewModel(ReadOnlyObservableCollection<AuthProvider> auths, StateService _stateservice, DialogService dialogService)
    {
        authProviders = auths;
        _stateService = _stateservice;
        _dialogService = dialogService;
    }
    [ObservableProperty]
    public ReadOnlyObservableCollection<AuthProvider> authProviders;

    [RelayCommand]
    private async Task PurgeSpecifiedToken(AuthProvider authProvider)
    {
        var res = _stateService.DeleteToken(authProvider);
        await _dialogService.DeleteToken(authProvider, res);
    }
}
