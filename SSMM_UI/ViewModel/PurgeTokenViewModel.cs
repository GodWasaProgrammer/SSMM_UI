using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSMM_UI.Enums;
using SSMM_UI.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace SSMM_UI.ViewModel;

public partial class PurgeTokenViewModel : ObservableObject
{
    Window mainWindow;
    private StateService _stateService;
    public PurgeTokenViewModel(Window window, ObservableCollection<AuthProvider> auths, StateService _stateservice)
    {
        mainWindow = window;
        authProviders = auths;
        _stateService = _stateservice;
        authProviders ??= [];
    }
    [ObservableProperty]
    public ObservableCollection<AuthProvider> authProviders;

    [RelayCommand]
    private void PurgeSpecifiedToken(AuthProvider authProvider)
    {
        _stateService.DeleteToken(authProvider);
    }
}