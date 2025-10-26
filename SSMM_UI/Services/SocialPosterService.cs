using System.Threading.Tasks;
using SSMM_UI.Poster;

namespace SSMM_UI.Services;

public class SocialPosterService
{
    public async Task RunPoster(bool Discord, bool Facebook, bool X)
    {
        await _socialPoster.RunPoster(X, Discord, Facebook);
    }

    private StateService _stateService;
    private ILogService _logService;
    private PostMaster _postMaster;
    private SocialPoster _socialPoster;
    public SocialPosterService(StateService stateService, ILogService logger)
    {
        _stateService = stateService;
        _logService = logger;
        _postMaster = new(_stateService, _logService);
        _socialPoster = new(_logService, _postMaster);
        
    }
}