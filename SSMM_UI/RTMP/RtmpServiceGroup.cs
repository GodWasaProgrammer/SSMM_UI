using System.Collections.Generic;

namespace SSMM_UI.RTMP;

public class RtmpServiceGroup
{
    public string ServiceName { get; set; } = string.Empty;
    public List<RtmpServerInfo> Servers { get; set; } = [];
    public RecommendedSettings? RecommendedSettings { get; set; }
}
