using System.Collections.Generic;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Google.Apis.YouTube.v3.Data;

namespace SSMM_UI.MetaData;
public partial class StreamMetadata : ObservableObject
{
    // ALL
    [ObservableProperty] public string title;

    // Primary Youtube might be relevant for others
    [ObservableProperty] public Bitmap? thumbnail;
    // optional

    [ObservableProperty] public VideoCategory? youTubeCategory;
    // optional
    [ObservableProperty] public TwitchCategory? twitchCategory;
    // optional

    [ObservableProperty] public List<string>? tags;

    [ObservableProperty] public string? thumbnailPath;
}
