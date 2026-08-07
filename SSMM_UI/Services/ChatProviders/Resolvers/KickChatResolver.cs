using PuppeteerSharp;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace SSMM_UI.Services.ChatProviders.Resolvers
{
    public class KickResolver
    {
        private const string KickBase = "https://kick.com";
        private const string ChromePath =
            @"C:\Program Files\Google\Chrome\Application\chrome.exe";


        public async Task<int?> ResolveChatroomId(string channelName)
        {
            var options = new LaunchOptions
            {
                Headless = false,
                ExecutablePath = ChromePath,
                UserDataDir = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "Google",
                    "Chrome",
                    "User Data",
                    "Puppeteer"
                ),
                Args = [
                                 "--window-position=-32000,-32000",
                             "--disable-blink-features=AutomationControlled",
                             "--no-first-run",
                             "--no-default-browser-check"
                                 ]
            };


            using var browser = await Puppeteer.LaunchAsync(options);

            var page = await browser.NewPageAsync();


            await page.GoToAsync(
                $"{KickBase}/{channelName}",
                WaitUntilNavigation.Networkidle2);


            await Task.Delay(2000);


            var json = await page.EvaluateFunctionAsync<string>(
                @"async (channel) => {
                const response = await fetch(
                    `/api/v1/channels/${channel}`
                );

                return await response.text();
            }",
                channelName);


            Console.WriteLine(json);


            using var doc = JsonDocument.Parse(json);


            var chatroomId =
                doc.RootElement
                   .GetProperty("chatroom")
                   .GetProperty("id")
                   .GetInt32();


            await browser.CloseAsync();


            return chatroomId;
        }
    }
}
