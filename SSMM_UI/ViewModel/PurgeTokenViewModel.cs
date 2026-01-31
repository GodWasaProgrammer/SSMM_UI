using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using SSMM_UI.Enums;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SSMM_UI.ViewModel;

public partial class PurgeTokenViewModel : ObservableObject
{
    Window mainWindow;
    
    public PurgeTokenViewModel(Window window, ObservableCollection<AuthProvider> auths)
    {
        mainWindow = window;
        authProviders = auths;
        authProviders ??= [];
    }
    [ObservableProperty]
    public ObservableCollection<AuthProvider> authProviders;

}