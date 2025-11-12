namespace SSMM_UI.Settings;

public class UserSettings
{
    public bool PollStream { get; set; } = true;
    public bool PollServer { get; set; } = true;
    public bool SaveTokens { get; set; } = true;
    public bool SaveMetaData { get; set; } = true;
    public bool SaveServices { get; set; } = true;


    // Social posting booleans

    // head control bool
    public bool AutoPost { get; set; } = true;


    public bool PostToX {  get; set; } = true;
    public bool PostToDiscord { get; set; } = true;
    public bool PostToFB { get; set; } = true;

    // Theme
    public bool IsDarkMode { get; set; } = true;
}