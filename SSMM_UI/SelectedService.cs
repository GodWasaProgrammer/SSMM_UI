using SSMM_UI;

public class SelectedService
{
    public RtmpServiceGroup ServiceGroup { get; set; }
    public RtmpServerInfo SelectedServer { get; set; }
    public string StreamKey { get; set; }

    public string DisplayName => $"{ServiceGroup.ServiceName} ({SelectedServer.ServerName})";
    public string FullUrl => $"{SelectedServer.Url}/{StreamKey}";
}