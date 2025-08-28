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
    public MainWindowViewModel(IFilePickerService filePickerService, MetaDataService MdService, 
        ILogService logService, StateService stateService, LeftSideBarViewModel leftSideBarViewModel, UserSettings settings, IDialogService 
        dialogService, SearchViewModel searchmodel, LogViewModel logview, SocialPosterViewModel socialposter, StreamControlViewModel streamControlVM)
    {
        //Settings 
        _settings = settings;
        _dialogService = dialogService;
        OpenSetting = new AsyncRelayCommand(OpenSettings);

        // ==== Metadata Controls ====
        UploadThumbnailCommand = new AsyncRelayCommand(UploadThumbnail);
        UpdateMetadataCommand = new RelayCommand(OnUpdateMetadata);

        // === start children ====
        LeftSideBarVM = leftSideBarViewModel;
        SearchVM = searchmodel;
        LogVM = logview;
        SocialPosterVM = socialposter;
        StreamControlVM = streamControlVM;

        // services
        MetaDataService = MdService;
        _filePickerService = filePickerService;
        _logService = logService;
        _stateService = stateService;
        YoutubeVideoCategories = _stateService.YoutubeVideoCategories;

        // state
        _settings = _stateService.UserSettingsObj;

        SearchVM.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SearchViewModel.SelectedItem))
            {
                CurrentMetadata.TwitchCategory = SearchVM.SelectedItem;
            }
        };

    }

    private UserSettings _settings = new();

    // ==== Child Models ====
    public LeftSideBarViewModel LeftSideBarVM { get; }
    public SearchViewModel SearchVM { get; }
    public LogViewModel LogVM { get; }
    public SocialPosterViewModel SocialPosterVM { get; }
    public StreamControlViewModel StreamControlVM { get; }

    // ==== Collections ====
    public ObservableCollection<VideoCategory> YoutubeVideoCategories { get; } = [];

    // ==== Services =====
    public MetaDataService MetaDataService { get; private set; }
    private readonly StateService _stateService;
    private readonly IFilePickerService _filePickerService;
    private readonly ILogService _logService;
    private readonly IDialogService _dialogService;

    // ==== Metadata ====
    [ObservableProperty] private string titleText;
    [ObservableProperty] private VideoCategory selectedYoutubeCategory;
    [ObservableProperty] private Bitmap thumbnailImage;
    [ObservableProperty] private string updateTitle;
    [ObservableProperty] private string metadataStatus;

    // ==== Commands ====
    public ICommand OpenSetting { get; }

    private async Task OpenSettings()
    {
        var newSettings = await _dialogService.ShowSettingsDialogAsync(_settings);
        _settings = newSettings;
        _stateService.SettingsChanged(_settings);
    }

    // == Metadata Selections ==
    public ICommand UploadThumbnailCommand { get; }
    public ICommand UpdateMetadataCommand { get; }

    public StreamMetadata CurrentMetadata { get; set; } = new StreamMetadata();

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
        _stateService.UpdateCurrentMetaData(CurrentMetadata);
    }

    private void OnUpdateMetadata()
    {
        CurrentMetadata.Title = TitleText;
        _logService.Log($"Updated metadata: Title={TitleText}");
        MetadataStatus = "Metadata updated successfully.";
        _stateService.UpdateCurrentMetaData(CurrentMetadata);
    }
}
