using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Google.Apis.YouTube.v3.Data;
using SSMM_UI.MetaData;
using SSMM_UI.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SSMM_UI.ViewModel;

public partial class MetaDataViewModel : ObservableObject
{
    public MetaDataViewModel(MetaDataService mdservice, StateService stateservice, IFilePickerService filePickerService, ILogService logService, SearchViewModel searchVM)
    {
        // ==== Service init =====
        MetaDataService = mdservice;
        _stateService = stateservice;
        _filePickerService = filePickerService;
        _logService = logService;
        YoutubeVideoCategories = _stateService.YoutubeVideoCategories;
        SearchVM = searchVM;

        // ==== Metadata Controls ====
        UploadThumbnailCommand = new AsyncRelayCommand(UploadThumbnail);
        UpdateMetadataCommand = new RelayCommand(OnUpdateMetadata);

        SearchVM.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SearchViewModel.SelectedItem))
            {
                CurrentMetadata.TwitchCategory = SearchVM.SelectedItem;
            }
        };

        // fetch saved state if there is any
        CurrentMetadata = _stateService.GetCurrentMetaData();
        if (CurrentMetadata != null)
        {
            if (CurrentMetadata.Title != null)
                TitleText = CurrentMetadata.Title;
            if (CurrentMetadata.Thumbnail != null)
                thumbnailImage = CurrentMetadata.Thumbnail;
            if (CurrentMetadata.YouTubeCategory != null)
                SelectedYoutubeCategory = YoutubeVideoCategories.FirstOrDefault(c => c.Id == CurrentMetadata.YouTubeCategory.Id);
            // revert to keep correct ref
            if(selectedYoutubeCategory != null)
            CurrentMetadata.YouTubeCategory = selectedYoutubeCategory;
        }
    }

    // == Metadata Selections ==
    public ICommand UploadThumbnailCommand { get; }
    public ICommand UpdateMetadataCommand { get; }

    public StreamMetadata? CurrentMetadata { get; set; } = new StreamMetadata();
    public ObservableCollection<VideoCategory> YoutubeVideoCategories { get; } = [];

    // == child models ==
    public SearchViewModel? SearchVM { get; }

    // == Services ==
    public MetaDataService MetaDataService { get; private set; }
    private readonly StateService _stateService;
    private readonly IFilePickerService _filePickerService;
    private readonly ILogService _logService;

    // ==== Metadata ====
    [ObservableProperty] private string? titleText;
    [ObservableProperty] private VideoCategory? selectedYoutubeCategory;
    [ObservableProperty] private Bitmap? thumbnailImage;
    [ObservableProperty] private string? updateTitle;
    [ObservableProperty] private string? metadataStatus;

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
