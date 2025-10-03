using System.Collections.Generic;
using System.Linq;

namespace SSMM_UI.RTMP;

public class RtmpServiceGroup
{
    public string ServiceName { get; set; } = string.Empty;
    public List<RtmpServerInfo> Servers { get; set; } = [];
    public RecommendedSettings? RecommendedSettings { get; set; }

    public RtmpServiceGroup Clone()
    {
        return new RtmpServiceGroup
        {
            ServiceName = ServiceName,
            Servers = this.Servers.Select(s => new RtmpServerInfo { ServerName = s.ServerName, ServiceName = s.ServiceName, Url = s.Url }).ToList(),
            RecommendedSettings = RecommendedSettings
            
        };
    }
}
