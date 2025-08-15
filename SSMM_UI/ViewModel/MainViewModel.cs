using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    // ==== Collections ====
    public ObservableCollection<RtmpServiceGroup> RtmpServiceGroups { get; } = [];
    public ObservableCollection<SelectedService> SelectedServicesToStream { get; } = [];
    public ObservableCollection<VideoCategory> YoutubeVideoCategories { get; } = [];

    public ObservableCollection<string> LogMessages { get; } = [];

    // ==== Services =====
    public MetaDataService MetaDataService { get; private set; }
    private readonly CentralAuthService _centralAuthService;
    private readonly StateService _stateService = new();
    private StreamService? _streamService;
    private readonly IFilePickerService _filePickerService;
    private readonly VideoPlayerService _videoPlayerService;
    private readonly IDialogService _dialogService;

    // ==== Service Selections ====
    [ObservableProperty]
    private SelectedService? _selectedService;

    // ==== Stream Status ====
    [ObservableProperty] private string streamStatusText = "Stream status: ❌ Not Receiving";
    [ObservableProperty] private string serverStatusText = "Stream status: ❌ Not Receiving";
    [ObservableProperty] private string streamButtonText = "Start Receiving";

    // ==== Login Status ====
    [ObservableProperty] private string youtubeLoginStatus;
    [ObservableProperty] private string kickLoginStatus;
    [ObservableProperty] private string twitchLoginStatus;

    // ==== Metadata ====
    [ObservableProperty] private string titleText;
    [ObservableProperty] private VideoCategory selectedYoutubeCategory;
    [ObservableProperty] private Bitmap thumbnailImage;
    [ObservableProperty] private string statusTextBlock;

    // ==== Commands ====
    public ICommand LoginWithGoogleCommand { get; }
    public ICommand LoginWithKickCommand { get; }
    public ICommand LoginWithTwitchCommand { get; }

    // bool toggler for stopping your output streams
    public bool CanStopStream { get; set; }



    // bool toggler for the preview window for stream
    [ObservableProperty] private bool isReceivingStream;

    public bool PostToDiscord { get; set; }
    public bool PostToFacebook { get; set; }
    public bool PostToX { get; set; }

    public string StreamTitle { get; set; }
    public string MetadataStatus { get; set; }

    public RtmpServiceGroup SelectedRtmpService { get; set; }

    public Bitmap StreamThumbnail { get; set; }

    public ICommand StartStreamCommand { get; }
    public ICommand StopStreamsCommand { get; }
    public ICommand ToggleReceivingStreamCommand { get; }
    public ICommand TestYtHacksCommand { get; }
    public IAsyncRelayCommand<RtmpServiceGroup> AddServiceCommand { get; }
    public ICommand UploadThumbnailCommand { get; }
    public ICommand UpdateMetadataCommand { get; }

    public ICommand RemoveSelectedServiceCommand { get; }


    public StreamMetadata CurrentMetadata { get; set; } = new StreamMetadata();

    private async Task Initialize()
    {
        await AutoLoginIfTokenized();
        SubscribeToEvents();
        if (_streamService != null)
            _streamService.StartPolling();

    }

    [ObservableProperty]
    private string _serverStatus = "RTMP-server: ❌ Not Running";

    [ObservableProperty]
    private string _streamStatus = "Stream status: ❌ Not Receiving";
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
            LogService.Log(ex.Message);
        }
    }


    public MainWindowViewModel(IDialogService dialogService, IFilePickerService filePickerService, VideoPlayerService vidPlayer)
    {
        // Init commands
        LoginWithGoogleCommand = new AsyncRelayCommand(OnLoginWithGoogleClicked);
        LoginWithKickCommand = new AsyncRelayCommand(LoginWithKick);
        LoginWithTwitchCommand = new AsyncRelayCommand(LoginWithTwitch);

        StartStreamCommand = new RelayCommand(OnStartStream);
        StopStreamsCommand = new RelayCommand(OnStopStreams);
        ToggleReceivingStreamCommand = new RelayCommand(ToggleReceivingStream);
        TestYtHacksCommand = new RelayCommand(OnTestYtHacks);
        AddServiceCommand = new AsyncRelayCommand<RtmpServiceGroup>(OnRTMPServiceSelected);
        UploadThumbnailCommand = new AsyncRelayCommand(UploadThumbnail);
        UpdateMetadataCommand = new RelayCommand(OnUpdateMetadata);

        RemoveSelectedServiceCommand = new RelayCommand(RemoveSelectedService);

        // services
        MetaDataService = new();
        _centralAuthService = new();
        _filePickerService = filePickerService;
        _dialogService = dialogService;
        _streamService = new(_centralAuthService);
        _videoPlayerService = vidPlayer;

        // set state of lists
        RtmpServiceGroups = _stateService.RtmpServiceGroups;
        SelectedServicesToStream = _stateService.SelectedServicesToStream;
        YoutubeVideoCategories = _stateService.YoutubeVideoCategories;

        // start stream inspection ( its really never off its just a toggle for the visibility)
        _ = Initialize();
    }


    private async Task OnRTMPServiceSelected(RtmpServiceGroup group)
    {
        var result = await _dialogService.ShowServerDetailsAsync(group);

        if (!result)
            LogService.Log($"Cancelled adding service: {group.ServiceName}\n");
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

        var userName = await _centralAuthService.LoginWithYoutube(MetaDataService);
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
        var results = await _centralAuthService.TryAutoLoginAllAsync(MetaDataService);

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

    // ==== Command handlers ====
    private void OnStartStream()
    {
        StreamStatusText = "Stream status: 🟢 Live";
        LogMessages.Add("Started streaming...");
    }

    private void OnStopStreams()
    {
        StreamStatusText = "Stream status: ❌ Not Receiving";
        LogMessages.Add("Stopped all streams.");
    }

    private void OnTestYtHacks()
    {
        LogMessages.Add("Tested YouTube Hacks.");
    }

    private async Task UploadThumbnail()
    {
        try
        {
            var bitmap = await _filePickerService.PickImageAsync();

            if (bitmap != null)
            {
                ThumbnailImage = bitmap;
                LogMessages.Add("Thumbnail loaded successfully");
            }
            else
            {
                LogMessages.Add("No file selected");
            }
        }
        catch (Exception ex)
        {
            LogMessages.Add($"Error loading thumbnail: {ex.Message}");
        }
    }


    private void OnUpdateMetadata()
    {
        //  LogMessages.Add($"Updated metadata: Title={TitleText}, Category={SelectedYoutubeCategory?.Snippet?.Title}");
        //  StatusTextBlock = "Metadata updated successfully.";
    }

    private void RemoveSelectedService()
    {
        if (SelectedService == null)
        {
            LogMessages.Add("Ingen tjänst vald att ta bort");
            return;
        }

        if (!SelectedServicesToStream.Contains(SelectedService))
        {
            LogMessages.Add("Den valda tjänsten finns inte i listan");
            return;
        }

        var serviceName = SelectedService.DisplayName;
        SelectedServicesToStream.Remove(SelectedService);
        LogMessages.Add($"Tog bort tjänst: {serviceName}");
        SelectedService = null;
    }
}
