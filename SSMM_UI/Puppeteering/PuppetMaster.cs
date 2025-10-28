using PuppeteerSharp;
using PuppeteerSharp.Input;
using SSMM_UI.Services;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SSMM_UI.Puppeteering;

public class PuppetMaster
{
    private const string KickUrl = "https://dashboard.kick.com/stream";
    private const string StudioUrl = "https://studio.youtube.com/";
    private const string ChromePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
    private readonly ILogService _logService;
    public PuppetMaster(ILogService logservice)
    {
        _logService = logservice;
    }

    public async Task ProfileSetupKick()
    {
        try
        {
            var options = GetLaunchOptions();

            using var browser = await Puppeteer.LaunchAsync(options);

            var page = await browser.NewPageAsync();

            await page.GoToAsync(KickUrl, WaitUntilNavigation.Networkidle2);

            // Vänta tills browser är stängd
            while (!browser.IsClosed)
            {
                await Task.Delay(1000); // Kolla varje sekund
            }
        }
        catch (Exception ex)
        {
            _logService.Log($"Error in PuppetMaster: {ex.Message}");
        }
    }

    public async Task ProfileSetupYoutube()
    {
        try
        {

            var options = GetLaunchOptions();

            using var browser = await Puppeteer.LaunchAsync(options);

            var page = await browser.NewPageAsync();

            await page.GoToAsync(StudioUrl, WaitUntilNavigation.Networkidle2);

            // Vänta tills browser är stängd
            while (!browser.IsClosed)
            {
                await Task.Delay(1000); // Kolla varje sekund
            }
        }
        catch (Exception ex)
        {
            _logService.Log($"Error in PuppetMaster: {ex.Message}");
        }
    }

    public static LaunchOptions GetLaunchOptions()
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

        return launchOptions;
    }

    public async Task<bool> SetKickGameTitle(string? GameTitle = null, string? StreamTitle = null)
    {
        var options = GetLaunchOptions();

        try
        {
            using var browser = await Puppeteer.LaunchAsync(options);
            var page = await browser.NewPageAsync();

            await page.GoToAsync(KickUrl, WaitUntilNavigation.Networkidle2);
            await Task.Delay(500);

            // === Edit button click with validation ===
            var editClicked = await page.EvaluateFunctionAsync<bool>(@"() => {
            const btn = Array.from(document.querySelectorAll('button'))
                            .find(b => b.textContent.trim() === 'Edit');
            if (btn) {
                btn.click();
                return true;
            }
            return false;
        }");

            if (!editClicked)
            {
                _logService.Log("Edit-button was not found on Kick");
                return false;
            }

            // === Stream title section ===
            if (StreamTitle != null)
            {
                var titleTextarea = await page.QuerySelectorAsync("textarea[name='title']");
                if (titleTextarea == null)
                {
                    _logService.Log("Title-text area was not found in Kick");
                    return false;
                }

                await page.ClickAsync("textarea[name='title']");
                await page.ClickAsync("textarea[name='title']", new ClickOptions { Count = 3 });
                await page.Keyboard.PressAsync("Backspace");
                await page.Keyboard.TypeAsync(StreamTitle);
            }

            // === Game title section === 
            if (GameTitle != null)
            {
                await Task.Delay(500);

                // Validera att dropdown-knappen finns
                var dropdownButton = await page.QuerySelectorAsync("button[aria-haspopup='dialog'][type='button']:has(svg.lucide-chevrons-up-down)");
                if (dropdownButton == null)
                {
                    _logService.Log("Dropdown-button Was not found in Kick");
                    return false;
                }

                await page.ClickAsync("button[aria-haspopup='dialog'][type='button']:has(svg.lucide-chevrons-up-down)");
                await Task.Delay(500);

                // Skriv in titel
                await page.Keyboard.TypeAsync(GameTitle);
                await Task.Delay(2500);

                // Validera att spelet finns och klicka

                // Vänta på att dropdownen visas
                await page.WaitForSelectorAsync("div[cmdk-list]");

                // Använd textbaserad sökning istället för data-value
                //await Task.Delay(500);
                //await page.ClickAsync($"div[data-value='{GameTitle}']");

                var clicked = false;
                for (int i = 0; i < 3; i++)
                {
                    var gameElement = await page.QuerySelectorAsync($"div[data-value='{GameTitle}']");
                    if (gameElement != null)
                    {
                        await gameElement.ClickAsync();
                        clicked = true;
                        break;
                    }
                    await Task.Delay(1000);
                }

                if (!clicked)
                {
                    _logService.Log($"Failed to click '{GameTitle}' after 3 attempts in Kick");
                    return false;
                }
            }

            await Task.Delay(500);

            // === Save button with validation ===
            var saveClicked = await page.EvaluateFunctionAsync<bool>(@"() => {
            const buttons = Array.from(document.querySelectorAll('button'));
            const saveButton = buttons.find(b => b.textContent.trim() === 'Save');
            if (saveButton) {
                saveButton.click();
                return true;
            }
            return false;
        }");

            if (!saveClicked)
            {
                _logService.Log("Save button was not found on Kick");
                return false;
            }

            await Task.Delay(500);
            await browser.CloseAsync();

            _logService.Log("Kick-Stream Updated Successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logService.Log($"Exception in Kick PuppetMaster: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ChangeGameTitleYoutube(string videoId, string title)
    {
        try
        {
            var options = GetLaunchOptions();
            using var browser = await Puppeteer.LaunchAsync(options);
            var page = await browser.NewPageAsync();

            var studioUrl = $"https://studio.youtube.com/video/{videoId}/livestreaming";
            await page.GoToAsync(studioUrl, WaitUntilNavigation.Networkidle0);

            await Task.Delay(500);

            // Validera att edit-knappen finns
            var editButton = await page.QuerySelectorAsync("#edit-button > ytcp-button-shape > button");
            if (editButton == null)
            {
                _logService.Log("Edit-button was not found in Youtube");
                return false;
            }
            await page.ClickAsync("#edit-button > ytcp-button-shape > button");

            await Task.Delay(500);

            // Validera att kategorifältet finns
            var categoryInput = await page.QuerySelectorAsync("#category-container > ytcp-form-gaming > ytcp-form-autocomplete > ytcp-dropdown-trigger > div > div.left-container.style-scope.ytcp-dropdown-trigger > input");
            if (categoryInput == null)
            {
                _logService.Log("Category field was not found in Youtube");
                return false;
            }
            await page.ClickAsync("#category-container > ytcp-form-gaming > ytcp-form-autocomplete > ytcp-dropdown-trigger > div > div.left-container.style-scope.ytcp-dropdown-trigger > input");
            await page.TypeAsync("#category-container > ytcp-form-gaming > ytcp-form-autocomplete > ytcp-dropdown-trigger > div > div.left-container.style-scope.ytcp-dropdown-trigger > input", $"{title}");

            await Task.Delay(2500);

            // Validera att dropdown-items finns
            //var items = await page.QuerySelectorAllAsync("tp-yt-paper-item");
            //if (items.Count() == 0)
            //{
            //    Console.WriteLine("INGA DROPDOWN-ALTERNATIV HITTADES");
            //    return false;
            //}

            // Klicka på rätt item och kolla om det lyckades
            var clickResult = await page.EvaluateFunctionAsync<bool>(@"(searchText) => {
                    const items = document.querySelectorAll('tp-yt-paper-item.selectable-item');
                    
                    for (const item of items) {
                        const text = item.innerText.trim();
                        // Bättre matchning: ignorerar case och mellanslag
                        if (text.toLowerCase().includes(searchText.toLowerCase())) {
                            item.click();
                            return true;
                        }
                    }
                    return false;
                }", title);

            if (!clickResult)
            {
                _logService.Log($"Game could not be found:{title} on Youtube");
                return false;
            }

            // Validera att spara-knappen finns
            var saveButton = await page.QuerySelectorAsync("#save-button > ytcp-button-shape > button");
            if (saveButton == null)
            {
                _logService.Log("Save Button wasnt found on Youtube");
                return false;
            }
            await page.ClickAsync("#save-button > ytcp-button-shape > button");

            await Task.Delay(500);
            await browser.CloseAsync();

            _logService.Log("Title on Youtube Changed Successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logService.Log($"Exception on Youtube:{ex.Message}");
            return false;
        }
    }
}
