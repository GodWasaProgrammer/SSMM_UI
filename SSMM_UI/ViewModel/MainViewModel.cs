using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;

namespace SSMM_UI;

public partial class MainWindowViewModel : ObservableObject
{
    // ==== Collections ====
    public ObservableCollection<RtmpServiceGroup> RtmpServiceGroups { get; } = [];
    public ObservableCollection<SelectedService> SelectedServicesToStream { get; } = [];
    public ObservableCollection<YoutubeCategory> YoutubeVideoCategories { get; } = [];
    public ObservableCollection<string> LogMessages { get; } = [];

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
    [ObservableProperty] private YoutubeCategory selectedYoutubeCategory;
    [ObservableProperty] private Bitmap thumbnailImage;
    [ObservableProperty] private string statusTextBlock;

    // ==== Commands ====
    public ICommand LoginWithGoogleCommand { get; }
    public ICommand LoginWithKickCommand { get; }
    public ICommand LoginWithTwitchCommand { get; }

    public ICommand StartStreamCommand { get; }
    public ICommand StopStreamsCommand { get; }
    public ICommand ToggleReceivingStreamCommand { get; }
    public ICommand TestYtHacksCommand { get; }

    public ICommand UploadThumbnailCommand { get; }
    public ICommand UpdateMetadataCommand { get; }

    public ICommand RemoveSelectedServiceCommand { get; }

    public MainWindowViewModel()
    {
        // Init commands
        LoginWithGoogleCommand = new RelayCommand(OnLoginWithGoogle);
        LoginWithKickCommand = new RelayCommand(OnLoginWithKick);
        LoginWithTwitchCommand = new RelayCommand(OnLoginWithTwitch);

        StartStreamCommand = new RelayCommand(OnStartStream);
        StopStreamsCommand = new RelayCommand(OnStopStreams);
        ToggleReceivingStreamCommand = new RelayCommand(OnToggleReceivingStream);
        TestYtHacksCommand = new RelayCommand(OnTestYtHacks);

        UploadThumbnailCommand = new RelayCommand(OnUploadThumbnail);
        UpdateMetadataCommand = new RelayCommand(OnUpdateMetadata);

        RemoveSelectedServiceCommand = new RelayCommand(OnRemoveSelectedService);

        // Example dummy data
        RtmpServiceGroups.Add(new RtmpServiceGroup { ServiceName = "YouTube" });
        RtmpServiceGroups.Add(new RtmpServiceGroup { ServiceName = "Twitch" });

        YoutubeVideoCategories.Add(new YoutubeCategory { Snippet = new YoutubeCategorySnippet { Title = "Gaming" } });
        YoutubeVideoCategories.Add(new YoutubeCategory { Snippet = new YoutubeCategorySnippet { Title = "Music" } });
    }

    // ==== Command handlers ====
    private void OnLoginWithGoogle() => YoutubeLoginStatus = "Logged in to YouTube ✔";
    private void OnLoginWithKick() => KickLoginStatus = "Logged in to Kick ✔";
    private void OnLoginWithTwitch() => TwitchLoginStatus = "Logged in to Twitch ✔";

    private void OnStartStream()
    {
        // StreamStatusText = "Stream status: 🟢 Live";
        LogMessages.Add("Started streaming...");
    }

    private void OnStopStreams()
    {
        // StreamStatusText = "Stream status: ❌ Not Receiving";
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
            //   ThumbnailImage = new Bitmap(dummyImagePath);
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

public class YoutubeCategory
{
    public YoutubeCategorySnippet Snippet { get; set; }
}

public class YoutubeCategorySnippet
{
    public string Title { get; set; }
}

