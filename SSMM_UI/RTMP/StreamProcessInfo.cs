using System;
using System.Diagnostics;

namespace SSMM_UI.RTMP;

public class StreamProcessInfo
{
    public string? Header { get; set; }
    public Process? Process { get; set; }
    public bool IsPaused { get; set; }
    public string? InterjectMediaPath { get; set; }
    public DateTime? PauseStartTime { get; set; }
}
