using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using SSMM_UI.API_Key_Secrets_Loader;
using SSMM_UI.DTO;
using SSMM_UI.Enums;
using SSMM_UI.Oauth.Facebook;
using SSMM_UI.Oauth.Twitch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SSMM_UI.Poster;

public static class SocialPoster
{
    public static async Task RunPoster(PostMaster postmaster, bool XPost = false, bool DiscordPost = false, bool FBpost = false)
    {
        string TwitchClientID = "y1cd8maguk5ob1m3lwvhdtupbj6pm3";

        // Använd Singleton-instansen
        var kl = KeyLoader.Instance;
        
        if(postmaster._authobjects == null)
        {
            Console.WriteLine("Auth objects are null in PostMaster.");
            return;
        }
        var FetchTwitchToken = postmaster._authobjects.TryGetValue(OAuthServices.Twitch, out var twitchtoken);
        if (!FetchTwitchToken || twitchtoken == null)
        {
            Console.WriteLine("Twitch token not found in PostMaster.");
            return;
        }
        var castedToken = (TwitchToken)twitchtoken;

        var isLiveTwitch = await IsUserLiveTwitch(TwitchClientID, castedToken.AccessToken, castedToken.UserId!);

        var FetchYoutubeToken = postmaster._authobjects.TryGetValue(OAuthServices.Youtube, out var youtubetoken);

        if (!FetchYoutubeToken || youtubetoken == null)
        {
            Console.WriteLine("Youtube token not found in PostMaster.");
            return;
        }
        var LiveYT = await IsLiveYoutube(youtubetoken.AccessToken);

        List<string> streamlinks = new List<string>();
        List<string> platforms = new List<string>();

        if (isLiveTwitch)
        {
            platforms.Add("Twitch");
            streamlinks.Add($"https://www.twitch.tv/{twitchtoken.Username}");
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
            if (postmaster._authobjects == null)
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
            if (fbAuthToken == null)
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

    public static async Task<bool> IsUserLiveTwitch(string clientId, string accessToken, string userId)
    {
        using HttpClient client = new();
        client.DefaultRequestHeaders.Add("Client-ID", clientId);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

        var response = await client.GetAsync($"https://api.twitch.tv/helix/streams?user_id={userId}");
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(responseBody);

        return json.RootElement.GetProperty("data").GetArrayLength() > 0;
    }

    public static async Task<IsLiveDto> IsLiveYoutube(string accesstoken)
    {
        var islive = new IsLiveDto();
        try
        {
            var credential = GoogleCredential.FromAccessToken(accesstoken);
            var YTService = new YouTubeService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "Streamer & Social Media Manager"
            });

            var channelsRequest = YTService.Channels.List("id,snippet");
            channelsRequest.Mine = true; // Viktigt: anger att vi vill ha den kanal som tillhör token

            var channelsResponse = await channelsRequest.ExecuteAsync();
            var channelId = channelsResponse.Items[0].Id;

            // Skapa en förfrågan
            var searchRequest = YTService.Search.List("snippet");
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

                islive.IsLive = true;
                islive.LiveUrl = liveUrl;
                islive.VideoTitle = videoTitle;
                return islive;
            }
            else
            {
                islive.IsLive = false;
                islive.LiveUrl = null;
                islive.VideoTitle = null;
                return islive;
            }
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
