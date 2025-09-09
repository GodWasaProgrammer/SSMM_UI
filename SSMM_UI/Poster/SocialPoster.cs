using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using System.Text.Json;
using Tweetinvi;
using Tweetinvi.Core.Web;
using System.Threading.Tasks;
using System;
using System.Net.Http;
using System.Collections.Generic;
using SSMM_UI.API_Key_Secrets_Loader;

namespace SSMM_UI.Poster;

public static class SocialPoster
{
    public static async Task RunPoster(bool XPost = false, bool DiscordPost = false, bool FBpost = false)
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
        Console.WriteLine(LiveYT ? $"{kl.ACCOUNT_Names["Youtube"]}is live" : $"{kl.ACCOUNT_Names["Youtube"]} is not live");

        //isLive = IsStreamerLive(twitchClientId, TwitchToken.Result, currentLivePerson);
        //Console.WriteLine(isLive.Result ? $"{currentLivePerson} is live!" : $"{currentLivePerson} is not live.");
        //await XPoster.PostTweetAsync("Testing API", kl);

        //if (true || isLive.Result && LiveYT.Result)
        //{


        List<string> streamlinks = new List<string>();
        streamlinks.Add("https://www.youtube.com/watch?v=P2XNX3MLEdY");
        streamlinks.Add("https://www.youtube.com/watch?v=P2XNX3MLEdY");

        List<string> platforms = new List<string>();
        platforms.Add("Twitch");
        platforms.Add("Youtube");

        var template = new SocialPostTemplate("cybercola", streamlinks, platforms);
        var stringtoPost = template.Post;

        if (XPost)
        {

            var client = new TwitterClient(
                    kl.CONSUMER_Keys["X"],
                    kl.CONSUMER_Secrets["X"],
                    kl.ACCESS_Tokens["X"],
                    kl.ACCESS_Secrets["X"]
                );

            var poster = new TweetsV2Poster(client);

            ITwitterResult result = await poster.PostTweet
                (
                new TweetV2PostRequest
                {
                    Text = "API test beep boop"
                }
                );

            if (!result.Response.IsSuccessStatusCode)
            {
                Console.WriteLine("Error when posting tweet: " + Environment.NewLine + result.Content);
            }
        }

        if (FBpost)
        {
            await FacebookPoster.Post("Testing Graph API", kl);
        }
        if (DiscordPost)
        {
            await DiscordPoster.PostToDiscord(kl.Webhooks["DGeneral"], "Testing API Webhooks - hello from cybercola!");
            await DiscordPoster.PostToDiscord(kl.Webhooks["DLive"], "Testing API Webhooks! - hello from cybercola!");
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

    public static async Task<bool> IsLiveYoutube(string apiKey, string channelId)
    {
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

            // Kontrollera om det finns några live-videor
            return searchResponse.Items.Count > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ett fel inträffade: " + ex.Message);
            return false;
        }
    }

}
