namespace SSMM_UI.Oauth.Twitch;

public static class TwitchScopes
{
    public const string UserReadEmail = "user:read:email";
    public const string ChannelManageBroadcast = "channel:manage:broadcast";
    public const string StreamKey = "channel:read:stream_key";

    public static string Combine(params string[] scopes)
    {
        return string.Join(" ", scopes);
    }
}