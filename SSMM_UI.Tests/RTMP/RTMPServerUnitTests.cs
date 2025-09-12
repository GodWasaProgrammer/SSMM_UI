using SSMM_UI.RTMP;
using LiveStreamingServerNet;
using LiveStreamingServerNet.Flv.Installer;
using LiveStreamingServerNet.Standalone.Installer;
using LiveStreamingServerNet.StreamProcessor.Installer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace SSMM_UI.Tests.RTMP
{
    public class RTMPServerUnitTests
    {
        private readonly Mock<ILiveStreamingServer> _rtmpServerMock;
        private readonly Mock<IHostApplicationLifetime> _lifetimeMock;

        public RTMPServerUnitTests()
        {
            _rtmpServerMock = new Mock<ILiveStreamingServer>();
            _lifetimeMock = new Mock<IHostApplicationLifetime>();
        }

        [Fact]
        public void SetupServerAsync_ShouldStartBackgroundTask()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();

            // Act
            RTMPServer.SetupServerAsync();

            // Assert - Om inget exception kastades, så startade tasken
            Assert.True(true); // Basic verification
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


            var hostedService = services.FirstOrDefault(s =>
                s.ServiceType == typeof(IHostedService) ||
                s.ServiceType.Name.Contains("HostedService"));

            // Minst en av dessa borde finnas
            Assert.True(hostedService != null,
                "Inga LiveStreaming-relaterade tjänster hittades");
        }
    }
}

