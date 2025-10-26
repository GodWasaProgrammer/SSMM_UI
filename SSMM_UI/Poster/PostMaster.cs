using SSMM_UI.Enums;
using SSMM_UI.Interfaces;
using SSMM_UI.Oauth.Facebook;
using SSMM_UI.RTMP;
using SSMM_UI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SSMM_UI.Poster;

public class PostMaster
{
    private ObservableCollection<SelectedService>? _selectedServices;
    public Dictionary<OAuthServices, IAuthToken>? _authobjects;
    private StateService _stateservice;
    private Dictionary<string, OAuthServices>? _usernameAndService;
    private ILogService _logService;
    public PostMaster(StateService stateservice, ILogService logger)
    {
        _selectedServices = stateservice.SelectedServicesToStream;
        _logService = logger;
        _stateservice = stateservice;
        _stateservice.OnAuthObjectsUpdated += AuthObjectsUpdated;
        //DetermineNamesAndServices();
    }

    public async Task<List<FacebookPage>> RequestPagesAsync(FacebookToken userToken)
    {
        var pages = new List<FacebookPage>();

        try
        {
            if (string.IsNullOrEmpty(userToken.AccessToken))
                throw new Exception("Missing user access token.");

            using var http = new HttpClient();
            var url = $"https://graph.facebook.com/v21.0/me/accounts?access_token={userToken.AccessToken}";

            var resp = await http.GetAsync(url);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Facebook /me/accounts failed: {resp.StatusCode}\n{body}");

            using var doc = JsonDocument.Parse(body);
            var data = doc.RootElement.GetProperty("data");

            foreach (var page in data.EnumerateArray())
            {
                var pageInfo = new FacebookPage
                {
                    Id = page.GetProperty("id").GetString() ?? "",
                    Name = page.GetProperty("name").GetString() ?? "",
                    AccessToken = page.GetProperty("access_token").GetString() ?? ""
                };

                pages.Add(pageInfo);

                Console.WriteLine($"✅ Page: {pageInfo.Name} ({pageInfo.Id})");
            }

            if (pages.Count == 0)
                Console.WriteLine("⚠️ No Facebook pages found for this user. Make sure they are an admin.");

            return pages;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ RequestPagesAsync failed: {ex.Message}");
            return pages;
        }
    }

    public void AuthObjectsUpdated()
    {
        _authobjects = _stateservice.AuthObjects;
        DetermineNamesAndServices();
    }

    public void DetermineNamesAndServices()
    {
        var selectedoutputs = new List<string>();
        _usernameAndService = [];
        if (_selectedServices != null)
        {
            foreach (var srv in _selectedServices)
            {
                selectedoutputs.Add(srv.DisplayName);
            }
        }
        if (_authobjects != null)
        {
            foreach (var obj in _authobjects)
            {
                var service = obj.Key;
                var username = obj.Value.Username;

                bool isServiceSelected = selectedoutputs.Any(output => output.Contains(service.ToString(), StringComparison.OrdinalIgnoreCase));

                if (username != null && isServiceSelected)
                {
                    _usernameAndService.Add(username, service);
                }
                else
                {
                    
                   // _logService.Log($"Username was missing for:{service}");
                }
            }
        }
    }
}
