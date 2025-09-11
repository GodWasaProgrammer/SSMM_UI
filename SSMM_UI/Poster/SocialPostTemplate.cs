using Avalonia.Media.Imaging;
using System.Collections.Generic;

namespace SSMM_UI.Poster;

public class SocialPostTemplate
{
    private string Title = string.Empty;
    private string description = string.Empty;
    private Bitmap? Thumbnail;
    private readonly string _userName = string.Empty;
    private string suffix = string.Empty;
    private List<string> LinksToStreams = [];
    private string _githubLink = "https://github.com/GodWasaProgrammer/SSMM_UI";

    public string Post { get; set; } = string.Empty;

    private List<string> Platforms = new List<string>();

    private void BuildPost()
    {
        var platformLines = new List<string>();
        for (int i = 0; i < Platforms.Count && i < LinksToStreams.Count; i++)
        {
            platformLines.Add($"{Platforms[i]} ✅ {LinksToStreams[i]}");
        }

        string platformsText = string.Join("\n", platformLines);

        suffix = "\nThis was generated using Streamer & Social Media Manager," +
            "\nA MultiStreaming Desktop Application Developed and maintained by cybercola!" +
            $"\n{_githubLink}";

        Post = $"{_userName} is now LIVE on:\n{platformsText}{suffix}";


    }

    public SocialPostTemplate(string username, List<string> linkstostreams, List<string> platforms)
    {
        LinksToStreams = linkstostreams;
        Platforms = platforms;
        _userName = username;
        BuildPost();
    }

}
