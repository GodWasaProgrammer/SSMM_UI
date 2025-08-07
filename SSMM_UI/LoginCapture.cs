namespace SSMM_UI.Hacks;

using Microsoft.Playwright;
using System;
using System.Linq;
using System.Threading.Tasks;

public class LoginCapture
{
    public static async Task RunAsync()
    {
        using var playwright = await Playwright.CreateAsync();

        var context = await playwright.Chromium.LaunchPersistentContextAsync(
            userDataDir: @"C:\Users\Björn\AppData\Local\MyChromeProfile",
            new BrowserTypeLaunchPersistentContextOptions
            {
                Headless = false,
                Channel = "chrome"
            });

        await context.AddInitScriptAsync(@"Object.defineProperty(navigator, 'webdriver', { get: () => undefined });");

        var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();

        await page.GotoAsync("https://studio.youtube.com");

        Console.WriteLine("Logga in manuellt om behövs och tryck ENTER.");
        Console.ReadLine();

        await context.StorageStateAsync(new() { Path = "youtube-session.json" });
        Console.WriteLine("Session sparad. Nästa gång kan du återanvända den.");
    }
}


public class YouTubeStudioAutomation
{
    public static async Task RunAsync(string videoId)
    {
        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new() { Headless = false });
        var context = await browser.NewContextAsync(new()
        {
            StorageStatePath = "youtube-session.json"
        });

        var page = await context.NewPageAsync();

        // Gå till videons inställningssida
        await page.GotoAsync($"https://studio.youtube.com/video/{videoId}/livestreaming");

        // Vänta på att sidan laddats (kan behöva justeras med fler selectors)
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Vänta tills kategori-menyn finns
        await page.WaitForSelectorAsync("text=Spelkategori");

        // Sök efter och välj ett spel (ändra till önskat namn)
        await page.ClickAsync("text=Skriv för att söka efter ett spel");
        await page.FillAsync("input[placeholder='Skriv för att söka efter ett spel']", "Minecraft");
        await page.WaitForTimeoutAsync(2000); // Vänta på autosuggest
        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync("Enter");

        // Klicka på Spara
        await page.ClickAsync("button:has-text('Spara')");

        Console.WriteLine("✅ Spelkategori uppdaterad.");
    }
}