using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using SSMM_UI.Services;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SSMM_UI.MetaData;

public class MetaDataService
{
    private YouTubeService? _youTubeService;
    private List<VideoCategory> _ytCategories;
    public MetaDataService(ILogService logger, CentralAuthService AuthService)
    {
        _ytCategories = [];
        _AuthService = AuthService;
        _logger = logger;
    }

    // redundant as we jsonize the list and load it in stateservice
    public List<VideoCategory> GetCategoriesYoutube()
    {
        return _ytCategories;
    }

    public async Task SetTwitchTitleAndCategory(string? title = null, string? Category = null)
    {
        if (title == null && Category == null)
        {
            return;
        }
        // get necessary stuff
        var Accesstoken = _AuthService.TwitchService.GetAccessToken();
        var clientId = _AuthService.TwitchService.GetClientId();
        var userId = _AuthService.TwitchService.FetchUserId();

        using var client = new HttpClient();
        var updateJson = new TwitchChannelUpdate();

        // change title
        if (title != null)
        {
            updateJson.Title = title;
        }

        string? gameId = null;
        //change category
        if (Category != null)
        {
            gameId = await GetGameIdTwitch(Category, Accesstoken, clientId);
            updateJson.GameId = gameId;
        }

        var request = new HttpRequestMessage(HttpMethod.Patch,
            $"https://api.twitch.tv/helix/channels?broadcaster_id={userId}");

        // build headers
        request.Headers.Add("Authorization", $"Bearer {Accesstoken}");
        request.Headers.Add("Client-Id", $"{clientId}");


        request.Content = new StringContent(
           JsonSerializer.Serialize(updateJson),
           Encoding.UTF8,
           "application/json"
            );

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

    }

    private static async Task<string?> GetGameIdTwitch(string categoryName, string accessToken, string clientId)
    {
        using var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.twitch.tv/helix/games?name={Uri.EscapeDataString(categoryName)}");

        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Headers.Add("Client-Id", $"{clientId}"); // Måste läggas till

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var gamesData = JsonSerializer.Deserialize<JsonElement>(content);

        if (gamesData.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
        {
            return data[0].GetProperty("id").GetString();
        }

        return null;
    }

    public void CreateYouTubeService(YouTubeService ytservice)
    {
        _youTubeService = ytservice;
    }

    public IReadOnlyList<VideoCategory> YouTubeCategories => _ytCategories;
    private readonly ILogService _logger;
    private readonly CentralAuthService _AuthService;
    

    public void Initialize(string accessToken, string clientId)
    {
        TwitchCategoryFetch(accessToken, clientId);
    }

    // only for making json, is stored locally
    public async Task YTCategoryFetch()
    {
        VideoCategoriesResource.ListRequest? List;
        if (_youTubeService != null)
        {
            if (_youTubeService.VideoCategories != null)
            {
                List = _youTubeService.VideoCategories.List("snippet");
                List.RegionCode = "SE"; // Eller "US", "GB", etc.

                try
                {
                    var result = await List.ExecuteAsync();
                    if (result is not null)
                    {

                        VideoCategoryListResponse response = new();
                        response = result;
                        _ytCategories = [];
                        foreach (var item in response.Items)
                        {
                            var category = new VideoCategory();
                            category = item;
                            _ytCategories.Add(category);
                        }
                        foreach (var category in _ytCategories)
                        {
                            if (category.Snippet.Assignable == true)
                            {
                                _logger.Log($"{category.Id} - {category.Snippet.Title}");
                            }
                        }
                    }
                    else
                    {
                        throw (new Exception("API call to YT failed to fetch categories"));
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(ex.Message);
                }
            }
        }
    }

    private void TwitchCategoryFetch(string accessToken, string clientId)
    {
        var categories = new Dictionary<string, (string Name, string BoxArtUrl)>();
        var searchChars = "abcdefghijklmnopqrstuvwxyz0123456789";

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        http.DefaultRequestHeaders.Add("Client-Id", clientId);

        foreach (var ch in searchChars)
        {
            string? cursor = null;

            do
            {
                var url = $"https://api.twitch.tv/helix/search/categories?query={ch}&first=100";
                if (!string.IsNullOrEmpty(cursor))
                    url += $"&after={cursor}";

                var resp = http.GetStringAsync(url).Result;
                using var doc = System.Text.Json.JsonDocument.Parse(resp);

                if (doc.RootElement.TryGetProperty("data", out var dataArr))
                {
                    foreach (var item in dataArr.EnumerateArray())
                    {
                        var id = item.GetProperty("id").GetString();
                        var name = item.GetProperty("name").GetString();
                        var boxArtUrl = item.GetProperty("box_art_url").GetString();

                        if (!categories.ContainsKey(id!))
                            categories[id!] = (name, boxArtUrl)!;
                    }
                }

                cursor = null;
                if (doc.RootElement.TryGetProperty("pagination", out var pagination) &&
                    pagination.TryGetProperty("cursor", out var cursorElem))
                {
                    cursor = cursorElem.GetString();
                }

            } while (!string.IsNullOrEmpty(cursor));
        }

        // Skriver ut alla kategorier
        foreach (var kv in categories)
        {
            _logger.Log($"{kv.Value.Name} ({kv.Key}) - {kv.Value.BoxArtUrl}");
        }
    }

    public async Task<bool> SetTitleAndCategoryYoutubeAsync(string videoId, int categoryId)
    {
        try
        {
            // check that we have our instance of YTService
            if (_youTubeService == null)
            {
                throw new Exception("YoutubeService was null!");
            }
            // check it exists
            var videosListRequest = _youTubeService.Videos.List("snippet");
            videosListRequest.Id = videoId;

            var videosListResponse = await videosListRequest.ExecuteAsync();

            if (videosListResponse.Items.Count == 0)
            {
                throw new Exception($"Video med ID {videoId} hittades inte");
            }

            var video = videosListResponse.Items[0];

            // TODO: Should we have a -1 param to avoid doing this if its not set?
            video.Snippet.CategoryId = categoryId.ToString();

            // Skapa update request
            var videosUpdateRequest = _youTubeService.Videos.Update(video, "snippet");

            // Utför uppdateringen
            var updatedVideo = await videosUpdateRequest.ExecuteAsync();

            Console.WriteLine($"Video uppdaterad: {updatedVideo.Snippet.Title} (Kategori: {categoryId})");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fel vid uppdatering: {ex.Message}");
            return false;
        }
    }

    public static async Task<List<TwitchCategory>> SearchTwitchCategories(string query, string accessToken, string clientId)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        http.DefaultRequestHeaders.Add("Client-Id", clientId);

        var url = $"https://api.twitch.tv/helix/search/categories?query={Uri.EscapeDataString(query)}&first=20";

        var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        var results = new List<TwitchCategory>();

        if (doc.RootElement.TryGetProperty("data", out var dataArr))
        {
            foreach (var item in dataArr.EnumerateArray())
            {
                results.Add(new TwitchCategory
                {
                    Id = item.GetProperty("id").GetString(),
                    Name = item.GetProperty("name").GetString(),
                    BoxArtUrl = item.GetProperty("box_art_url").GetString()
                });
            }
        }

        return results;
    }
}