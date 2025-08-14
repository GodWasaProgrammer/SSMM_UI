using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Google.Apis.YouTube.v3.Data;
using SSMM_UI.MetaData;
using SSMM_UI.Services;
using System.Collections.ObjectModel;
using System.IO;
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

    // ==== Stream Status ====
    [ObservableProperty] private string receivingStatus;
    [ObservableProperty] private string streamStatusText = "Stream status: ❌ Not Receiving";
    [ObservableProperty] private string serverStatusText = "Polling...";

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

    public bool CanStopStream { get; set; }
    public bool IsReceivingStream { get; set; }

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
    }

    public MainWindowViewModel(IDialogService dialogService)
    {
        // Init commands
        LoginWithGoogleCommand = new AsyncRelayCommand(OnLoginWithGoogleClicked);
        LoginWithKickCommand = new AsyncRelayCommand(LoginWithKick);
        LoginWithTwitchCommand = new AsyncRelayCommand(LoginWithTwitch);

        StartStreamCommand = new RelayCommand(OnStartStream);
        StopStreamsCommand = new RelayCommand(OnStopStreams);
        ToggleReceivingStreamCommand = new RelayCommand(OnToggleReceivingStream);
        TestYtHacksCommand = new RelayCommand(OnTestYtHacks);
        AddServiceCommand = new AsyncRelayCommand<RtmpServiceGroup>(OnRTMPServiceSelected);
        UploadThumbnailCommand = new RelayCommand(OnUploadThumbnail);
        UpdateMetadataCommand = new RelayCommand(OnUpdateMetadata);

        RemoveSelectedServiceCommand = new RelayCommand(OnRemoveSelectedService);
        MetaDataService = new();
        _centralAuthService = new();
        RtmpServiceGroups = _stateService.RtmpServiceGroups;
        SelectedServicesToStream = _stateService.SelectedServicesToStream;
        YoutubeVideoCategories = _stateService.YoutubeVideoCategories;
        _dialogService = dialogService;
        _ = Initialize();
    }
    private readonly IDialogService _dialogService;

    private async Task OnRTMPServiceSelected(RtmpServiceGroup group)
    {
        var result = await _dialogService.ShowServerDetailsAsync(group);

        if (!result)
            LogService.Log($"Cancelled adding service: {group.ServiceName}\n");
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

    private void OnToggleReceivingStream()
    {
        // ReceivingStatus = ReceivingStatus == "Receiving" ? "Not Receiving" : "Receiving";
        //  LogMessages.Add($"Receiving status toggled to: {ReceivingStatus}");
    }

    private void OnTestYtHacks()
    {
        LogMessages.Add("Tested YouTube Hacks.");
    }

    private void OnUploadThumbnail()
    {
        // Simulate loading an image
        var dummyImagePath = "Assets/dummy_thumbnail.png";
        if (File.Exists(dummyImagePath))
        {
            ThumbnailImage = new Bitmap(dummyImagePath);
            LogMessages.Add("Thumbnail uploaded.");
        }
        else
        {
            LogMessages.Add("No thumbnail file found.");
        }
    }

    private void OnUpdateMetadata()
    {
        //  LogMessages.Add($"Updated metadata: Title={TitleText}, Category={SelectedYoutubeCategory?.Snippet?.Title}");
        //  StatusTextBlock = "Metadata updated successfully.";
    }

    private void OnRemoveSelectedService()
    {
        if (SelectedServicesToStream.Count > 0)
        {
            var service = SelectedServicesToStream[0];
            SelectedServicesToStream.Remove(service);
            LogMessages.Add($"Removed service: {service.DisplayName}");
        }
    }
}
