using SSMM_UI.Enums;
using SSMM_UI.Oauth.Facebook;
using SSMM_UI.Oauth.Twitch;
using SSMM_UI.Services;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SSMM_UI.Poster;

public class SocialPoster
{
    public SocialPoster(ILogService logger, PostMaster postmaster, StateService stateservice)
    {
        _logger = logger;
        _postmaster = postmaster;
        _stateservice = stateservice;
    }

    private readonly ILogService _logger;
    private readonly PostMaster _postmaster;
    private readonly StateService _stateservice;
    private const string TwitchClientID = "y1cd8maguk5ob1m3lwvhdtupbj6pm3";

    public async Task RunPoster(bool XPost = false, bool DiscordPost = false, bool FBpost = false)
    {
        List<string> platforms = [];
        List<string> streamlinks = [];
        // Använd Singleton-instansen
        //var kl = KeyLoader.Instance;

        if (_postmaster._authobjects == null)
        {
            _logger.Log("Auth objects are null in PostMaster.");
            return;
        }

        if (_postmaster.UsernameAndService?.ContainsValue(OAuthServices.Youtube) == true)
        {
            var FetchYoutubeToken = _postmaster._authobjects.TryGetValue(OAuthServices.Youtube, out var youtubetoken);
            if (!FetchYoutubeToken || youtubetoken == null)
            {
                _logger.Log("Youtube token not found in PostMaster.");
                return;
            }
            var LiveYT = await PosterHelper.IsLiveYoutube(youtubetoken.AccessToken);
            if (LiveYT.IsLive)
            {
                platforms.Add("Youtube");
                if (LiveYT.LiveUrl != null)
                {
                    streamlinks.Add(LiveYT.LiveUrl);
                }
            }
        }

        if (_postmaster.UsernameAndService?.ContainsValue(OAuthServices.Twitch) == true)
        {
            var FetchTwitchToken = _postmaster._authobjects.TryGetValue(OAuthServices.Twitch, out var twitchtoken);
            if (!FetchTwitchToken || twitchtoken == null)
            {
                _logger.Log("Twitch token not found in PostMaster.");
                return;
            }
            var castedToken = (TwitchToken)twitchtoken;
            var isLiveTwitch = await PosterHelper.IsUserLiveTwitch(TwitchClientID, castedToken.AccessToken, castedToken.UserId!);
            if (isLiveTwitch)
            {
                platforms.Add("Twitch");
                streamlinks.Add($"https://www.twitch.tv/{twitchtoken.Username}");
            }
        }
        var first = _postmaster.UsernameAndService?.FirstOrDefault().Key;
        if (first != null)
        {
            var template = new SocialPostTemplate(first, streamlinks, platforms);
            var stringtoPost = template.Post;

            if (XPost)
            {
                // Post to X using Bearer Token (OAuth 2.0 Bearer Token)
                var accesstoken = _postmaster._authobjects[OAuthServices.X]?.AccessToken;
                if ((accesstoken != null))
                {
                    using var http = new HttpClient();
                    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accesstoken);
                    http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var payload = new
                    {
                        text = stringtoPost
                    };

                    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                    var response = await http.PostAsync("https://api.x.com/2/tweets", content);

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.Log("X Post Sent!");
                    }
                    else
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        _logger.Log($"Error posting to X: {response.StatusCode}");
                        _logger.Log(responseBody);
                    }
                }
                else
                {
                    _logger.Log("Access token is null for X.");
                }
            }

            if (FBpost)
            {
                _postmaster._authobjects.TryGetValue(OAuthServices.Facebook, out var fbAuthToken);
                if (fbAuthToken == null)
                {
                    _logger.Log("Facebook auth token is null in PostMaster.");
                    return;
                }
                var res = await PostMaster.RequestPagesAsync((FacebookToken)fbAuthToken);
                var page = res.FirstOrDefault()!;
                await FacebookPosterV2.PostAsync(page.Id, page.AccessToken, stringtoPost);
            }
            if (DiscordPost)
            {
                foreach (var webhook in _stateservice.Webhooks)
                {
                    var hook = webhook.Value;
                    if (hook != null)
                    await DiscordPoster.PostToDiscord(hook!, stringtoPost);

                }
            }
        }
    }
}
