using System.Threading.Tasks;
using SSMM_UI.Poster;

namespace SSMM_UI.Services;

public class SocialPosterService
{
    public async Task RunPoster(bool Discord, bool Facebook, bool X)
    {
        await SocialPoster.RunPoster(_postMaster, X, Discord, Facebook);
    }

    private StateService _stateService;
    private ILogService _logService;
    private PostMaster _postMaster;
    public SocialPosterService(StateService stateService, ILogService logger)
    {
        _stateService = stateService;
        _logService = logger;
        _postMaster = new(_stateService, _logService);
    }
}