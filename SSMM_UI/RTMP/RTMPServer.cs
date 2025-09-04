using LiveStreamingServerNet;
using LiveStreamingServerNet.AdminPanelUI;
using LiveStreamingServerNet.Flv.Installer;
using LiveStreamingServerNet.Standalone;
using LiveStreamingServerNet.Standalone.Installer;
using LiveStreamingServerNet.StreamProcessor.AspNetCore.Installer;
using LiveStreamingServerNet.StreamProcessor.Installer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using System.Net;
using System.Threading.Tasks;

namespace SSMM_UI.RTMP;

// TODO: Fix cert or just run HTTP
public class RTMPServer
{
    //public async Task SetupServerAsync()
    //{
    //    using var server = LiveStreamingServerBuilder.Create()
    //        .ConfigureLogging(options => options.AddConsole())
    //        .Build();

    //    await server.RunAsync(new IPEndPoint(IPAddress.Any, 1935));
    //}
    public static void SetupServerAsync()
    {
        Task.Run(() =>
        {
            StartSrv();
        });
    }
    private const bool UseHttpFlvPreview = true;
    private const bool UseHlsPreview = true;

    public static async void StartSrv()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddLiveStreamingServer(
            new IPEndPoint(IPAddress.Any, 1935),
            options =>
            {
                options.AddStandaloneServices();


                options.AddFlv();


                options.AddStreamProcessor()
                    .AddHlsTransmuxer();

            });
        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.Listen(IPAddress.Any, 7000, listenOptions =>
            {
                listenOptions.UseHttps(); // Använder dev-certifikatet
            });
        });

        var app = builder.Build();
        app.UseHttpFlv();
        app.UseHlsFiles();

        app.MapStandaloneServerApiEndPoints();
        app.UseAdminPanelUI(new AdminPanelUIOptions
        {
            // The Admin Panel UI will be available at https://localhost:7000/ui
            BasePath = "/ui",

            // The Admin Panel UI will access HTTP-FLV streams at https://localhost:7000/{streamPath}.flv
            HasHttpFlvPreview = UseHttpFlvPreview,
            HttpFlvUriPattern = "{streamPath}.flv",

            // The Admin Panel UI will access HLS streams at https://localhost:7000/{streamPath}/output.m3u8
            HasHlsPreview = UseHlsPreview,
            HlsUriPattern = "{streamPath}/output.m3u8"
        });

        await app.RunAsync();
    }
}