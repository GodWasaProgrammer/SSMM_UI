using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PuppeteerSharp;

namespace SSMM_UI.Puppeteering
{
    internal class PuppetMaster
    {
        private string ProfileDir = @"";
        private const string KickUrl = "https://dashboard.kick.com/stream";
        private const string StudioUrl = "https://studio.youtube.com/";
        private const string ChromePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe";

        static async Task Main()
        {
            var userDataDirPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Google",
                "Chrome",
                "User Data",
                "Puppeteer"
            );


            //await ProfileSetupKick(userDataDirPath);

            //await ProfileSetupYoutube(userDataDirPath);
            await SetKickGameTitle("Grand Theft Auto V (GTA)");
        }

        public static async Task ProfileSetupKick(string userPath)
        {
            var args = new[]
            {
                "--remote-allow-origins=*",
                "--disable-blink-features=AutomationControlled",
                "--no-first-run",
                "--no-default-browser-check"
            };

            var launchOptions = new LaunchOptions
            {
                Headless = false,
                ExecutablePath = ChromePath,
                UserDataDir = userPath,
                Args = args,
                DumpIO = true
            };

            using var browser = await Puppeteer.LaunchAsync(launchOptions);

            var page = await browser.NewPageAsync();

            await page.GoToAsync(KickUrl, WaitUntilNavigation.Networkidle2);


            await Task.Delay(50000);
        }

        public static async Task ProfileSetupYoutube(string userpath)
        {
            var args = new[]
            {
                "--remote-allow-origins=*",
                "--disable-blink-features=AutomationControlled",
                "--no-first-run",
                "--no-default-browser-check"
            };

            var launchOptions = new LaunchOptions
            {
                Headless = false,
                ExecutablePath = ChromePath,
                UserDataDir = userpath,
                Args = args,
                DumpIO = true
            };

            using var browser = await Puppeteer.LaunchAsync(launchOptions);

            var page = await browser.NewPageAsync();

            await page.GoToAsync(StudioUrl, WaitUntilNavigation.Networkidle2);

            await browser.CloseAsync();
        }

        public static async Task SetKickGameTitle(string GameTitle)
        {
            var userDataDirPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Google",
                "Chrome",
                "User Data",
                "Puppeteer"
            );
            var args = new[]
           {
                "--remote-allow-origins=*",
                "--disable-blink-features=AutomationControlled",
                "--no-first-run",
                "--no-default-browser-check"
            };

            var launchOptions = new LaunchOptions
            {
                Headless = false,
                ExecutablePath = ChromePath,
                UserDataDir = userDataDirPath,
                Args = args,
                DumpIO = true
            };
            using var browser = await Puppeteer.LaunchAsync(launchOptions);
            var page = await browser.NewPageAsync();
            await page.GoToAsync(KickUrl, WaitUntilNavigation.Networkidle2);

            // click the edit button
            await page.EvaluateFunctionAsync(@"() => {
    const btn = Array.from(document.querySelectorAll('button'))
                     .find(b => b.textContent.trim() === 'Edit');
    if (btn) btn.click();
}");


            await Task.Delay(500);

            await page.ClickAsync("button[aria-haspopup='dialog'][type='button']:has(svg.lucide-chevrons-up-down)");

            await Task.Delay(500);

            // type in the title
            await page.Keyboard.TypeAsync(GameTitle);

            await Task.Delay(2500);

            await page.ClickAsync($"div[data-value='{GameTitle}']");
            //await page.ClickAsync($"div[data-value='{GameTitle}']");


            await Task.Delay(500);

            // click save button
            await page.EvaluateFunctionAsync(@"() => {
    const buttons = Array.from(document.querySelectorAll('button'));
    const saveButton = buttons.find(b => b.textContent.trim() === 'Save');
    if (saveButton) {
        saveButton.click();
    }
}");


            await Task.Delay(50000);
        }

    }
}
