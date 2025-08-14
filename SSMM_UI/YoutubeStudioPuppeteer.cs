using PuppeteerSharp;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SSMM_UI;

public record StudioHeadersResult(
    Dictionary<string, string> Headers,
    string InnerTubeKey,
    string XsrfToken
);


public static class YoutubeStudioPuppeteer
{

    public static async Task ChangeGameTitle(
        string videoId,
        string userDataDir = "",
        string executablePath = "",
        int timeoutMs = 8000)
    {
        var args = new List<string>
        {
            "--remote-allow-origins=*",
            "--disable-blink-features=AutomationControlled",
            "--no-first-run",
            "--no-default-browser-check"
        };
        if (!string.IsNullOrEmpty(userDataDir))
            args.Add($"--user-data-dir={userDataDir}");

        var launchOptions = new LaunchOptions
        {
            Headless = false,
            Args = args.ToArray(),
            ExecutablePath = executablePath
        };

        using var browser = await Puppeteer.LaunchAsync(launchOptions);
        var page = await browser.NewPageAsync();

        var tcs = new TaskCompletionSource<Dictionary<string, string>>(TaskCreationOptions.RunContinuationsAsynchronously);


        var studioUrl = $"https://studio.youtube.com/video/{videoId}/livestreaming";
        await page.GoToAsync(studioUrl, WaitUntilNavigation.Networkidle0);

        await page.WaitForSelectorAsync("#edit-button > ytcp-button-shape > button");

        await page.ClickAsync("#edit-button > ytcp-button-shape > button");

        await page.WaitForSelectorAsync("#category-container > ytcp-form-gaming > ytcp-form-autocomplete > ytcp-dropdown-trigger > div > div.left-container.style-scope.ytcp-dropdown-trigger > input");
        await page.ClickAsync("#category-container > ytcp-form-gaming > ytcp-form-autocomplete > ytcp-dropdown-trigger > div > div.left-container.style-scope.ytcp-dropdown-trigger > input");
        await page.TypeAsync("#category-container > ytcp-form-gaming > ytcp-form-autocomplete > ytcp-dropdown-trigger > div > div.left-container.style-scope.ytcp-dropdown-trigger > input", "Hearts of Iron IV");

        // await page.WaitForSelectorAsync("#paper-list");

        await Task.Delay(2500);

        // Leta upp rätt item genom text och klicka på det
        await page.EvaluateFunctionAsync(@"(gameName) => {
    const items = document.querySelectorAll('tp-yt-paper-item');
    for (const item of items) {
        if (item.innerText.trim().startsWith(gameName)) {
            item.click();
            break;
        }
    }
}", "Hearts of Iron IV");


        // Klicka på spara-knappen
        await page.ClickAsync("#save-button > ytcp-button-shape > button");

        await Task.Delay(500000);
    }
}




