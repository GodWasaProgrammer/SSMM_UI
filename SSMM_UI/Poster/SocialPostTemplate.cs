using System.Collections.Generic;

namespace SSMM_UI.Poster;

public class SocialPostTemplate
{
    private string prefix = string.Empty;
    private string suffix = string.Empty;
    private List<string> LinksToStreams = new List<string>();
    private string isLive = "Is Now LIVE";

    private List<string> Platforms = new List<string>();

    private void BuildPost()
    {
        string platformsText = string.Join(" ", Platforms);
        string linksText = string.Join("\n", LinksToStreams);
        string.Concat(prefix, isLive, platformsText, linksText, suffix);
    }

    private void AddPlatform(string platform)
    {
        string add = $"On:{platform}";
        Platforms.Add(add);
    }
}
