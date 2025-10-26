using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using SSMM_UI.API_Key_Secrets_Loader;
using SSMM_UI.DTO;
using SSMM_UI.Enums;
using SSMM_UI.Oauth.Facebook;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Tweetinvi;
using Tweetinvi.Core.Web;

namespace SSMM_UI.Poster;

public static class SocialPoster
{
    public static async Task RunPoster(PostMaster postmaster, bool XPost = false, bool DiscordPost = false, bool FBpost = false)
    {

        // Använd Singleton-instansen
        var kl = KeyLoader.Instance;
        // establish webhooks to deduce if live or not

        // fetch links for all active platforms

        // post on relevant media platforms with said links

        // generate tokens
        var TwitchToken = await GetAccessTokenTwitch(kl.CLIENT_Ids["Twitch"], kl.API_Keys["Twitch"]);

        var isLive = await IsStreamerLiveTwitch(kl.CLIENT_Ids["Twitch"], TwitchToken, kl.ACCOUNT_Names["Twitch"]);

        Console.WriteLine(isLive ? $"{kl.ACCOUNT_Names["Twitch"]} is live!" : $"{kl.ACCOUNT_Names["Twitch"]} is not live.");

        var LiveYT = await IsLiveYoutube(kl.API_Keys["Google"], kl.CLIENT_Ids["Youtube"]);
        Console.WriteLine(LiveYT.IsLive ? $"{kl.ACCOUNT_Names["Youtube"]}is live" : $"{kl.ACCOUNT_Names["Youtube"]} is not live");

        //isLive = IsStreamerLive(twitchClientId, TwitchToken.Result, currentLivePerson);
        //Console.WriteLine(isLive.Result ? $"{currentLivePerson} is live!" : $"{currentLivePerson} is not live.");
        //await XPoster.PostTweetAsync("Testing API", kl);

        //if (true || isLive.Result && LiveYT.Result)
        //{


        List<string> streamlinks = new List<string>();
        List<string> platforms = new List<string>();



        if (isLive)
        {
            platforms.Add("Twitch");
            streamlinks.Add("https://www.twitch.tv/cybercolagaming");
        }
        if (LiveYT.IsLive)
        {
            platforms.Add("Youtube");
            if (LiveYT.LiveUrl != null)
            {
                streamlinks.Add(LiveYT.LiveUrl);
            }
        }

        var template = new SocialPostTemplate("cybercola", streamlinks, platforms);
        var stringtoPost = template.Post;


        if (XPost)
        {
            // Post to X using Bearer Token (OAuth 2.0 Bearer Token)
            if(postmaster._authobjects == null)
            {
                Console.WriteLine("Auth objects are null in PostMaster.");
                return;
            }
            var accesstoken = postmaster._authobjects[OAuthServices.X]?.AccessToken;
            if ((accesstoken != null))
            {

                using var http = new HttpClient();
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accesstoken);
                http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var payload = new
                {
                    text = "Testing X APi without Consumer_key and Consumer_Secret"
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await http.PostAsync("https://api.x.com/2/tweets", content);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Tweet skickad!");
                }
                else
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Fel vid postning: {response.StatusCode}");
                    Console.WriteLine(responseBody);
                }
            }
            else
            {
                Console.WriteLine("Access token is null for X.");
            }
        }

        if (FBpost)
        {
            if (postmaster._authobjects == null)
            {
                Console.WriteLine("Auth objects are null in PostMaster.");
                return;
            }
            postmaster._authobjects.TryGetValue(OAuthServices.Facebook, out var fbAuthToken);
            if(fbAuthToken == null)
            {
                Console.WriteLine("Facebook auth token is null in PostMaster.");
                return;
            }

            var res = await postmaster.RequestPagesAsync((FacebookToken)fbAuthToken);
            var page = res.FirstOrDefault()!;
            await FacebookPosterV2.PostAsync(page.Id, page.AccessToken, "Testing Graph API");
        }
        if (DiscordPost)
        {
            await DiscordPoster.PostToDiscord(kl.Webhooks["DGeneral"], stringtoPost);
            await DiscordPoster.PostToDiscord(kl.Webhooks["DLive"], stringtoPost);
        }
    }

    public static async Task<string> GetAccessTokenTwitch(string clientId, string clientSecret)
    {
        using var client = new HttpClient();
        var response = await client.PostAsync($"https://id.twitch.tv/oauth2/token?client_id={clientId}&client_secret={clientSecret}&grant_type=client_credentials", null);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<TwitchTokenResponse>(responseBody);

        if (tokenResponse is null || string.IsNullOrEmpty(tokenResponse.AccessToken))
        {
            throw new InvalidOperationException("Failed to retrieve the access token from the Twitch API response.");
        }

        return tokenResponse.AccessToken;
    }

    public static async Task<bool> IsStreamerLiveTwitch(string clientId, string accessToken, string streamerName)
    {
        using HttpClient client = new();
        client.DefaultRequestHeaders.Add("Client-ID", clientId);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

        var response = await client.GetAsync($"https://api.twitch.tv/helix/streams?user_login={streamerName}");
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<Dictionary<string, object>>(responseBody);

        if (json is null || !json.TryGetValue("data", out var data))
        {
            throw new InvalidOperationException("Unexpected response format or missing 'data' field from Twitch API.");
        }

        // Kontrollera att datafältet är av typen JsonElement
        if (data is not JsonElement dataElement || dataElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("The 'data' field in the response is not a valid JSON array.");
        }

        return ((JsonElement)json["data"]).GetArrayLength() > 0;
    }

    public static async Task<IsLiveDto> IsLiveYoutube(string apiKey, string channelId)
    {
        var islive = new IsLiveDto();
        try
        {
            // Skapa YouTube Service
            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = apiKey,
                ApplicationName = "YouTubeLiveChecker"
            });

            // Skapa en förfrågan
            var searchRequest = youtubeService.Search.List("snippet");
            searchRequest.ChannelId = channelId;
            searchRequest.EventType = SearchResource.ListRequest.EventTypeEnum.Live;
            searchRequest.Type = "video";

            // Skicka förfrågan
            var searchResponse = await searchRequest.ExecuteAsync();

            if (searchResponse.Items.Count > 0)
            {
                // Hämta den första live-videon
                var liveVideo = searchResponse.Items[0];
                string videoId = liveVideo.Id.VideoId;
                string videoTitle = liveVideo.Snippet.Title;

                // Bygg den exakta live-URL:en
                string liveUrl = $"https://www.youtube.com/watch?v={videoId}";
                // Alternativt: https://www.youtube.com/live/{videoId}

                islive.IsLive = true;
                islive.LiveUrl = liveUrl;
                islive.VideoTitle = videoTitle;
                return islive;
            }
            else
            {
                //return (false, null, null);
                islive.IsLive = false;
                islive.LiveUrl = null;
                islive.VideoTitle = null;
                return islive;
            }
            // Kontrollera om det finns några live-videor
            //return searchResponse.Items.Count > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ett fel inträffade: " + ex.Message);
            //return (false, null, null);
            islive.IsLive = false;
            islive.LiveUrl = null;
            islive.VideoTitle = null;
            return islive;
        }
    }

}
