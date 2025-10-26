using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using SSMM_UI.DTO;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SSMM_UI.Poster;

public class PosterHelper
{
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
