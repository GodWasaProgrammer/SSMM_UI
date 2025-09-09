using Avalonia.Media.Imaging;
using System.Collections.Generic;

namespace SSMM_UI.Poster;

public class SocialPostTemplate
{
    private string Title = string.Empty;
    private string description = string.Empty;
    private Bitmap Thumbnail;
    private string UserName = string.Empty;
    private string suffix = string.Empty;
    private List<string> LinksToStreams = new List<string>();
    private string isLive = "Is Now LIVE";

    private List<string> Platforms = new List<string>();

    private void BuildPost()
    {
        string platformsText = string.Join(" ", Platforms);
        string linksText = string.Join("\n", LinksToStreams);
        string.Concat(UserName, isLive, platformsText, linksText, suffix);
    }

    private void AddPlatform(string platform)
    {
        string add = $"On:{platform}";
        Platforms.Add(add);
    }
}
