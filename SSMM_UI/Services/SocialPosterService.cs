using System.Threading.Tasks;
using SSMM_UI.Poster;

namespace SSMM_UI.Services;

public class SocialPosterService
{
    public async Task Discord()
    {
        await SocialPoster.RunPoster(DiscordPost:true);
    }

    public async Task Facebook()
    {
        await SocialPoster.RunPoster(FBpost:true);
    }

    public async Task X()
    {
        await SocialPoster.RunPoster(XPost:true);
    }
}