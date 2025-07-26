using System.Collections.Generic;

namespace SSMM_UI;

public class RtmpServiceGroup
{
    public string ServiceName { get; set; } = string.Empty;
    public List<RtmpServerInfo> Servers { get; set; } = new List<RtmpServerInfo>();
}
