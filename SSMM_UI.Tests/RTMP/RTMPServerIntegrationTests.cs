using LiveStreamingServerNet;
using LiveStreamingServerNet.Flv.Installer;
using LiveStreamingServerNet.Standalone.Installer;
using LiveStreamingServerNet.StreamProcessor.Installer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SSMM_UI.Tests.RTMP
{
    public class RTMPServerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;
        private static WebApplicationFactory<Program>? _factory;

        public RTMPServerIntegrationTests(WebApplicationFactory<Program> factory)
        {
            // Behåll factory som singleton
            _factory ??= factory;
            _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
                BaseAddress = new Uri("https://localhost:7000")
            });
        }

        [Fact]
        public void ServicesConfiguration_ShouldRegisterRequiredServices()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddLiveStreamingServer(
                new System.Net.IPEndPoint(System.Net.IPAddress.Any, 1935),
                options =>
                {
                    options.AddStandaloneServices();
                    options.AddFlv();
                    options.AddStreamProcessor().AddHlsTransmuxer();
                });

            // Assert - Leta efter IHostedService registrering
            var hostedService = services.FirstOrDefault(s =>
                s.ServiceType == typeof(IHostedService));

            Assert.NotNull(hostedService);
            Assert.Equal(ServiceLifetime.Singleton, hostedService!.Lifetime);
        }
    }
}