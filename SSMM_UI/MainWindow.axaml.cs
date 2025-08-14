using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Google.Apis.YouTube.v3.Data;
using SSMM_UI.MetaData;
using SSMM_UI.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
namespace SSMM_UI;

public partial class MainWindow : Window
{
    public static ObservableCollection<string> LogMessages => LogService.Messages;
    private readonly CentralAuthService _centralAuthService;
    public StreamMetadata CurrentMetadata { get; set; } = new StreamMetadata();
    public MetaDataService MetaDataService { get; private set; }
    private readonly StateService _stateService = new();
    private StreamService? _streamService;
    const string RtmpAdress = "rtmp://localhost:1935/live/demo";
    private bool isReceivingStream = false;

    public MainWindow()
    {
        InitializeComponent();
        _centralAuthService = new CentralAuthService();
        MetaDataService = new();
        //if (!Design.IsDesignMode)
        //    RtmpIncoming.Play(RtmpAdress);
    }

    protected override async void OnOpened(EventArgs e)
    {
        //    base.OnOpened(e);
        //    await AutoLoginIfTokenized();
        //    _streamService = new(_centralAuthService);
        //    UIService.StreamStatusChanged += text =>
        //    {
        //        StreamStatusText.Text = text;
        //    };
        //    UIService.ServerStatusChanged += text =>
        //    {
        //        ServerStatusText.Text = text;
        //    };
        //    UIService.StartStreamButtonChanged += change =>
        //    {
        //        StartStreamButton.IsEnabled = change;
        //    };
        //    UIService.StopStreamButtonChanged += change =>
        //    {
        //        StopStreamButton.IsEnabled = change;
        //    };
        //    if (!Design.IsDesignMode)
        //    {
        //        _streamService.StartStreamStatusPolling();
        //        StreamService.StartServerStatusPolling();
        //    }
        //}
    }



    public string haxx = "";
    private async void TestYThacks(object? sender, RoutedEventArgs e)
    {

        // ~~~~~~~~~~~~~~~~~~~WORKING~~~~~~~~~~~~~~//
        var videoId = "rJZZqhvgQ1A";
        var userDataDirPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Google",
            "Chrome",
            "User Data"
        );
        var chromeExePath = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
        var defaultProfilePath = Path.Combine(userDataDirPath, "Default");

        await YoutubeStudioPuppeteer.ChangeGameTitle(videoId, defaultProfilePath, chromeExePath);
        // ~~~~~~~~~~~~~~~~~~~~~~WORKING~~~~~~~~~~//
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _stateService.SerializeServices();
    }
    
    //private void ToggleReceivingStream(object? sender, RoutedEventArgs e)
    //{
    //    if (!isReceivingStream)
    //    {
    //        RtmpIncoming.IsVisible = true;
    //        ReceivingStatus.Text = "Receiving stream...";
    //        ToggleStreamButton.Content = "Stop Receiving";
    //        isReceivingStream = true;
    //    }
    //    else
    //    {
    //        //RtmpIncoming.Stop();
    //        RtmpIncoming.IsVisible = false;

    //        ReceivingStatus.Text = "Stream stopped";
    //        ToggleStreamButton.Content = "Start Receiving";
    //        isReceivingStream = false;
    //    }
    //}

    //private async Task SetYouTubeCategoryAndGameAsync(string videoId, string wikipediaUrl, string accessToken)
    //{
    //    // 1. Uppdatera kategorin till "Gaming" via klient-API
    //    var videosList = _youtubeService.Videos.List("snippet,topicDetails"); // ändrat här
    //    videosList.Id = videoId;

    //    var video = (await videosList.ExecuteAsync()).Items.FirstOrDefault();

    //    if (video == null)
    //    {
    //        Console.WriteLine("Kunde inte hitta videon med angivet ID.");
    //        return;
    //    }

    //    video.Snippet.CategoryId = "20"; // Gaming

    //    var updateRequest = _youtubeService.Videos.Update(video, "snippet");
    //    await updateRequest.ExecuteAsync();

    //    // 2. Gör PATCH-anrop för att sätta spel/topicDetails
    //    var patchPayload = new
    //    {
    //        id = videoId,
    //        topicDetails = new
    //        {
    //            topicCategories = new[] { wikipediaUrl }
    //        }
    //    };

    //    using var httpClient = new HttpClient();
    //    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    //    var json = JsonSerializer.Serialize(patchPayload);
    //    var content = new StringContent(json, Encoding.UTF8, "application/json");

    //    var patchRequest = new HttpRequestMessage(new HttpMethod("PATCH"),
    //        $"https://www.googleapis.com/youtube/v3/videos?part=topicDetails")
    //    {
    //        Content = content
    //    };

    //    var response = await httpClient.SendAsync(patchRequest);
    //    var body = await response.Content.ReadAsStringAsync();

    //    if (!response.IsSuccessStatusCode)
    //    {
    //        Console.WriteLine($"PATCH failed: {response.StatusCode}\n{body}");
    //    }
    //    else
    //    {
    //        Console.WriteLine("Gaming-topic satt korrekt.");
    //    }
    //}


    // TODO: Figure out a better way to deduct which stream is which...
    //private void StartStream(object? sender, RoutedEventArgs e)
    //{
    //    //StartStreamButton.IsEnabled = false;
    //    if (_streamService != null)
    //    {
    //        try
    //        {
    //            _streamService.StartStream(CurrentMetadata, SelectedServicesToStream);
    //        }
    //        catch (Exception ex)
    //        {
    //            LogService.Log(ex.ToString());
    //        }
    //    }
    //}

    private void StopStreams(object? sender, RoutedEventArgs e)
    {
        if (_streamService != null)
        {
            try
            {
                _streamService.StopStreams();
            }
            catch (Exception ex)
            {
                LogService.Log(ex.ToString());
            }
        }
        //StartStreamButton.IsEnabled = true;
        //StopStreamButton.IsEnabled = false;
    }

    private async void OnUploadThumbnailClicked(object? sender, RoutedEventArgs e)
    {
        if (this.VisualRoot is Window window)
        {

            var options = new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select Thumbnail Image",
                AllowMultiple = false,
                FileTypeFilter =
            [
                new Avalonia.Platform.Storage.FilePickerFileType("Image Files")
            {
                Patterns = ["*.jpg", "*.jpeg", "*.png", "*.bmp"]
            }
            ]
            };


            var files = await window.StorageProvider.OpenFilePickerAsync(options);

            if (files is not null && files.Count > 0)
            {
                var file = files[0];

                using var stream = await file.OpenReadAsync();
                var bitmap = new Avalonia.Media.Imaging.Bitmap(stream);

                // show image in UI
                //ThumbnailImage.Source = bitmap;
                CurrentMetadata.Thumbnail = bitmap;
                var path = file.Path?.LocalPath ?? "(no local path)";
                LogService.Log($"Selected thumbnail: {path}");
//
//                //StatusTextBlock.Foreground = Avalonia.Media.Brushes.Green;
                //StatusTextBlock.Text = "Thumbnail loaded successfully.";
                // set path to metadataobjekt
                CurrentMetadata.ThumbnailPath = path;
            }
            else
            {
               // StatusTextBlock.Foreground = Avalonia.Media.Brushes.Red;
               // StatusTextBlock.Text = "No file selected.";
            }
        }
        else
        {
            throw new Exception("Our parameter window was null");
        }
    }

    private static void DetectSystemTheme()
    {
        if (Application.Current != null)
        {
            if (Application.Current.ActualThemeVariant != null)
            {

                var isDark = Application.Current.ActualThemeVariant == ThemeVariant.Dark;

                // Ladda rätt tema
                var theme = isDark ? "Dark" : "Light";
                Application.Current.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;

                // Rensa befintliga resurser
                Application.Current.Resources.MergedDictionaries.Clear();

                // Lägg till det nya temat
                var themeResource = new ResourceInclude(new Uri($"avares://SSMM_UI/Resources/{theme}Theme.axaml"))
                {
                    Source = new Uri($"avares://SSMM_UI/Resources/{theme}Theme.axaml")
                };
                Application.Current.Resources.MergedDictionaries.Add(themeResource);
            }
        }
        else
        {
            throw new Exception("Our Application.Current was null. Major error");
        }
    }

    private void OnUpdateMetadataClicked(object? sender, RoutedEventArgs e)
    {
        var title = ""; //TitleTextBox.Text?.Trim();

        if (string.IsNullOrEmpty(title))
        {
            //StatusTextBlock.Foreground = Avalonia.Media.Brushes.Red;
            //StatusTextBlock.Text = "Please enter a stream title.";
            return;
        }

        //TitleOfStream.Text = $"Stream Title: {title}";

        // set title of MetaData 
        CurrentMetadata.Title = title;

        // UI indicator
        //StatusTextBlock.Foreground = Avalonia.Media.Brushes.Green;
        //StatusTextBlock.Text = "Metadata updated successfully!";
    }

    /// <summary>
    /// Debug only
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    public void ClearGoogleOAuthTokens(object? sender, RoutedEventArgs e)
    {
        string[] possiblePaths =
        [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".credentials"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Google.Apis.Auth")
    ];

        bool foundAndDeleted = false;

        foreach (var path in possiblePaths)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    Directory.Delete(path, true);
                    LogService.Log($"OAuth tokens cleared from: {path}");
                    foundAndDeleted = true;
                }
                catch (Exception ex)
                {
                    LogService.Log($"Failed to clear OAuth tokens at {path}: {ex.Message}");
                }
            }
            else
            {
                LogService.Log($"No OAuth tokens found at: {path}");
            }
        }

        if (!foundAndDeleted)
        {
            LogService.Log("No OAuth token directories found to clear.");
        }
    }
}