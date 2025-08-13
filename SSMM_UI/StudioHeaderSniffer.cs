using PuppeteerSharp;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SSMM_UI;

public record StudioHeadersResult(
    Dictionary<string, string> Headers,
    string InnerTubeKey,
    string XsrfToken
);


public static class StudioHeaderSniffer
{

    public static async Task ChangeGameTitle(
        string videoId,
        string userDataDir = null,
        string executablePath = null,
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

        await Task.Delay(1000); // 1 sekund

        await page.WaitForSelectorAsync("#dialog > div", new WaitForSelectorOptions { Visible = true });

        // Lite extra väntetid för säkerhet
        await Task.Delay(500);

        // Klicka på rätt alternativ i listan inom dialogen
        await page.EvaluateFunctionAsync(@"() => {
    const items = Array.from(document.querySelectorAll('#dialog tp-yt-paper-item'));
    const target = items.find(item => {
        const span = item.querySelector('yt-formatted-string span');
        return span && span.textContent.trim() === 'Hearts of Iron IV';
    });
    if (target) {
        target.click();
    } else {
        console.warn('Alternativet Hearts of Iron IV hittades ej i dropdown-listan');
    }
}");
        // Klicka på spara-knappen
        await page.ClickAsync("#save-button > ytcp-button-shape > button");

        await Task.Delay(500000);
    }
}




