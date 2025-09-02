using API_Key_Secrets_Loader;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SSMM_UI.Poster;

public static class FacebookPoster
{
    private static readonly HttpClient _httpClient = new();
    public static async Task Post(string PostContent, KeyLoader kl)
    {
        string pageAccessToken = kl.ACCESS_Tokens["Facebook"]; // Byt ut med din access token
        string pageId = kl.CLIENT_Ids["Facebook"];                 // Byt ut med din sidas ID
        
        // URL för att posta på sidans flöde
        string url = $"https://graph.facebook.com/v17.0/{pageId}/feed";

        // Förbered HTTP-begäran
        var postData = new
        {
            message = PostContent,
            access_token = pageAccessToken
        };

        var content = new StringContent(JsonSerializer.Serialize(postData), Encoding.UTF8, "application/json");

        try
        {
            // Skicka POST-begäran
            var response = await _httpClient.PostAsync(url, content);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Inlägget publicerades framgångsrikt!");
                Console.WriteLine($"Respons: {responseBody}");
            }
            else
            {
                Console.WriteLine("Något gick fel vid publiceringen.");
                Console.WriteLine($"Responskod: {response.StatusCode}");
                Console.WriteLine($"Respons: {responseBody}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ett fel inträffade: {ex.Message}");
        }
    }
}
