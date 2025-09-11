using SSMM_UI.Oauth.Google;
using SSMM_UI.Oauth.Kick;
using SSMM_UI.RTMP;
using System.Collections.Generic;

namespace SSMM_UI.Poster;

public class PostMaster
{
    private List<SelectedService>? _selectedServices;
    private KickAuthResult? _kickauth;
    private TwitchTokenResponse? _twitchauth;
    private GoogleOauthResult? _oauthResult;
}
