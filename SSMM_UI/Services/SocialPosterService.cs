using System.Threading.Tasks;
using SSMM_UI.Poster;

namespace SSMM_UI.Services;

public class SocialPosterService
{
    public static async Task Discord()
    {
        await SocialPoster.RunPoster(DiscordPost:true);
    }

    public static async Task Facebook()
    {
        await SocialPoster.RunPoster(FBpost:true);
    }

    public static async Task X()
    {
        await SocialPoster.RunPoster(XPost:true);
    }
}