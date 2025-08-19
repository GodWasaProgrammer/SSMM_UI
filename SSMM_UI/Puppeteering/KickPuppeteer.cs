using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PuppeteerSharp;

namespace SSMM_UI.Puppeteering;

public class KickPuppeteer
{
    private const string KickUrl = "https://dashboard.kick.com/stream";
    private const string ChromePath = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
    private const string ProfileDir = "C:\\Users\\vemha\\AppData\\Local\\Google\\Chrome\\User Data";
    private const string TempProfileDir = "C:\\temp\\chrome_profile_copy";
    private const string ProfileName = "Default";
    public async Task SetGameTitleKick(string gameTitle)
    {

        //DirectoryCopy(ProfileDir, TempProfileDir, true);
        var args = new List<string>
        {
            "--remote-allow-origins=*",
            "--disable-blink-features=AutomationControlled",
            "--no-first-run",
            "--no-default-browser-check",
            $"--user-data-dir={ProfileDir}",
            "--profile-directory=Default"
        };

        var launchOptions = new LaunchOptions
        {
            Headless = false,
            Args = args.ToArray(),
            ExecutablePath = ChromePath,
            DumpIO = true
        };

        using var browser = await Puppeteer.LaunchAsync(launchOptions);

        
        var page = await browser.NewPageAsync();


        await page.GoToAsync(KickUrl, WaitUntilNavigation.Networkidle0);

        var tcs = new TaskCompletionSource<Dictionary<string, string>>(TaskCreationOptions.RunContinuationsAsynchronously);


    }

    private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
    {
        // Hämtar information om källkatalogen
        DirectoryInfo dir = new DirectoryInfo(sourceDirName);

        // Kontrollerar att källkatalogen existerar
        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException(
                "Källkatalogen existerar inte eller kunde inte hittas: " + sourceDirName);
        }

        // Hämtar alla underkataloger
        DirectoryInfo[] dirs = dir.GetDirectories();

        // Skapar målkatalogen om den inte existerar
        if (!Directory.Exists(destDirName))
        {
            Directory.CreateDirectory(destDirName);
        }

        // Hämtar alla filer i den aktuella katalogen och kopierar dem till den nya platsen
        FileInfo[] files = dir.GetFiles();
        foreach (FileInfo file in files)
        {
            string tempPath = Path.Combine(destDirName, file.Name);
            file.CopyTo(tempPath, true);
        }

        // Om underkataloger ska kopieras, gör det rekursivt
        if (copySubDirs)
        {
            foreach (DirectoryInfo subdir in dirs)
            {
                string tempPath = Path.Combine(destDirName, subdir.Name);
                DirectoryCopy(subdir.FullName, tempPath, copySubDirs);
            }
        }
    }
}


