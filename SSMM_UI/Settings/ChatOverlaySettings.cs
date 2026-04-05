namespace SSMM_UI.Settings;

public class ChatOverlaySettings
{
    public bool Enabled { get; set; } = true;
    public bool IsAlwaysOnTop { get; set; } = true;
    public bool IsClickThrough { get; set; } = false;
    public bool EnableConcatenation { get; set; } = true;
    public int ConcatenationWindowSeconds { get; set; } = 8;
    public int MaxMessages { get; set; } = 200;
    public int MaxConcatenatedLines { get; set; } = 4;
    public double Opacity { get; set; } = 0.85;
    public double FontScale { get; set; } = 1.0;
}
