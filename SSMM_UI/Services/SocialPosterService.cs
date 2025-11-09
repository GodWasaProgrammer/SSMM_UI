using System.Threading.Tasks;
using SSMM_UI.Poster;

namespace SSMM_UI.Services;

public class SocialPosterService
{
    public async Task RunPoster(bool Discord, bool Facebook, bool X)
    {
        await _socialPoster.RunPoster(X, Discord, Facebook);
    }

    private readonly SocialPoster _socialPoster;
    public SocialPosterService(ILogService logger, SocialPoster poster)
    {
        _socialPoster = poster;
    }
}