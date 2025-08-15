using Avalonia.Interactivity;
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

namespace SSMM_UI;

public partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(IDialogService dialogService, IFilePickerService filePickerService, VideoPlayerService vidPlayer, CentralAuthService authservice, MetaDataService MdService, ILogService logService, StateService stateService)
    {
        // Init commands

        // ==== Login ====
        LoginWithGoogleCommand = new AsyncRelayCommand(OnLoginWithGoogleClicked);
        LoginWithKickCommand = new AsyncRelayCommand(LoginWithKick);
        LoginWithTwitchCommand = new AsyncRelayCommand(LoginWithTwitch);

        // ==== OutPut Streams ====
        StartStreamCommand = new RelayCommand(StartStream);
        StopStreamsCommand = new RelayCommand(OnStopStreams);

        // ==== Stream Inspection window ====
        ToggleReceivingStreamCommand = new RelayCommand(ToggleReceivingStream);

        // ==== testing shit ====
        TestYtHacksCommand = new RelayCommand(OnTestYtHacks);

        // ==== Selected Services controls ====
        AddServiceCommand = new AsyncRelayCommand<RtmpServiceGroup>(OnRTMPServiceSelected);
        RemoveSelectedServiceCommand = new RelayCommand(RemoveSelectedService);

        // ==== Metadata Controls ====
        UploadThumbnailCommand = new AsyncRelayCommand(UploadThumbnail);
        UpdateMetadataCommand = new RelayCommand(OnUpdateMetadata);

        // services
        MetaDataService = MdService;
        _centralAuthService = authservice;
        _filePickerService = filePickerService;
        _dialogService = dialogService;
        _videoPlayerService = vidPlayer;
        _logService = logService;
        _streamService = new(_centralAuthService, _logService);
        _stateService = stateService;
        // set state of lists
        RtmpServiceGroups = _stateService.RtmpServiceGroups;
        SelectedServicesToStream = _stateService.SelectedServicesToStream;
        YoutubeVideoCategories = _stateService.YoutubeVideoCategories;
        LogMessages = _logService.Messages;
        _logService.OnLogAdded = ScrollToEnd;
        // ==== Fire and forget awaits ====
        _ = Initialize();
    }

    // ==== Collections ====
    public ObservableCollection<RtmpServiceGroup> RtmpServiceGroups { get; } = [];
    public ObservableCollection<SelectedService> SelectedServicesToStream { get; } = [];
    public ObservableCollection<VideoCategory> YoutubeVideoCategories { get; } = [];

    public ObservableCollection<string> LogMessages { get; }

    // ==== Services =====
    public MetaDataService MetaDataService { get; private set; }
    private readonly CentralAuthService _centralAuthService;
    private readonly StateService _stateService;
    private StreamService? _streamService;
    private readonly IFilePickerService _filePickerService;
    private readonly VideoPlayerService _videoPlayerService;
    private readonly IDialogService _dialogService;
    private YouTubeService YTService;
    private readonly ILogService _logService;

    // ==== Service Selections ====
    [ObservableProperty]
    private SelectedService? _selectedService;

    // ==== RTMP Server and internal RTMP feed from OBS Status ====
    [ObservableProperty] private string serverStatusText = "Stream status: ❌ Not Receiving";
    [ObservableProperty] private string _serverStatus = "RTMP-server: ❌ Not Running";
    [ObservableProperty] private string streamStatusText = "Stream status: ❌ Not Receiving";
    [ObservableProperty] private string streamButtonText = "Start Receiving";

    // ==== Login Status ====
    [ObservableProperty] private string youtubeLoginStatus;
    [ObservableProperty] private string kickLoginStatus;
    [ObservableProperty] private string twitchLoginStatus;

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

    // == Login ===
    public ICommand LoginWithGoogleCommand { get; }
    public ICommand LoginWithKickCommand { get; }
    public ICommand LoginWithTwitchCommand { get; }

    // == Output Controls ==
    public ICommand StartStreamCommand { get; }
    public ICommand StopStreamsCommand { get; }

    // == Internal stream inspection toggle ==
    public ICommand ToggleReceivingStreamCommand { get; }
    public ICommand TestYtHacksCommand { get; }

    // == Service Selections ==
    public IAsyncRelayCommand<RtmpServiceGroup> AddServiceCommand { get; }
    public ICommand RemoveSelectedServiceCommand { get; }

    // == Metadata Selections ==
    public ICommand UploadThumbnailCommand { get; }
    public ICommand UpdateMetadataCommand { get; }

    // == bool toggler for stopping your output streams ==
    [ObservableProperty] private bool canStopStream = false;
    [ObservableProperty] private bool canStartStream = true;

    // == bool toggler for the preview window for stream ==
    [ObservableProperty] private bool isReceivingStream;

    // ==== Social Media Poster ====
    [ObservableProperty] private bool postToDiscord;
    [ObservableProperty] private bool postToFacebook;
    [ObservableProperty] private bool postToX;
    [ObservableProperty] private RtmpServiceGroup selectedRtmpService;

    public StreamMetadata CurrentMetadata { get; set; } = new StreamMetadata();

    private async Task Initialize()
    {
        await AutoLoginIfTokenized();
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

    private async Task OnRTMPServiceSelected(RtmpServiceGroup group)
    {
        var result = await _dialogService.ShowServerDetailsAsync(group);

        if (!result)
            _logService.Log($"Cancelled adding service: {group.ServiceName}\n");
    }

    private void ToggleReceivingStream()
    {
        IsReceivingStream = !IsReceivingStream;
        _videoPlayerService.ToggleVisibility(IsReceivingStream);
        StreamButtonText = IsReceivingStream ? "Stop Receiving" : "Start Receiving";
    }

    private async Task OnLoginWithGoogleClicked()
    {

        YoutubeLoginStatus = "Loggar in...";

        var (userName, ytservice) = await _centralAuthService.LoginWithYoutube();
        if(ytservice != null)
        {
            YTService = ytservice;
        }
        if (userName != null)
        {
            YoutubeLoginStatus = $"✅ Inloggad som {userName}";
        }
        else
        {
            YoutubeLoginStatus = "Inloggning misslyckades";
        }
    }

    private async Task LoginWithTwitch()
    {
        TwitchLoginStatus = "Logging in...";
        TwitchLoginStatus = await _centralAuthService.LoginWithTwitch();
    }

    private async Task LoginWithKick()
    {
        KickLoginStatus = "Logging in...";
        KickLoginStatus = await _centralAuthService.LoginWithKick();
    }

    private async Task AutoLoginIfTokenized()
    {
        var (results, ytService)= await _centralAuthService.TryAutoLoginAllAsync();

        if (ytService != null) 
        {
            YTService = ytService;
        }
        foreach (var result in results)
        {
            var message = result.Success
                ? $"✅ Logged in as: {result.Username}"
                : $"❌ {result.ErrorMessage}";

            switch (result.Provider)
            {
                case AuthProvider.Twitch:
                    TwitchLoginStatus = message;
                    break;
                case AuthProvider.YouTube:
                    YoutubeLoginStatus = message;
                    break;
                case AuthProvider.Kick:
                    KickLoginStatus = message;
                    break;
            }
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
        catch(Exception ex)
        {
            _logService.Log(ex.ToString());
        }
    }

    private void OnTestYtHacks()
    {
        _logService.Log("Tested YouTube Hacks.");
    }

    private void StartStream()
    {
        CanStartStream = false;
        CanStopStream = true;
        if (_streamService != null)
        {
            try
            {
                _streamService.CreateYTService(YTService);
                _streamService.StartStream(CurrentMetadata, SelectedServicesToStream);
                _logService.Log("Started streaming...");
            }
            catch (Exception ex)
            {
                _logService.Log(ex.ToString());
            }
        }
    }

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

    private void RemoveSelectedService()
    {
        if (SelectedService == null)
        {
            _logService.Log("Ingen tjänst vald att ta bort");
            return;
        }

        if (!SelectedServicesToStream.Contains(SelectedService))
        {
            _logService.Log("Den valda tjänsten finns inte i listan");
            return;
        }

        var serviceName = SelectedService.DisplayName;
        SelectedServicesToStream.Remove(SelectedService);
        _logService.Log($"Tog bort tjänst: {serviceName}");
        SelectedService = null;
    }
}
