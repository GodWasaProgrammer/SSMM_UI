using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PuppeteerSharp;
using SSMM_UI.Interfaces;
using SSMM_UI.Puppeteering;
using SSMM_UI.Services;
using SSMM_UI.Settings;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SSMM_UI.ViewModel;

public partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(StateService stateService, 
                               LeftSideBarViewModel leftSideBarVM, 
                               UserSettings settings, 
                               IDialogService dialogService, 
                               LogViewModel logVM, 
                               SocialPosterViewModel socialposterVM, 
                               StreamControlViewModel streamControlVM, 
                               MetaDataViewModel metadataVM, 
                               IThemeService themeService,
                               LoginViewModel loginVM,
                               InspectionViewModel inspectionVM,
                               PuppetMaster puppeteer,
                               SocialPosterLoginViewModel socialposterLoginVM)
    {
        //Settings 
        _settings = settings;
        _dialogService = dialogService;
        OpenSetting = new AsyncRelayCommand(OpenSettings);
        SetupPuppetYT = new AsyncRelayCommand(SetupPuppetMasterYoutube);
        SetupPuppetKick = new AsyncRelayCommand(SetupPuppetMasterKick);
        OpenAbout = new AsyncRelayCommand(OpenAboutWindow);
        ShowSecretsAndKeys = new AsyncRelayCommand(ShowSecretsAndKeysDialog);

        // === start children ====
        LeftSideBarVM = leftSideBarVM;
        LogVM = logVM;
        SocialPosterVM = socialposterVM;
        StreamControlVM = streamControlVM;
        MetaDataVM = metadataVM;
        LoginVM = loginVM;
        InspectionVM = inspectionVM;
        SocialPosterLoginVM = socialposterLoginVM;

        // theme
        _themeService = themeService;
        IsDarkMode = _themeService.IsDark;

        // services
        _stateService = stateService;
        _puppeteer = puppeteer;
        // state
        _settings = _stateService.UserSettingsObj;
        //_puppeteer = new(log)
    }

    private UserSettings _settings = new();
    private readonly PuppetMaster _puppeteer;

    // ==== Theme ====
    private readonly IThemeService _themeService;
    [ObservableProperty] bool isDarkMode;

    // ==== Child Models ====
    public LeftSideBarViewModel LeftSideBarVM { get; }
    public LogViewModel LogVM { get; }
    public SocialPosterViewModel SocialPosterVM { get; }
    public StreamControlViewModel StreamControlVM { get; }
    public MetaDataViewModel MetaDataVM { get; }
    public LoginViewModel LoginVM { get; }
    public InspectionViewModel InspectionVM { get; }
    public SocialPosterLoginViewModel SocialPosterLoginVM { get; }


    // ==== Services =====
    private readonly IDialogService _dialogService;
    private readonly StateService _stateService;

    // ==== Commands ====
    public ICommand OpenSetting { get; }
    public ICommand OpenAbout { get; }
    public ICommand ToggleThemes => new RelayCommand(ToggleTheme);
    public ICommand ShowSecretsAndKeys {  get; }

    private async Task ShowSecretsAndKeysDialog()
    {
        await _dialogService.WebhooksView();
    }

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

    public ICommand SetupPuppetYT { get; }
    public ICommand SetupPuppetKick {  get; }

    private async Task SetupPuppetMasterYoutube()
    {
        await _puppeteer.ProfileSetupYoutube();
    }

    private async Task SetupPuppetMasterKick()
    {
        await _puppeteer.ProfileSetupKick();
    }
}
