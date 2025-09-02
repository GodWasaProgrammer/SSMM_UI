using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SSMM_UI.Poster;

public static class DiscordPoster
{
    public static async Task PostToDiscord(string webhookUrl, string message)
    {
        using var httpClient = new HttpClient();

        var payload = new
        {
            content = message
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync(webhookUrl, content);
        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("Meddelande skickat till Discord!");
        }
        else
        {
            Console.WriteLine($"Fel: {response.StatusCode}, {await response.Content.ReadAsStringAsync()}");
        }
    }
}
