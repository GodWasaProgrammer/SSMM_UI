using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Timers;
using SSMM_UI.Services;
using Avalonia.Media.Imaging;
using System.IO;
using SSMM_UI.MetaData;

namespace SSMM_UI.ViewModel;


public partial class SearchViewModel : ObservableObject
{
    private readonly Timer _searchTimer;
    private string _accessToken;
    private readonly string _clientId;
    private CentralAuthService CentAuthService;
    public SearchViewModel(CentralAuthService authsrv)
    {
        CentAuthService = authsrv;
        _accessToken = CentAuthService.TwitchService.GetAccessToken();
        _clientId = CentAuthService.TwitchService.GetClientId();
        CentAuthService.TwitchService.OnAccessTokenUpdated += OnTokenChange;
        // Sätt upp timer för debounce
        _searchTimer = new Timer(300);
        _searchTimer.Elapsed += async (s, e) => await PerformSearch();
        _searchTimer.AutoReset = false;
    }

    [ObservableProperty]
    TwitchCategory selectedItem;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private ObservableCollection<TwitchCategory> _searchResults = new();


    private void OnTokenChange(string accessTokenUpdated)
    {
        _accessToken = accessTokenUpdated;
    }
    partial void OnSearchQueryChanged(string value)
    {
        // Starta om timern när text ändras
        _searchTimer.Stop();

        if (!string.IsNullOrWhiteSpace(value) && value.Length > 2)
        {
            _searchTimer.Start();
        }
        else
        {
            SearchResults.Clear();
        }
    }

    private async Task PerformSearch()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery) || SearchQuery.Length < 3)
            return;

        IsSearching = true;

        try
        {
            var results = await SearchTwitchCategories(SearchQuery, _accessToken, _clientId);

            // Uppdatera på UI-tråden
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                SearchResults.Clear();
                foreach (var result in results)
                {
                    SearchResults.Add(result);
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Search error: {ex.Message}");
            SearchResults.Clear();
        }
        finally
        {
            IsSearching = false;
        }
    }

    public async Task<Bitmap> LoadBoxArtAsync(string BoxArtUrl)
    {
        using var http = new HttpClient();
        
        await using var netStream = await http.GetStreamAsync(BoxArtUrl);
        using var ms = new MemoryStream();
        await netStream.CopyToAsync(ms);
        ms.Position = 0;
        return new Bitmap(ms);
    }

    public async Task<List<TwitchCategory>> SearchTwitchCategories(string query, string accessToken, string clientId)
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
                    BoxArtUrl = item.GetProperty("box_art_url").GetString(),
                });
            }
        }
        var newlist = new List<TwitchCategory>();

        foreach (var item in results)
        {
            Bitmap res = null;
            if (item.BoxArtUrl != null)
            {
                var pic = await LoadBoxArtAsync(item.BoxArtUrl);
                if (pic != null)
                {
                    res = pic;
                }
                var cloneitem = item;
                if (res != null)
                cloneitem.BoxArt = res;
                newlist.Add(cloneitem);
            }
        }

        return newlist;
    }
}
