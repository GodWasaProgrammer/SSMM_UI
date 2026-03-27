using System;
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

    public async Task<SocialPostResult> RunPoster(bool postToX = false, bool postToDiscord = false, bool postToFacebook = false, string? customMessage = null)
    {
        var result = new SocialPostResult();
        List<string> platforms = [];
        List<string> streamlinks = [];

        _postmaster.DetermineNamesAndServices();

        if (_postmaster._authobjects == null)
        {
            _logger.Log("Auth objects are null in PostMaster.");
            result.AddReason("No auth objects available.");
            return result;
        }

        if (_postmaster.UsernameAndService == null || !_postmaster.UsernameAndService.Any())
        {
            _logger.Log("No selected and authed services found for social posting.");
            result.AddReason("No selected services with auth.");
            return result;
        }

        if (_postmaster.UsernameAndService?.ContainsValue(AuthProvider.YouTube) == true)
        {
            var FetchYoutubeToken = _postmaster._authobjects.TryGetValue(AuthProvider.YouTube, out var youtubetoken);
            if (!FetchYoutubeToken || youtubetoken == null)
            {
                _logger.Log("Youtube token not found in PostMaster.");
                result.AddReason("YouTube token missing.");
                return result;
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

        if (_postmaster.UsernameAndService?.ContainsValue(AuthProvider.Twitch) == true)
        {
            var FetchTwitchToken = _postmaster._authobjects.TryGetValue(AuthProvider.Twitch, out var twitchtoken);
            if (!FetchTwitchToken || twitchtoken == null)
            {
                _logger.Log("Twitch token not found in PostMaster.");
                result.AddReason("Twitch token missing.");
                return result;
            }
            var castedToken = (TwitchToken)twitchtoken;
            var isLiveTwitch = await PosterHelper.IsUserLiveTwitch(TwitchClientID, castedToken.AccessToken, castedToken.UserId!);
            if (isLiveTwitch)
            {
                platforms.Add("Twitch");
                streamlinks.Add($"https://www.twitch.tv/{twitchtoken.Username}");
            }
        }

        if (_postmaster.UsernameAndService?.ContainsValue(AuthProvider.Kick) == true)
        {
            var kvp = _postmaster.UsernameAndService.FirstOrDefault(x => x.Value == AuthProvider.Kick);
            if (kvp.Key != null)
            {
                platforms.Add("Kick");
                streamlinks.Add($"https://www.kick.com/{kvp.Key}");
            }
        }

        var first = _postmaster.UsernameAndService?.FirstOrDefault().Key;
        if (platforms.Count == 0)
        {
            _logger.Log("No live platforms available for social posting.");
            result.AddReason("No live platforms detected.");
            return result;
        }
        if (first != null)
        {
            var template = new SocialPostTemplate(first, streamlinks, platforms, customMessage);
            var stringtoPost = template.Post;

            if (postToX)
            {
                if (_postmaster._authobjects.TryGetValue(AuthProvider.X, out var xToken) && xToken?.AccessToken != null)
                {
                    using var http = new HttpClient();
                    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", xToken.AccessToken);
                    http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var payload = new
                    {
                        text = stringtoPost
                    };

                    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                    var response = await http.PostAsync("https://api.x.com/2/tweets", content);

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.Log("X post sent.");
                        result.PostedAny = true;
                        result.PostedTo.Add("X");
                    }
                    else
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        _logger.Log($"Error posting to X: {response.StatusCode}");
                        _logger.Log(responseBody);
                        result.AddReason($"X post failed: {response.StatusCode}");
                    }
                }
                else
                {
                    _logger.Log("Access token is missing for X.");
                    result.AddReason("X token missing.");
                }
            }

            if (postToFacebook)
            {
                if (!_postmaster._authobjects.TryGetValue(AuthProvider.Facebook, out var fbAuthToken) || fbAuthToken == null)
                {
                    _logger.Log("Facebook auth token is null in PostMaster.");
                    result.AddReason("Facebook token missing.");
                }
                else
                {
                    try
                    {
                        var res = await PostMaster.RequestPagesAsync((FacebookToken)fbAuthToken);
                        var page = res.FirstOrDefault();
                        if (page != null)
                        {
                            await FacebookPosterV2.PostAsync(page.Id, page.AccessToken, stringtoPost);
                            _logger.Log("Facebook post sent.");
                            result.PostedAny = true;
                            result.PostedTo.Add("Facebook");
                        }
                        else
                        {
                            _logger.Log("No Facebook page available for posting.");
                            result.AddReason("No Facebook page available.");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        _logger.Log($"Facebook post failed: {ex.Message}");
                        result.AddReason($"Facebook post failed: {ex.Message}");
                    }
                }
            }
            if (postToDiscord)
            {
                var discordPosted = false;
                foreach (var webhook in _stateservice.Webhooks)
                {
                    var hook = webhook.Value;
                    if (hook != null)
                    {
                        try
                        {
                            await DiscordPoster.PostToDiscord(hook!, stringtoPost);
                            discordPosted = true;
                            _logger.Log($"Discord post sent to webhook {webhook.Key}.");
                        }
                        catch (System.Exception ex)
                        {
                            _logger.Log($"Discord post failed for webhook {webhook.Key}: {ex.Message}");
                            result.AddReason($"Discord post failed for {webhook.Key}: {ex.Message}");
                        }
                    }

                }
                if (discordPosted)
                {
                    result.PostedAny = true;
                    result.PostedTo.Add("Discord");
                }
                else if (!result.SkippedReasons.Any(r => r.Contains("Discord post failed", System.StringComparison.OrdinalIgnoreCase)))
                {
                    result.AddReason("No Discord webhooks available.");
                }
            }
        }
        return result;
    }
}
