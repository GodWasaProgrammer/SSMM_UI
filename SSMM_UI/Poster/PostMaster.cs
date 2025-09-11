using SSMM_UI.Enums;
using SSMM_UI.Interfaces;
using SSMM_UI.Oauth.Google;
using SSMM_UI.Oauth.Kick;
using SSMM_UI.RTMP;
using SSMM_UI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SSMM_UI.Poster;

public class PostMaster
{
    private ObservableCollection<SelectedService>? _selectedServices;
    private Dictionary<OAuthServices, IAuthToken> _authobjects;
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
                _logService.Log($"Username was missing for:{service}");
            }
        }
    }
}
