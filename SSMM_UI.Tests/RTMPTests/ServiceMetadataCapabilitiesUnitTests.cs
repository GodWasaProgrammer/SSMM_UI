using SSMM_UI.RTMP;

namespace SSMM_UI.Tests.RTMPTests;

public class ServiceMetadataCapabilitiesUnitTests
{
    [Theory]
    [InlineData("YouTube - RTMPS")]
    [InlineData("Twitch")]
    [InlineData("Kick")]
    public void Resolve_ShouldReturnFullPlatformIntegration_ForMainstreamPlatforms(string serviceName)
    {
        var capability = ServiceMetadataCapabilities.Resolve(serviceName);

        Assert.Equal(MetadataSupportLevel.FullPlatformIntegration, capability.SupportLevel);
    }

    [Theory]
    [InlineData("Trovo")]
    [InlineData("Facebook Live")]
    public void Resolve_ShouldReturnPartialIntegration_ForKnownPartialPlatforms(string serviceName)
    {
        var capability = ServiceMetadataCapabilities.Resolve(serviceName);

        Assert.Equal(MetadataSupportLevel.PartialPlatformIntegration, capability.SupportLevel);
    }

    [Fact]
    public void Resolve_ShouldReturnEmbeddedMetadata_ForUnknownPlatform()
    {
        var capability = ServiceMetadataCapabilities.Resolve("Restream.io");

        Assert.Equal(MetadataSupportLevel.EmbeddedStreamMetadata, capability.SupportLevel);
        Assert.Contains("Catalog service", capability.Reason);
    }

    [Fact]
    public void Resolve_ShouldReturnEmbeddedMetadata_ForOutOfCatalogPlatform()
    {
        var capability = ServiceMetadataCapabilities.Resolve("TotallyCustomPlatform");

        Assert.Equal(MetadataSupportLevel.EmbeddedStreamMetadata, capability.SupportLevel);
        Assert.Contains("outside catalog", capability.Reason);
    }
}
