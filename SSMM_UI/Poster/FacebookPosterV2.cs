using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace SSMM_UI.Poster
{
    public static class FacebookPosterV2
    {
        private static readonly HttpClient _http = new();

        /// <summary>
        /// Publishes a post to a Facebook page's feed using the Graph API v19.0.
        /// </summary>
        /// <param name="pageId">Facebook-Page ID</param>
        /// <param name="pageAccessToken">Pages access token (inte användarens)</param>
        /// <param name="message">Content of Post</param>
        public static async Task<bool> PostAsync(string pageId, string pageAccessToken, string message)
        {
            if (string.IsNullOrWhiteSpace(pageAccessToken))
                throw new ArgumentException("Page access token saknas.");
            if (string.IsNullOrWhiteSpace(pageId))
                throw new ArgumentException("Page ID saknas.");
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Inläggstext saknas.");

            var url = $"https://graph.facebook.com/v19.0/{pageId}/feed";

            // Facebook Expects form data, not JSON for feed-posts
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["message"] = message,
                ["access_token"] = pageAccessToken
            });

            var response = await _http.PostAsync(url, content);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✅ Inlägg publicerat på sida {pageId}");
                Console.WriteLine($"Respons: {body}");
                return true;
            }
            else
            {
                Console.WriteLine($"❌ Fel vid publicering: {response.StatusCode}");
                Console.WriteLine($"Respons: {body}");
                return false;
            }
        }
    }
}
