using Avalonia.Media.Imaging;
using System.Text.Json.Serialization;

namespace SSMM_UI.MetaData;

public class TwitchCategory
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? BoxArtUrl { get; set; }
    [JsonIgnore]
    public Bitmap? BoxArt { get; set; }
}