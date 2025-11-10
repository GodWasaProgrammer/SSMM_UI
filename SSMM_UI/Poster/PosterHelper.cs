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
            var ytService = new YouTubeService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "Streamer & Social Media Manager"
            });

            // Hämta alla aktuella live-broadcasts
            var request = ytService.LiveBroadcasts.List("id,snippet,contentDetails,status");
            request.BroadcastStatus = LiveBroadcastsResource.ListRequest.BroadcastStatusEnum.Active; // Aktiva sändningar

            var response = await request.ExecuteAsync();

            if (response.Items.Count > 0)
            {
                var broadcast = response.Items[0];
                string videoId = broadcast.Id;
                string title = broadcast.Snippet.Title;
                string liveUrl = $"https://www.youtube.com/watch?v={videoId}";

                islive.IsLive = true;
                islive.LiveUrl = liveUrl;
                islive.VideoTitle = title;
            }
            else
            {
                islive.IsLive = false;
            }

            return islive;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Fel vid kontroll av YouTube live-status: " + ex.Message);
            islive.IsLive = false;
            return islive;
        }
    }
}
