using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using SSMM_UI.MetaData;
using SSMM_UI.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SSMM_UI.ViewModel;

public partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(IFilePickerService filePickerService, CentralAuthService authservice, MetaDataService MdService, ILogService logService, StateService stateService, LeftSideBarViewModel leftSideBarViewModel)
    {
        // Init commands

        // ==== OutPut Streams ====
        //StartStreamCommand = new RelayCommand(StartStream);
        StopStreamsCommand = new RelayCommand(OnStopStreams);

        // ==== testing shit ====
        TestYtHacksCommand = new RelayCommand(OnTestYtHacks);

        // ==== Metadata Controls ====
        UploadThumbnailCommand = new AsyncRelayCommand(UploadThumbnail);
        UpdateMetadataCommand = new RelayCommand(OnUpdateMetadata);

        // === start children ====
        LeftSideBarViewModel = leftSideBarViewModel;

        // services
        MetaDataService = MdService;
        _centralAuthService = authservice;
        _filePickerService = filePickerService;
        _logService = logService;
        _streamService = new(_centralAuthService, _logService);
        _stateService = stateService;
        YoutubeVideoCategories = _stateService.YoutubeVideoCategories;
        LogMessages = _logService.Messages;
        _logService.OnLogAdded = ScrollToEnd;
        // ==== Fire and forget awaits ====
        _ = Initialize();
    }

    // ==== Child Models ====
    public LeftSideBarViewModel LeftSideBarViewModel { get; }

    // ==== Collections ====
    public ObservableCollection<VideoCategory> YoutubeVideoCategories { get; } = [];

    public ObservableCollection<string> LogMessages { get; }

    // ==== Services =====
    public MetaDataService MetaDataService { get; private set; }
    private readonly CentralAuthService _centralAuthService;
    private readonly StateService _stateService;
    private StreamService? _streamService;
    private readonly IFilePickerService _filePickerService;
    private YouTubeService YTService;
    private readonly ILogService _logService;

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

    // === Log things ====
    [ObservableProperty] private string? _selectedLogItem;

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
        SubscribeToEvents();
        if (_streamService != null)
            _streamService.StartPolling();

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

    private void OnTestYtHacks()
    {
        _logService.Log("Tested YouTube Hacks.");
    }

    //private void StartStream()
    //{
    //    CanStartStream = false;
    //    CanStopStream = true;
    //    if (_streamService != null)
    //    {
    //        try
    //        {
    //            _streamService.CreateYTService(YTService);
    //            _streamService.StartStream(CurrentMetadata, SelectedServicesToStream);
    //            _logService.Log("Started streaming...");
    //        }
    //        catch (Exception ex)
    //        {
    //            _logService.Log(ex.ToString());
    //        }
    //    }
    //}

    private async Task UploadThumbnail()
    {
        try
        {
            var bitmap = await _filePickerService.PickImageAsync();

            if (bitmap != null)
            {
                ThumbnailImage = bitmap;
                CurrentMetadata.Thumbnail = ThumbnailImage;
                _logService.Log("Thumbnail loaded successfully");
            }
            else
            {
                _logService.Log("No file selected");
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
