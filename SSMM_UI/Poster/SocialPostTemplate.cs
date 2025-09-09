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
        string platformsText = string.Join(" ", Platforms);
        string linksText = string.Join("\n", LinksToStreams);
        Post = string.Concat(_userName, isLive, platformsText, linksText, suffix);
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
