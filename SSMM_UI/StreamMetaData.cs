using System.Collections.Generic;

namespace SSMM_UI;
public class StreamMetadata
{
    public string Title { get; set; } = string.Empty;
    public Avalonia.Media.Imaging.Bitmap? Thumbnail { get; set; }
    // optional
    public string Category { get; set; }
    // optional
    public List<string> Tags { get; set; }
    public string? ThumbnailPath { get; set; }
}
