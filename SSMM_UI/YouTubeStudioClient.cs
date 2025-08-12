using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SSMM_UI.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SSMM_UI
{
    public class YouTubeStudioClient
    {
        private readonly string _origin = "https://studio.youtube.com";
        private ChromeBrowserCookiesDecryptor _browserCookiesDecryptor;

        public YouTubeStudioClient()
        {
            _browserCookiesDecryptor = new ChromeBrowserCookiesDecryptor();
        }

        public class CookieEntry
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("value")]
            public string Value { get; set; }
        }

        public async Task<HttpResponseMessage> SendRequestAsync(string relativePath, HttpMethod method, string body = null)
        {
            var authHeader = _browserCookiesDecryptor.BuildSapisdHashHeader();
            var cookies = _browserCookiesDecryptor._cookies ?? throw new Exception("No cookies.");

            var cookiePairs = new List<string>();
            foreach (var kv in new[] { "SID", "SAPISID", "HSID", "SSID", "APISID", "__Secure-3PAPISID", "__Secure-1PSID" })
            {
                if (cookies.TryGetValue(kv, out var val) && !string.IsNullOrEmpty(val))
                    cookiePairs.Add($"{kv}={val}");
            }
            var cookieHeader = string.Join("; ", cookiePairs);

            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true }) { BaseAddress = new Uri(_origin) };

            client.DefaultRequestHeaders.Add("Origin", _origin);
            client.DefaultRequestHeaders.Add("Referer", $"{_origin}/");
            client.DefaultRequestHeaders.Add("x-goog-authuser", "0");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("SAPISIDHASH", authHeader);
            client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookieHeader);
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

            var req = new HttpRequestMessage(method, relativePath);
            if (!string.IsNullOrEmpty(body))
                req.Content = new StringContent(body, Encoding.UTF8, "application/json");

            return await client.SendAsync(req);
        }

        public async Task UpdateVideoMetadataAsync(string videoId, string title, string description)
        {
            string filePath = "cookies_v20.json";
            if (!File.Exists(filePath))
            {
                LogService.Log($"Filen {filePath} finns inte.");
                return;
            }

            string json = File.ReadAllText(filePath);

            var cookies2 = JsonConvert.DeserializeObject<List<CookieEntry>>(json);

            var dictionary = new Dictionary<string, string>();

            if (cookies2 != null)
            {
                foreach (var cookie in cookies2)
                {
                    if (!string.IsNullOrEmpty(cookie.Name))
                    {
                        dictionary[cookie.Name] = cookie.Value ?? string.Empty;
                    }
                }
            }

            var cookies = dictionary;
            var cookieNames = new[] { "SID", "HSID", "SSID", "APISID", "SAPISID", "__Secure-1PSID", "__Secure-3PSID", "__Secure-1PSIDCC", "__Secure-3PSIDCC", "__Secure-1PSIDTS", "__Secure-3PSIDTS", "VISITOR_INFO1_LIVE", "VISITOR_PRIVACY_METADATA", "YSC", "PREF", "SIDCC", "ST-sbra4i", "LOGIN_INFO", "__Secure-ROLLOUT_TOKEN" };
            var cookiePairs = new List<string>();
            foreach (var cookieName in cookieNames)
            {
                if (cookies.TryGetValue(cookieName, out var value) && !string.IsNullOrEmpty(value))
                {
                    cookiePairs.Add($"{cookieName}={value}");
                }
            }
            var cookieHeader = string.Join("; ", cookiePairs);

            var sapisidHash = _browserCookiesDecryptor.BuildSapisdHashHeader();

            using var client = new HttpClient
            {
                BaseAddress = new Uri("https://studio.youtube.com")
            };

            client.DefaultRequestHeaders.Add("accept", "application/json");
            client.DefaultRequestHeaders.Add("accept-language", "sv-SE,sv;q=0.9,en-US;q=0.8,en;q=0.7");
            client.DefaultRequestHeaders.Add("Origin", "https://studio.youtube.com");
            client.DefaultRequestHeaders.Add("Referer", $"https://studio.youtube.com/video/{videoId}/livestreaming");
            client.DefaultRequestHeaders.Add("x-goog-authuser", "0");
            client.DefaultRequestHeaders.Add("x-goog-visitor-id", cookies["VISITOR_INFO1_LIVE"]);
            client.DefaultRequestHeaders.Add("x-youtube-client-name", "62");
            client.DefaultRequestHeaders.Add("x-youtube-client-version", "1.20250807");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("SAPISIDHASH", sapisidHash);
            client.DefaultRequestHeaders.Add("Cookie", cookieHeader);

            var payload = new
            {
                sessionInfo = new
                {
                    token = cookies["ST-sbra4i"]
                },
                metadata = new
                {
                    title,
                    description
                },
                videoId
            };

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/youtubei/v1/video_manager/metadata_update", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"API request failed: {response.StatusCode}, Content: {responseContent}");
            }

            LogService.Log($"API Response: {responseContent}");
        }

        public static async Task UpdateCategoryAndGameAsync(
    string videoId,
    YouTubeStudioAuthData auth,
    Dictionary<string, string> cookies,
    string sapisidHash)
        {
            if (string.IsNullOrEmpty(auth.ApiKey) || string.IsNullOrEmpty(auth.XsrfToken))
                throw new ArgumentException("ApiKey eller XsrfToken saknas i auth-data.");
            if (sapisidHash is null)
            {
                throw new ArgumentNullException(nameof(sapisidHash));
            }

            using var handler = new HttpClientHandler { AllowAutoRedirect = true };
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://studio.youtube.com") };

            var decryptor = new ChromeBrowserCookiesDecryptor();
            var authHeaders = decryptor.BuildFullAuthHeaders();

            //test
            var CSRF = cookies.GetValueOrDefault("__Secure-3PSIDTS");
            var CSRF2 = cookies.GetValueOrDefault("__Secure-1PSIDTS");
            client.DefaultRequestHeaders.Add("X-YouTube-CSRF-Token", CSRF);

            //
            client.DefaultRequestHeaders.Add("X-YouTube-CSRF-Token", CSRF2);
            //

            client.DefaultRequestHeaders.Add("X-Origin", "https://studio.youtube.com");
            var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36";
            client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);

            // Lägg till moderna sec-ch-ua headers
            client.DefaultRequestHeaders.Add("sec-ch-ua", "\"Google Chrome\";v=\"135\", \"Chromium\";v=\"135\", \"Not_A Brand\";v=\"24\"");
            client.DefaultRequestHeaders.Add("sec-ch-ua-arch", "\"\"");
            client.DefaultRequestHeaders.Add("sec-ch-ua-bitness", "\"\"");
            client.DefaultRequestHeaders.Add("sec-ch-ua-form-factors", "");
            client.DefaultRequestHeaders.Add("sec-ch-ua-full-version", "\"\"");
            client.DefaultRequestHeaders.Add("sec-ch-ua-full-version-list", "");
            client.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
            client.DefaultRequestHeaders.Add("sec-ch-ua-model", "\"\"");
            client.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Windows\"");
            client.DefaultRequestHeaders.Add("sec-ch-ua-platform-version", "\"\"");
            client.DefaultRequestHeaders.Add("sec-ch-ua-wow64", "?0");

            client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "empty");
            client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");
            client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");
            //


            //bekräftat 
            // Och använd sedan:
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("SAPISIDHASH", authHeaders["main"]);
            client.DefaultRequestHeaders.Add("SAPISID1PHASH", authHeaders["secondary"]);
            client.DefaultRequestHeaders.Add("SAPISID3PHASH", authHeaders["tertiary"]);
            //client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("SAPISIDHASH", $"{authHeaders["main"]}_{authHeaders["secondary"]}_{authHeaders["tertiary"]}");
            client.DefaultRequestHeaders.Add("x-youtube-time-zone", "Europe/Stockholm");
            client.DefaultRequestHeaders.Add("x-youtube-utc-offset", "120");
            client.DefaultRequestHeaders.Add("x-goog-authuser", "0");
            client.DefaultRequestHeaders.Add("x-youtube-client-name", "62");
            client.DefaultRequestHeaders.Add("x-youtube-client-version", "1.20250808.01.00");
            client.DefaultRequestHeaders.Add("x-goog-visitor-id", cookies.TryGetValue("VISITOR_INFO1_LIVE", out var visitor) ? visitor : "");
            client.DefaultRequestHeaders.Add("accept", "application/json");
            client.DefaultRequestHeaders.Add("accept-language", "sv-SE,sv;q=0.9,en-US;q=0.8,en;q=0.7");
            client.DefaultRequestHeaders.Add("Origin", "https://studio.youtube.com");
            client.DefaultRequestHeaders.Add("Referer", $"https://studio.youtube.com/video/{videoId}/livestreaming");

            client.DefaultRequestHeaders.Add("cookie", string.Join("; ", cookies.Select(kv => $"{kv.Key}={kv.Value}")));

            var body = new JObject
            {
                ["context"] = new JObject
                {
                    ["client"] = new JObject
                    {
                        ["clientName"] = 62,
                        ["clientVersion"] = "1.20250808.01.00",
                        ["visitorData"] = auth.VisitorData,
                    },
                    ["user"] = new JObject
                    {
                        ["onBehalfOfUser"] = auth.DelegatedSessionId ?? ""
                    }
                },
                ["videoId"] = videoId,
                ["updates"] = new JObject
                {
                    ["categoryId"] = "20", // Gaming
                    ["gameTitle"] = new JObject
                    {
                        ["newKgEntityId"] = "/m/0_fnvvc" // Sätt här rätt gameKgEntityId, inte bara namn
                    },
                    ["description"] = "Spel: Hearts of Iron IV - https://en.wikipedia.org/wiki/Hearts_of_Iron_IV"
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"/youtubei/v1/video_manager/metadata_update?alt=json&key={auth.ApiKey}")
            {
                Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json")
            };

            request.Headers.Add("x-youtube-session-token", auth.XsrfToken);

            var response = await client.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            bool challenge = json.Contains("CHALLENGE_PROMPT_TYPE_AUTHENTICATE");

            //test area //
            // Challenge-identifiering
            if (json.Contains("CHALLENGE_PROMPT_TYPE_AUTHENTICATE"))
            {
                LogService.Log("YouTube kräver ytterligare verifiering...");

                // 1. Extrahera exakt challenge-data
                var challengeData = JObject.Parse(json);
                var challengeInfo = challengeData["responseContext"]?["webResponseContextExtensionData"]?["challenge"];
                var cookieHeader = request.Headers.GetValues("Cookie").FirstOrDefault();
                if (challengeInfo != null)
                {
                    // 2. Lös challenge med mer kontext
                    // Hämta cookies från originalrequest
                    var challengePath = challengeInfo["path"]?.ToString();
                    // Lös challenge
                    var challengeSolution = await SolveYouTubeChallenge(json, cookieHeader);

                    if (!string.IsNullOrEmpty(challengeSolution))
                    {
                        // Skapa NY request (viktigt!)
                        var newRequest = new HttpRequestMessage(request.Method, request.RequestUri);

                        // Kopiera allt från originalrequest
                        foreach (var header in request.Headers)
                        {
                            newRequest.Headers.Add(header.Key, header.Value);
                        }
                        newRequest.Content = request.Content;

                        // Lägg till challenge-lösning
                        newRequest.Headers.Add("X-YouTube-Challenge-Solution", challengeSolution);

                        // Skicka igen
                        response = await client.SendAsync(newRequest);
                        json = await response.Content.ReadAsStringAsync();
                        //test area // 

                        if (!response.IsSuccessStatusCode && json.Contains("CHALLENGE_PROMPT_TYPE_AUTHENTICATE"))
                        {
                            throw new Exception($"Error updating metadata: {response.StatusCode} - {json}");

                        }
                        else
                        {
                            LogService.Log("✅ Kategori och spel uppdaterat!");
                        }
                    }
                }
            }
        }

        private static async Task<string?> SolveYouTubeChallenge(string challengeResponse, string cookieHeader)
        {
            try
            {
                // 1. Parse det enkla svaret
                var challenge = JObject.Parse(challengeResponse);

                // 2. Kontrollera att det är rätt typ av challenge
                //if (challenge["type"]?.ToString() != "CHALLENGE_PROMPT_TYPE_AUTHENTICATE")
                //{
                //    Console.WriteLine("Okänt challenge-format");
                //    return null;
                //}

                // 3. Bygg standard-URL för denna challenge-typ
                string challengeUrl = "https://studio.youtube.com/youtubei/v1/challenge";
                LogService.Log($"Använder standard challenge URL: {challengeUrl}");

                // 4. Skapa request-body baserat på kända YouTube-mönster
                var requestBody = new JObject
                {
                    ["challengeType"] = "CHALLENGE_PROMPT_TYPE_AUTHENTICATE",
                    ["continueUrl"] = "/youtubei/v1/metadata_update"
                };

                // 5. Skicka verifieringsförfrågan
                var verifyRequest = new HttpRequestMessage(HttpMethod.Post, challengeUrl);
                verifyRequest.Headers.Add("Cookie", cookieHeader);
                verifyRequest.Content = new StringContent(
                    requestBody.ToString(),
                    Encoding.UTF8,
                    "application/json"
                );
                var _httpClient = new HttpClient();
                var verifyResponse = await _httpClient.SendAsync(verifyRequest);
                var verifyContent = await verifyResponse.Content.ReadAsStringAsync();

                // 6. Extrahera token från olika möjliga format
                var responseJson = JObject.Parse(verifyContent);
                return responseJson["token"]?.ToString()
                       ?? responseJson.SelectToken("response.token")?.ToString();
            }
            catch (Exception ex)
            {
                LogService.Log($"Challenge-lösning misslyckades: {ex.Message}");
                return null;
            }
        }

        public async Task<YouTubeStudioAuthData> FetchStudioEditHtmlAsync(string videoId)
        {
            string filePath = "cookies_v20.json";
            if (!File.Exists(filePath))
                throw new Exception($"Filen {filePath} finns inte.");

            string json = File.ReadAllText(filePath);
            var cookies2 = JsonConvert.DeserializeObject<List<CookieEntry>>(json);
            var cookies = new Dictionary<string, string>();
            if (cookies2 != null)
                foreach (var cookie in cookies2)
                    if (!string.IsNullOrEmpty(cookie.Name))
                        cookies[cookie.Name] = cookie.Value ?? string.Empty;

            var cookieNames = new[] { "SID", "HSID", "SSID", "APISID", "SAPISID", "__Secure-1PSID", "__Secure-3PSID", "__Secure-1PSIDCC", "__Secure-3PSIDCC", "__Secure-1PSIDTS", "__Secure-3PSIDTS", "VISITOR_INFO1_LIVE", "VISITOR_PRIVACY_METADATA", "YSC", "PREF", "SIDCC", "ST-sbra4i", "LOGIN_INFO", "__Secure-ROLLOUT_TOKEN" };
            var cookiePairs = cookieNames.Where(c => cookies.TryGetValue(c, out var value) && !string.IsNullOrEmpty(value))
                                        .Select(c => $"{c}={cookies[c]}");
            var cookieHeader = string.Join("; ", cookiePairs);
            LogService.Log($"Cookies: {cookieHeader}");

            if (!cookies.ContainsKey("SAPISID") || !cookies.ContainsKey("ST-sbra4i") || !cookies.TryGetValue("VISITOR_INFO1_LIVE", out string? value))
                throw new Exception("Missing critical cookies: SAPISID, ST-sbra4i, or VISITOR_INFO1_LIVE");
            if (!cookies.ContainsKey("YSC"))
                LogService.Log("Warning: YSC cookie is missing, this may cause issues.");

            //var sapisidHash = _browserCookiesDecryptor.BuildSapisdHashHeader();
            var authHeaders = _browserCookiesDecryptor.BuildFullAuthHeaders();
            //Console.WriteLine($"SAPISIDHASH: {sapisidHash}");

            using var handler = new HttpClientHandler { AllowAutoRedirect = true };
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://studio.youtube.com") };

            client.DefaultRequestHeaders.Add("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            client.DefaultRequestHeaders.Add("accept-language", "sv-SE,sv;q=0.9,en-US;q=0.8,en;q=0.7");
            client.DefaultRequestHeaders.Add("Origin", "https://studio.youtube.com");
            client.DefaultRequestHeaders.Add("Referer", $"https://studio.youtube.com/video/{videoId}/livestreaming");
            client.DefaultRequestHeaders.Add("x-goog-authuser", "0");
            client.DefaultRequestHeaders.Add("x-goog-visitor-id", value);
            client.DefaultRequestHeaders.Add("x-youtube-client-name", "62");
            client.DefaultRequestHeaders.Add("x-youtube-client-version", "1.20250807");
            client.DefaultRequestHeaders.Add("x-youtube-delegation-context", "EhhVQ0NIYzJyRUlaTDFWT1dVTXNUQnk4N3cqAggI");
            client.DefaultRequestHeaders.Add("x-youtube-page-cl", "791771214");
            client.DefaultRequestHeaders.Add("x-youtube-page-label", "youtube.studio.web_20250806_06_RC00");
            client.DefaultRequestHeaders.Add("x-youtube-time-zone", "Europe/Stockholm");
            client.DefaultRequestHeaders.Add("x-youtube-utc-offset", "120");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36");
            //client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("SAPISIDHASH", sapisidHash);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("SAPISIDHASH", authHeaders["main"]);
            client.DefaultRequestHeaders.Add("SAPISID1PHASH", authHeaders["secondary"]);
            client.DefaultRequestHeaders.Add("SAPISID3PHASH", authHeaders["tertiary"]);
            client.DefaultRequestHeaders.Add("Cookie", cookieHeader);

            var response = await client.GetAsync($"/video/{videoId}/livestreaming");
            var html = await response.Content.ReadAsStringAsync();

            await File.WriteAllTextAsync("page.html", html);

            var test = YouTubeStudioDataExtractor.ExtractAllData(html);
            var ytCfg = YouTubeConfigExtractor.ExtractYtCfg(html);

            //Console.WriteLine("DELEGATED_SESSION_ID: " + ytCfg["DELEGATED_SESSION_ID"]);
            //Console.WriteLine("VISITOR_DATA: " + ytCfg["VISITOR_DATA"]);
            //Console.WriteLine("XSRF_TOKEN: " + ytCfg["XSRF_TOKEN"]);

            LogService.Log($"Response Status: {response.StatusCode}");
            LogService.Log($"Response Content (first 500 chars): {html.Substring(0, Math.Min(500, html.Length))}");

            if (!response.IsSuccessStatusCode || html.Contains("<title>Logga in"))
                throw new Exception($"Failed to fetch Studio Edit HTML. Status: {response.StatusCode}, Content: {html[..Math.Min(500, html.Length)]}");

            return ytCfg;
        }

        public class YouTubeStudioAuthData
        {
            public string? ApiKey { get; set; }
            public string? ClientScreenNonce { get; set; }
            public string? VisitorData { get; set; }
            public string? XsrfToken { get; set; }
            public string? DelegatedSessionId { get; set; }
            public string? VideoId { get; set; }
            public string? CsrfToken { get; set; }
            public string? ChannelId { get; set; }
            public string? PlaylistId { get; set; }
        }

        public class YouTubeStudioDataExtractor
        {
            public static YouTubeStudioAuthData ExtractAllData(string html)
            {
                var result = new YouTubeStudioAuthData();

                // 1. Extrahera ytcfg-data (din befintliga metod)
                var ytCfgMatch = Regex.Match(html, @"ytcfg\.set\((\{.*?\})\);", RegexOptions.Singleline);
                if (ytCfgMatch.Success)
                {
                    try
                    {
                        var ytCfg = JObject.Parse(ytCfgMatch.Groups[1].Value);

                        result.ApiKey = ytCfg["INNERTUBE_API_KEY"]?.ToString();
                        result.ClientScreenNonce = ytCfg["CLIENT_SCREEN_NONCE"]?.ToString();
                        result.VisitorData = ytCfg["VISITOR_DATA"]?.ToString();
                        result.XsrfToken = ytCfg["XSRF_TOKEN"]?.ToString();
                        result.DelegatedSessionId = ytCfg["DELEGATED_SESSION_ID"]?.ToString();
                    }
                    catch (Exception ex)
                    {
                        LogService.Log($"Error parsing ytcfg: {ex.Message}");
                    }
                }

                // 2. Extrahera ytInitialData (för mer information)
                var ytInitialMatch = Regex.Match(html, @"var ytInitialData\s*=\s*(\{.*?\});", RegexOptions.Singleline);
                if (ytInitialMatch.Success)
                {
                    try
                    {
                        var ytInitial = JObject.Parse(ytInitialMatch.Groups[1].Value);
                        result.ChannelId = ytInitial.SelectToken("responseContext.visitorData")?.ToString()?.Split('.')?.Last();
                        result.CsrfToken = ytInitial.SelectToken("responseContext.csrfToken")?.ToString();

                        // Fallback för saknade värden
                        result.VisitorData ??= ytInitial.SelectToken("responseContext.visitorData")?.ToString();
                        result.DelegatedSessionId ??= ytInitial.SelectToken("responseContext.mainAppWebResponseContext.delegatedSessionId")?.ToString();
                    }
                    catch (Exception ex)
                    {
                        LogService.Log($"Error parsing ytInitialData: {ex.Message}");
                    }
                }

                // 3. Extrahera från meta-taggar (fallback)
                var htmlDoc = new HtmlAgilityPack.HtmlDocument();
                htmlDoc.LoadHtml(html);

                result.XsrfToken ??= htmlDoc.DocumentNode
                    .SelectSingleNode("//meta[@name='XSRF-TOKEN']")?
                    .GetAttributeValue("content", null);

                // 4. Debug-utskrift av alla värden
                LogService.Log("=== Extraherade värden ===");
                LogService.Log($"API Key: {result.ApiKey ?? "null"}");
                LogService.Log($"Client Screen Nonce: {result.ClientScreenNonce ?? "null"}");
                LogService.Log($"Visitor Data: {result.VisitorData ?? "null"}");
                LogService.Log($"XSFR Token: {result.XsrfToken ?? "null"}");
                LogService.Log($"Delegated Session ID: {result.DelegatedSessionId ?? "null"}");
                LogService.Log($"Channel ID: {result.ChannelId ?? "null"}");
                LogService.Log($"CSRF Token: {result.CsrfToken ?? "null"}");

                return result;
            }
        }

        public class YouTubeConfigExtractor
        {
            public static YouTubeStudioAuthData ExtractYtCfg(string html)
            {
                var match = Regex.Match(html, @"ytcfg\.set\((\{.*?\})\);", RegexOptions.Singleline);
                if (!match.Success)
                    throw new Exception("ytcfg.set block not found in HTML.");

                var json = match.Groups[1].Value;
                var obj = JObject.Parse(json);

                return new YouTubeStudioAuthData
                {
                    ApiKey = obj["INNERTUBE_API_KEY"]?.ToString(),
                    ClientScreenNonce = obj["CLIENT_SCREEN_NONCE"]?.ToString(),
                    VisitorData = obj["VISITOR_DATA"]?.ToString(),
                    XsrfToken = obj["XSRF_TOKEN"]?.ToString(),
                    DelegatedSessionId = obj["DELEGATED_SESSION_ID"]?.ToString()
                };
            }
        }
    }
}


//public static async Task UpdateCategoryAndGameAsync(
//    string videoId,
//    YouTubeStudioAuthData auth,
//    Dictionary<string, string> cookies,
//    string sapisidHash)
//{
//    if (string.IsNullOrEmpty(auth.ApiKey) || string.IsNullOrEmpty(auth.XsrfToken))
//        throw new ArgumentException("ApiKey eller XsrfToken saknas i auth-data.");

//    using var handler = new HttpClientHandler { AllowAutoRedirect = true };
//    using var client = new HttpClient(handler) { BaseAddress = new Uri("https://studio.youtube.com") };

//    var decryptor = new ChromeBrowserCookiesDecryptor();
//    var authHeaders = decryptor.BuildFullAuthHeaders();

//    // Och använd sedan:
//    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("SAPISIDHASH", authHeaders["main"]);
//    client.DefaultRequestHeaders.Add("SAPISID1PHASH", authHeaders["secondary"]);
//    client.DefaultRequestHeaders.Add("SAPISID3PHASH", authHeaders["tertiary"]);
//    client.DefaultRequestHeaders.Add("x-youtube-time-zone", "Europe/Stockholm");
//    client.DefaultRequestHeaders.Add("x-youtube-utc-offset", "120");
//    client.DefaultRequestHeaders.Add("x-goog-authuser", "0");
//    client.DefaultRequestHeaders.Add("x-youtube-client-name", "62");
//    client.DefaultRequestHeaders.Add("x-youtube-client-version", "1.20250808.01.00");
//    client.DefaultRequestHeaders.Add("x-goog-visitor-id", cookies.TryGetValue("VISITOR_INFO1_LIVE", out var visitor) ? visitor : "");
//    client.DefaultRequestHeaders.Add("accept", "application/json");
//    client.DefaultRequestHeaders.Add("accept-language", "sv-SE,sv;q=0.9,en-US;q=0.8,en;q=0.7");
//    client.DefaultRequestHeaders.Add("Origin", "https://studio.youtube.com");
//    client.DefaultRequestHeaders.Add("Referer", $"https://studio.youtube.com/video/{videoId}/livestreaming");

//    client.DefaultRequestHeaders.Add("cookie", string.Join("; ", cookies.Select(kv => $"{kv.Key}={kv.Value}")));

//    var body = new JObject
//    {
//        ["context"] = new JObject
//        {
//            ["client"] = new JObject
//            {
//                ["clientName"] = 62,
//                ["clientVersion"] = "1.20250808.01.00",
//                ["visitorData"] = auth.VisitorData,
//            },
//            ["user"] = new JObject
//            {
//                ["onBehalfOfUser"] = auth.DelegatedSessionId ?? ""
//            }
//        },
//        ["videoId"] = videoId,
//        ["updates"] = new JObject
//        {
//            ["categoryId"] = "20", // Gaming
//            ["gameTitle"] = new JObject
//            {
//                ["newKgEntityId"] = "/g/11s0wvnggg" // Sätt här rätt gameKgEntityId, inte bara namn
//            },
//            ["description"] = "Spel: Hearts of Iron IV - https://en.wikipedia.org/wiki/Hearts_of_Iron_IV"
//        }
//    };

//    var request = new HttpRequestMessage(HttpMethod.Post, $"/youtubei/v1/video_manager/metadata_update?alt=json&key={auth.ApiKey}")
//    {
//        Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json")
//    };

//    request.Headers.Add("x-youtube-session-token", auth.XsrfToken);

//    var response = await client.SendAsync(request);
//    var json = await response.Content.ReadAsStringAsync();

//    if (!response.IsSuccessStatusCode)
//        throw new Exception($"Error updating metadata: {response.StatusCode} - {json}");

//    Console.WriteLine("✅ Kategori och spel uppdaterat!");
//}