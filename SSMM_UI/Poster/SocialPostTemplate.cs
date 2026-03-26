using Avalonia.Media.Imaging;
using System.Collections.Generic;

namespace SSMM_UI.Poster;

public class SocialPostTemplate
{
    private readonly string _title = string.Empty;
    private readonly string _description = string.Empty;
    private readonly Bitmap? _Thumbnail;
    private readonly string _userName = string.Empty;
    private string _suffix = string.Empty;
    private readonly List<string> _linksToStreams = [];
    private readonly string? _customMessage;
    private const string _githubLink = "https://github.com/GodWasaProgrammer/SSMM_UI";

    public string Post { get; set; } = string.Empty;

    private readonly List<string> Platforms = [];

    private void BuildPost()
    {
        var platformLines = new List<string>();
        for (int i = 0; i < Platforms.Count && i < _linksToStreams.Count; i++)
        {
            platformLines.Add($"{Platforms[i]} ✅ {_linksToStreams[i]}");
        }

        string platformsText = string.Join("\n", platformLines);

        _suffix = "\nThis was generated using Multistream Manager," +
            "\nA MultiStreaming Desktop Application Developed and maintained by cybercola!" +
            $"\n{_githubLink}";

        var intro = string.IsNullOrWhiteSpace(_customMessage)
            ? $"{_userName} is now LIVE on:"
            : _customMessage;

        Post = string.IsNullOrWhiteSpace(platformsText)
            ? $"{intro}{_suffix}"
            : $"{intro}\n{platformsText}{_suffix}";
    }

    public SocialPostTemplate(string username, List<string> linkstostreams, List<string> platforms, string? customMessage = null)
    {
        _linksToStreams = linkstostreams;
        Platforms = platforms;
        _userName = username;
        _customMessage = customMessage?.Trim();
        BuildPost();
    }
}
