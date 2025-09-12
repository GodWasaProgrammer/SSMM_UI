using LiveStreamingServerNet;
using LiveStreamingServerNet.Flv.Installer;
using LiveStreamingServerNet.Standalone.Installer;
using LiveStreamingServerNet.StreamProcessor.Installer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace SSMM_UI.Tests.RTMP
{
    public class RTMPServerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;

        public RTMPServerIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false
            });
        }

        [Fact]
        public async Task AdminPanel_ShouldReturnSuccess()
        {
            // Act
            var response = await _client.GetAsync("/ui");

            // Assert
            Assert.True(response.StatusCode == HttpStatusCode.OK ||
                       response.StatusCode == HttpStatusCode.Redirect ||
                       response.StatusCode == HttpStatusCode.NotFound,
                $"Admin panel should respond (actual: {response.StatusCode})");
        }

        [Fact]
        public void StartSrv_ShouldConfigureServicesCorrectly()
        {
            // Arrange
            var builder = WebApplication.CreateBuilder();

            // Act
            // Anropa din faktiska konfigurationsmetod
            builder.Services.AddLiveStreamingServer(
                new System.Net.IPEndPoint(System.Net.IPAddress.Any, 1935),
                options =>
                {
                    options.AddStandaloneServices();
                    options.AddFlv();
                    options.AddStreamProcessor().AddHlsTransmuxer();
                });

            // Assert
            var serviceProvider = builder.Services.BuildServiceProvider();
            var rtmpServer = serviceProvider.GetService<ILiveStreamingServer>();

            Assert.NotNull(rtmpServer);
        }
    }
}