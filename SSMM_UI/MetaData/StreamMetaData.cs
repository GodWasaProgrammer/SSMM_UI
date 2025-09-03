using System.Collections.Generic;
using System.Text.Json.Serialization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Google.Apis.YouTube.v3.Data;
using SSMM_UI.Services;

namespace SSMM_UI.MetaData;
[JsonConverter(typeof(StreamMetadataConverter))]
public partial class StreamMetadata : ObservableObject
{
    public StreamMetadata() 
    {

    }

    // ALL
    [ObservableProperty] public string? title;

    // Primary Youtube might be relevant for others
    [ObservableProperty] 
    [JsonIgnore]
    public Bitmap? thumbnail;
    // optional

    [ObservableProperty] public VideoCategory? youTubeCategory;
    // optional
    [ObservableProperty] public TwitchCategory? twitchCategory;
    // optional

    [ObservableProperty] public List<string>? tags;

    [ObservableProperty] public string? thumbnailPath;
}
