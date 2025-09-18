using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSMM_UI.Interfaces;
using SSMM_UI.Puppeteering;
using SSMM_UI.Services;
using SSMM_UI.Settings;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SSMM_UI.ViewModel;

public partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(StateService stateService, LeftSideBarViewModel leftSideBarVM, UserSettings settings, IDialogService
        dialogService, LogViewModel logVM, SocialPosterViewModel socialposterVM, StreamControlViewModel streamControlVM, MetaDataViewModel metadataVM, IThemeService themeService)
    {
        //Settings 
        _settings = settings;
        _dialogService = dialogService;
        OpenSetting = new AsyncRelayCommand(OpenSettings);
        SetupPuppet = new AsyncRelayCommand(SetupPuppetMaster);
        OpenAbout = new AsyncRelayCommand(OpenAboutWindow);

        // === start children ====
        LeftSideBarVM = leftSideBarVM;
        LogVM = logVM;
        SocialPosterVM = socialposterVM;
        StreamControlVM = streamControlVM;
        MetaDataVM = metadataVM;

        // theme
        _themeService = themeService;
        IsDarkMode = _themeService.IsDark;

        // services
        _stateService = stateService;
        // state
        _settings = _stateService.UserSettingsObj;
    }

    private UserSettings _settings = new();

    // ==== Theme ====
    private IThemeService _themeService;
    [ObservableProperty] bool isDarkMode;

    // ==== Child Models ====
    public LeftSideBarViewModel LeftSideBarVM { get; }
    public LogViewModel LogVM { get; }
    public SocialPosterViewModel SocialPosterVM { get; }
    public StreamControlViewModel StreamControlVM { get; }
    public MetaDataViewModel MetaDataVM { get; }


    // ==== Services =====
    private readonly IDialogService _dialogService;
    private readonly StateService _stateService;

    // ==== Commands ====
    public ICommand OpenSetting { get; }
    public ICommand OpenAbout { get; }

    public ICommand ToggleThemes => new RelayCommand(ToggleTheme);

    private void ToggleTheme()
    {
        _themeService.ToggleTheme();
        IsDarkMode = _themeService.IsDark;
    }

    private async Task OpenAboutWindow()
    {
        await _dialogService.About();
    }
    private async Task OpenSettings()
    {
        var newSettings = await _dialogService.ShowSettingsDialogAsync(_settings);
        _settings = newSettings;
        _stateService.SettingsChanged(_settings);
    }

    public ICommand SetupPuppet { get; }

    private async Task SetupPuppetMaster()
    {
        await PuppetMaster.ProfileSetupYoutube();
    }
}
