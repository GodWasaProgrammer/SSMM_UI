using System.Threading.Tasks;
using SSMM_UI.Poster;

namespace SSMM_UI.Services;

public class SocialPosterService
{
    public async Task RunPoster(bool postToDiscord, bool postToFacebook, bool postToX, string? customMessage = null)
    {
        await _socialPoster.RunPoster(postToX, postToDiscord, postToFacebook, customMessage);
    }

    private readonly SocialPoster _socialPoster;
    public SocialPosterService(ILogService logger, SocialPoster poster)
    {
        _socialPoster = poster;
    }
}
