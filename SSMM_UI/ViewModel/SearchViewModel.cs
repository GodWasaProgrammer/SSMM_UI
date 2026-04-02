using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using Timer = System.Timers.Timer;
using SSMM_UI.Services;
using SSMM_UI.MetaData;
using SSMM_UI.Interfaces;
using SSMM_UI.Enums;
using Avalonia.Threading;
using System.Threading;
using Avalonia.Media.Imaging;

namespace SSMM_UI.ViewModel;

public partial class SearchViewModel : ObservableObject
{
    private readonly Timer _searchTimer;
    private string _accessToken;
    private readonly string _clientId;
    private readonly CentralAuthService _centralAuthService;
    private readonly StateService _stateService;
    private readonly ITwitchCategoryCacheService _cacheService;
    private readonly ILogService _logger;
    private CancellationTokenSource? _searchCts;
    private int _searchVersion;

    public SearchViewModel(
        CentralAuthService authsrv,
        StateService stateService,
        ITwitchCategoryCacheService cacheService,
        ILogService logger)
    {
        _centralAuthService = authsrv;
        _stateService = stateService;
        _cacheService = cacheService;
        _logger = logger;

        _accessToken = _centralAuthService.TwitchService.GetAccessToken();
        _clientId = _centralAuthService.TwitchService.GetClientId();
        _centralAuthService.TwitchService.OnAccessTokenUpdated += OnTokenChange;
        _stateService.OnAuthObjectsUpdated += RefreshAuthState;

        // Sätt upp timer för debounce
        _searchTimer = new Timer(300);
        _searchTimer.Elapsed += async (s, e) => await PerformSearch();
        _searchTimer.AutoReset = false;

        RefreshAuthState();
    }

    [ObservableProperty]
    TwitchCategory? selectedItem;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private ObservableCollection<TwitchCategory> _searchResults = [];

    [ObservableProperty]
    private bool _canSearchTwitch;

    [ObservableProperty]
    private string _searchStatusText = "Log in with Twitch to search categories.";

    [ObservableProperty]
    private bool _isUsingCachedResults;

    private void RefreshAuthState()
    {
        var hasValidToken =
            _stateService.AuthObjects.TryGetValue(AuthProvider.Twitch, out var token)
            && token is not null
            && token.IsValid
            && !string.IsNullOrWhiteSpace(token.AccessToken);

        CanSearchTwitch = hasValidToken;
        if (!CanSearchTwitch)
        {
            SearchStatusText = "Log in with Twitch to search categories.";
            SearchResults.Clear();
            SelectedItem = null;
            IsUsingCachedResults = false;
        }
        else if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            SearchStatusText = "Type at least 3 characters.";
        }
    }

    private void OnTokenChange(string accessTokenUpdated)
    {
        _accessToken = accessTokenUpdated;
        RefreshAuthState();
    }

    partial void OnSearchQueryChanged(string value)
    {
        // Starta om timern när text ändras
        _searchTimer.Stop();
        _searchCts?.Cancel();

        if (!CanSearchTwitch)
        {
            SearchResults.Clear();
            IsSearching = false;
            IsUsingCachedResults = false;
            return;
        }

        if (!string.IsNullOrWhiteSpace(value) && value.Length >= 3)
        {
            SearchStatusText = "Searching Twitch categories...";
            _searchTimer.Start();
        }
        else
        {
            SearchResults.Clear();
            IsUsingCachedResults = false;
            SearchStatusText = "Type at least 3 characters.";
        }
    }

    private async Task PerformSearch()
    {
        if (!CanSearchTwitch)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SearchQuery) || SearchQuery.Length < 3)
        {
            return;
        }

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var localToken = _searchCts.Token;
        var currentVersion = ++_searchVersion;
        IsSearching = true;

        try
        {
            var (results, fromCache) = await _cacheService.SearchAsync(SearchQuery, _accessToken, _clientId, localToken);
            if (localToken.IsCancellationRequested || currentVersion != _searchVersion)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
           {
               SearchResults.Clear();
               foreach (var result in results)
               {
                   SearchResults.Add(result);
               }
           });

            IsUsingCachedResults = fromCache;
            SearchStatusText = results.Count == 0
                ? "No categories found."
                : fromCache
                    ? $"Loaded {results.Count} cached result(s)."
                    : $"Loaded {results.Count} result(s) from Twitch.";
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
        catch (Exception ex)
        {
            _logger.Log($"Twitch category search failed: {ex.Message}");
            SearchResults.Clear();
            IsUsingCachedResults = false;
            SearchStatusText = "Search failed. Check Twitch login and try again.";
        }
        finally
        {
            IsSearching = false;
        }
    }

    public async Task EnsureCategoryBoxArtAsync(TwitchCategory category, CancellationToken cancellationToken = default)
    {
        if (category is null || category.BoxArt is not null)
        {
            return;
        }

        category.BoxArt = await _cacheService.GetOrFetchBoxArtAsync(category.Id, category.BoxArtUrl, cancellationToken);
    }
}
