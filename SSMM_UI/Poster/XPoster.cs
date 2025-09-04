using System;
using System.Threading.Tasks;
using SSMM_UI.API_Key_Secrets_Loader;
using Tweetinvi;

namespace SSMM_UI.Poster;
[Obsolete ("this is not working for some retarded reason")]
public static class XPoster
{
    public static async Task PostTweetAsync(string tweetContent , KeyLoader kl)
    {
        // example for memory
        // TwitterClient("CONSUMER_KEY", "CONSUMER_SECRET", "ACCESS_TOKEN", "ACCESS_TOKEN_SECRET");

        var userClient = new TwitterClient(kl.CONSUMER_Keys["X"], kl.CONSUMER_Secrets["X"], kl.ACCESS_Tokens["X"], kl.ACCESS_Secrets["X"]);

        var user = await userClient.Users.GetAuthenticatedUserAsync();
        Console.WriteLine(user);
        await userClient.Tweets.PublishTweetAsync("Hello tweetinvi world!");
        try
        {
            // Skicka en tweet
            var tweet = await userClient.Tweets.PublishTweetAsync(tweetContent);
            Console.WriteLine("Tweet skickad: " + tweet.Text);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Något gick fel: " + ex.Message);
        }
    }
}
