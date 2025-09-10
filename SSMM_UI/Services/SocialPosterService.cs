using System.Threading.Tasks;
using SSMM_UI.Poster;

namespace SSMM_UI.Services;

public class SocialPosterService
{
    public async Task RunPoster(bool Discord, bool Facebook, bool X)
    {
        await SocialPoster.RunPoster(Discord, Facebook, X);
    }
}