using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;


namespace SSMM_UI.Oauth;

public static class OAuthListenerHelper
{
    /// <summary>
    /// Starts a temporary local web server to listen for OAuth callback.
    /// Returns the query parameters as a dictionary.
    /// </summary>
    public static async Task<Dictionary<string, string?>?> WaitForCallbackAsync(
        string redirectUri,
        string? expectedState,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<Dictionary<string, string?>?>();
        var uri = new Uri(redirectUri);
        var port = uri.Port;
        var callbackPath = uri.AbsolutePath;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{port}");
        var app = builder.Build();

        app.MapGet(callbackPath, async context =>
        {
            try
            {
                var query = context.Request.Query;
                var queryDict = new Dictionary<string, string?>();
                foreach (var kv in query)
                    queryDict[kv.Key] = kv.Value;

                // State-validering
                if (expectedState != null)
                {
                    if (!queryDict.TryGetValue("state", out var returnedState) ||
                        returnedState != expectedState)
                    {
                        context.Response.StatusCode = 400;
                        await context.Response.WriteAsync("State mismatch");
                        tcs.TrySetException(new Exception("State mismatch in OAuth callback"));
                        return;
                    }
                }

                // Användarfeedback i browsern
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync("<html><body><h2>Login complete — you can close this tab.</h2></body></html>");

                tcs.TrySetResult(queryDict);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            finally
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100);
                    await app.StopAsync();
                });
            }
        });

        // Kör webservern i bakgrunden
        _ = Task.Run(async () =>
        {
            try
            {
                await app.RunAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                tcs.TrySetResult(null);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }, cancellationToken);

        return await tcs.Task;
    }
}