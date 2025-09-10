using Avalonia.Media.Imaging;
using System.Collections.Generic;

namespace SSMM_UI.Poster;

public class SocialPostTemplate
{
    private string Title = string.Empty;
    private string description = string.Empty;
    private Bitmap? Thumbnail;
    private readonly string  _userName = string.Empty;
    private string suffix = string.Empty;
    private List<string> LinksToStreams = [];
    private string isLive = "Is Now LIVE";

    public string Post {  get; set; }

    private List<string> Platforms = new List<string>();

    private void BuildPost()
    {
        // Bygg plattformsdelen med checkmarks och länkar
        var platformLines = new List<string>();
        for (int i = 0; i < Platforms.Count && i < LinksToStreams.Count; i++)
        {
            platformLines.Add($"{Platforms[i]} ✅ {LinksToStreams[i]}");
        }

        string platformsText = string.Join("\n", platformLines);

        // Bygg hela inlägget med proper formatering
        Post = $"{_userName} is now LIVE on:\n{platformsText}";
    }

    private void AddPlatform(string platform)
    {
        string add = $"On:{platform}";
        Platforms.Add(add);
    }

    public SocialPostTemplate(string username, List<string> linkstostreams, List<string> platforms) 
    {
        LinksToStreams = linkstostreams;
        Platforms = platforms;
        _userName = username;
        BuildPost();
    }

}
