using System.Text.Json;
using System.Text.Json.Serialization;

namespace SSMM_UI.Oauth.Facebook;

public class FacebookUser
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("picture")]
    public JsonElement? Picture { get; set; }
}
