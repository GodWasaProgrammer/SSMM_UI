using System;
using System.Diagnostics;

namespace SSMM_UI.Oauth;

public static class BrowserHelper
{
    public static void OpenUrlInBrowser(string url)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Could not open URL in browser.", ex);
        }
    }
}
