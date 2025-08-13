using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using PuppeteerSharp;
using SSMM_UI.Services;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace SSMM_UI.MetaData;

public class MetaDataService
{
    private YouTubeService? _youTubeService;
    private List<VideoCategory> _ytCategories;

    public List<VideoCategory> GetCategoriesYoutube()
    {
        return _ytCategories;
    }

    public void CreateYouTubeService(string accessToken)
    {
        var credential = GoogleCredential
            .FromAccessToken(accessToken)
            .CreateScoped(YouTubeService.Scope.Youtube);

        var youtubeService = new YouTubeService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "Streamer & Social Media Manager"
        });
        _youTubeService = youtubeService;
    }
    public IReadOnlyList<VideoCategory> YouTubeCategories => _ytCategories;

    public MetaDataService()
    {
        _ytCategories = [];
    }

    public async Task YTCategoryFetch()
    {
        var categoriesRequest = _youTubeService.VideoCategories.List("snippet");
        categoriesRequest.RegionCode = "SE"; // Eller "US", "GB", etc.

        try
        {
            var result = await categoriesRequest.ExecuteAsync();
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
                        LogService.Log($"{category.Id} - {category.Snippet.Title}");
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
            LogService.Log(ex.Message);
        }
    }

    private static void TwitchCategoryFetch(string accessToken, string clientId)
    {
        var categories = new Dictionary<string, (string Name, string BoxArtUrl)>();
        var searchChars = "abcdefghijklmnopqrstuvwxyz0123456789";

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        http.DefaultRequestHeaders.Add("Client-Id", clientId);

        foreach (var ch in searchChars)
        {
            string cursor = null;

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

                        if (!categories.ContainsKey(id))
                            categories[id] = (name, boxArtUrl);
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
            LogService.Log($"{kv.Value.Name} ({kv.Key}) - {kv.Value.BoxArtUrl}");
        }
    }

    public static async Task ChangeGameTitle(string videoId, string userDataDir = null, string executablePath = null, int timeoutMs = 8000)
    {
        var args = new List<string>
        {
            "--remote-allow-origins=*",
            "--disable-blink-features=AutomationControlled",
            "--no-first-run",
            "--no-default-browser-check"
        };
        if (!string.IsNullOrEmpty(userDataDir))
            args.Add($"--user-data-dir={userDataDir}");

        var launchOptions = new LaunchOptions
        {
            Headless = false,
            Args = args.ToArray(),
            ExecutablePath = executablePath
        };

        using var browser = await Puppeteer.LaunchAsync(launchOptions);
        var page = await browser.NewPageAsync();

        var tcs = new TaskCompletionSource<Dictionary<string, string>>(TaskCreationOptions.RunContinuationsAsynchronously);


        var studioUrl = $"https://studio.youtube.com/video/{videoId}/livestreaming";
        await page.GoToAsync(studioUrl, WaitUntilNavigation.Networkidle0);

        await page.WaitForSelectorAsync("#edit-button > ytcp-button-shape > button");

        await page.ClickAsync("#edit-button > ytcp-button-shape > button");

        await page.WaitForSelectorAsync("#category-container > ytcp-form-gaming > ytcp-form-autocomplete > ytcp-dropdown-trigger > div > div.left-container.style-scope.ytcp-dropdown-trigger > input");
        await page.ClickAsync("#category-container > ytcp-form-gaming > ytcp-form-autocomplete > ytcp-dropdown-trigger > div > div.left-container.style-scope.ytcp-dropdown-trigger > input");
        await page.TypeAsync("#category-container > ytcp-form-gaming > ytcp-form-autocomplete > ytcp-dropdown-trigger > div > div.left-container.style-scope.ytcp-dropdown-trigger > input", "Hearts of Iron IV");

        await Task.Delay(2500);

        // Leta upp rätt item genom text och klicka på det
        await page.EvaluateFunctionAsync(@"(gameName) => {
    const items = document.querySelectorAll('tp-yt-paper-item');
    for (const item of items) {
        if (item.innerText.trim().startsWith(gameName)) {
            item.click();
            break;
        }
    }
}", "Hearts of Iron IV");


        // Klicka på spara-knappen
        await page.ClickAsync("#save-button > ytcp-button-shape > button");

        await Task.Delay(500000);
    }

}
