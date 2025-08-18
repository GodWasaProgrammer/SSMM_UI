namespace SSMM_UI.RTMP;

public class StreamInfo
{
    public int Width { get; set; }
    public int Height { get; set; }
    public string Resolution => Height >= 1080 ? "1080p" : Height >= 720 ? "720p" : "480p";
    public string FrameRate { get; set; } = "30fps";
}