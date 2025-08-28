using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Google.Apis.YouTube.v3.Data;
using SSMM_UI.MetaData;
using SSMM_UI.Services;
using SSMM_UI.Settings;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SSMM_UI.ViewModel;

public partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(IFilePickerService filePickerService, CentralAuthService authservice, MetaDataService MdService, ILogService logService, StateService stateService, LeftSideBarViewModel leftSideBarViewModel, UserSettings settings, IDialogService dialogService, SearchViewModel searchmodel, LogViewModel logview)
    {
        // Init commands

        // ==== OutPut Streams ====
        StartStreamCommand = new AsyncRelayCommand(StartStream);
        StopStreamsCommand = new RelayCommand(OnStopStreams);

        // ==== testing shit ====
        TestYtHacksCommand = new AsyncRelayCommand(OnTestYtHacks);

        //Settings 
        _settings = settings;
        _dialogService = dialogService;
        OpenSetting = new AsyncRelayCommand(OpenSettings);

        // ==== Metadata Controls ====
        UploadThumbnailCommand = new AsyncRelayCommand(UploadThumbnail);
        UpdateMetadataCommand = new RelayCommand(OnUpdateMetadata);

        // === start children ====
        LeftSideBarViewModel = leftSideBarViewModel;
        SearchVM = searchmodel;
        LogVM = logview;

        // services
        MetaDataService = MdService;
        _centralAuthService = authservice;
        _filePickerService = filePickerService;
        _logService = logService;
        _streamService = new(_centralAuthService, _logService, MetaDataService);
        _stateService = stateService;
        YoutubeVideoCategories = _stateService.YoutubeVideoCategories;
        LogMessages = _logService.Messages;
        _logService.OnLogAdded = ScrollToEnd;

        // state
        _settings = _stateService.UserSettingsObj;

        SearchVM.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SearchViewModel.SelectedItem))
            {
                CurrentMetadata.TwitchCategory = SearchVM.SelectedItem;
            }
        };

        // ==== Fire and forget awaits ====
        _ = Initialize();
    }

    private UserSettings _settings = new();

    // ==== Child Models ====
    public LeftSideBarViewModel LeftSideBarViewModel { get; }
    public SearchViewModel SearchVM { get; }
    public ObservableCollection<OutputViewModel> OutputViewModels { get; }
    public LogViewModel LogVM { get; }


    // ==== Collections ====
    public ObservableCollection<VideoCategory> YoutubeVideoCategories { get; } = [];

    // === Log things ====
    [ObservableProperty] private string? _selectedLogItem;
    public ObservableCollection<string> LogMessages { get; }

    // ==== Services =====
    public MetaDataService MetaDataService { get; private set; }
    private readonly CentralAuthService _centralAuthService;
    private readonly StateService _stateService;
    private StreamService? _streamService;
    private readonly IFilePickerService _filePickerService;
    private readonly ILogService _logService;
    private readonly IDialogService _dialogService;

    // ==== RTMP Server and internal RTMP feed from OBS Status ====
    [ObservableProperty] private string serverStatusText = "Stream status: ❌ Not Receiving";
    [ObservableProperty] private string _serverStatus = "RTMP-server: ❌ Not Running";
    [ObservableProperty] private string streamStatusText = "Stream status: ❌ Not Receiving";

    // ==== Metadata ====
    [ObservableProperty] private string titleText;
    [ObservableProperty] private VideoCategory selectedYoutubeCategory;
    [ObservableProperty] private Bitmap thumbnailImage;
    [ObservableProperty] private string updateTitle;
    [ObservableProperty] private string metadataStatus;



    public void ScrollToEnd()
    {
        if (LogMessages.Count > 0)
        {
            SelectedLogItem = LogMessages[^1]; // Senaste item
        }
    }

    // ==== Commands ====

    // == Output Controls ==
    public ICommand StartStreamCommand { get; }
    public ICommand StopStreamsCommand { get; }

    public ICommand OpenSetting { get; }

    private async Task OpenSettings()
    {
        var newSettings = await _dialogService.ShowSettingsDialogAsync(_settings);
        _settings = newSettings;
        _stateService.SettingsChanged(_settings);
    }

    // == Internal stream inspection toggle ==
    public ICommand TestYtHacksCommand { get; }

    // == Metadata Selections ==
    public ICommand UploadThumbnailCommand { get; }
    public ICommand UpdateMetadataCommand { get; }

    // == bool toggler for stopping your output streams ==
    [ObservableProperty] private bool canStopStream = false;
    [ObservableProperty] private bool canStartStream = true;

    // ==== Social Media Poster ====
    [ObservableProperty] private bool postToDiscord;
    [ObservableProperty] private bool postToFacebook;
    [ObservableProperty] private bool postToX;


    public StreamMetadata CurrentMetadata { get; set; } = new StreamMetadata();

    private async Task Initialize()
    {
        if (_settings.PollStream && _settings.PollServer)
        {
            SubscribeToEvents();
            if (_streamService != null)
                _streamService.StartPolling();
        }
        else
        {
            ServerStatusText = "Polling is turned off for RTMP Server";
            StreamStatusText = "Polling is turned off for incoming Stream";
        }
    }

    private void SubscribeToEvents()
    {
        try
        {
            if (_streamService != null)
            {
                _streamService.ServerStatusChanged += isAlive =>
                    ServerStatusText = isAlive ? "RTMP-server: ✅ Running" : "RTMP-server: ❌ Not Running";

                _streamService.StreamStatusChanged += isAlive =>
                    StreamStatusText = isAlive ? "Stream status: ✅ Live" : "Stream status: ❌ Not Receiving";
            }
            else
            {
                throw new Exception("streamservice was null");
            }
        }
        catch (Exception ex)
        {
            _logService.Log(ex.Message);
        }
    }

    private void OnStopStreams()
    {
        try
        {
            if (_streamService != null)
                _streamService.StopStreams();
            _logService.Log("Stopped all streams.");
            CanStartStream = true;
            CanStopStream = false;
        }
        catch (Exception ex)
        {
            _logService.Log(ex.ToString());
        }
    }

    public async Task OnTestYtHacks()
    {
        //YT Puppeteeer
        //var videoId = "rJZZqhvgQ1A";
        //var userDataDirPath = Path.Combine(
        //Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        //"Google",
        //"Chrome",
        //"User Data"
        //);
        //var chromeExePath = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
        //var defaultProfilePath = Path.Combine(userDataDirPath, "Default");

        //await YoutubeStudioPuppeteer.ChangeGameTitle(videoId, defaultProfilePath, chromeExePath);

        //var puppy = new KickPuppeteer();
        //await puppy.SetGameTitleKick("Hearts Of Iron IV");
        //await MetaDataService.SetTwitchTitleAndCategory("SSMM_RULES_THE_DAY", "Hearts Of Iron IV");
        //MetaDataService.CreateYouTubeService(LeftSideBarViewModel.YTService);
        //await MetaDataService.SetTitleAndCategoryYoutubeAsync("G5Ko1fLdMFM","SSMM_RULES_THE_DAY", 20);
    }

    private async Task StartStream()
    {
        CanStartStream = false;
        CanStopStream = true;
        if (_streamService != null)
        {
            try
            {
                if (LeftSideBarViewModel.YTService != null)
                {
                    _streamService.CreateYTService(LeftSideBarViewModel.YTService);
                    MetaDataService.CreateYouTubeService(LeftSideBarViewModel.YTService);
                }

                await _streamService.StartStream(CurrentMetadata, LeftSideBarViewModel.SelectedServicesToStream);
                _logService.Log("Started streaming...");
            }
            catch (Exception ex)
            {
                _logService.Log(ex.ToString());
            }
            var bla = _streamService.ProcessInfos;
            LogVM.StreamOutputVM.Outputs.Clear();
            foreach (var info in bla)
            {
                var outputview = new OutputViewModel(info.Header, info.Process);
               LogVM.StreamOutputVM.Outputs.Add(outputview);
            }
        }
    }

    private async Task UploadThumbnail()
    {
        try
        {
            var result = await _filePickerService.PickImageAsync();

            if (result != null)
            {
                var (bitmap, path) = result.Value;
                if (bitmap != null)
                {
                    ThumbnailImage = bitmap;
                    if (path != null)
                        CurrentMetadata.ThumbnailPath = path;
                    CurrentMetadata.Thumbnail = ThumbnailImage;
                    _logService.Log("Thumbnail loaded successfully");
                }
                else
                {
                    _logService.Log("No file selected");
                }
            }
        }
        catch (Exception ex)
        {
            _logService.Log($"Error loading thumbnail: {ex.Message}");
        }
    }


    private void OnUpdateMetadata()
    {
        CurrentMetadata.Title = TitleText;
        _logService.Log($"Updated metadata: Title={TitleText}");
        MetadataStatus = "Metadata updated successfully.";
    }
}
