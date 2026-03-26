using System.Threading.Tasks;
using SSMM_UI.Poster;

namespace SSMM_UI.Services;

public class SocialPosterService
{
    private readonly ILogService _logger;
    private readonly SocialPoster _socialPoster;

    public SocialPosterService(ILogService logger, SocialPoster poster)
    {
        _logger = logger;
        _socialPoster = poster;
    }

    public async Task<SocialPostResult> RunPoster(bool postToDiscord, bool postToFacebook, bool postToX, string? customMessage = null)
    {
        var result = await _socialPoster.RunPoster(postToX, postToDiscord, postToFacebook, customMessage);

        if (result.PostedAny && result.PostedTo.Count > 0)
        {
            _logger.Log($"Social post sent to: {string.Join(", ", result.PostedTo)}");
        }
        else
        {
            var reason = result.SkippedReasons.Count > 0 ? string.Join("; ", result.SkippedReasons) : "No destinations accepted the post.";
            _logger.Log($"Social post triggered but nothing sent. {reason}");
        }

        return result;
    }
}
