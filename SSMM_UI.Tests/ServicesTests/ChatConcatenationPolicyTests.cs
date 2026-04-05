using SSMM_UI.DTO;
using SSMM_UI.Enums;
using SSMM_UI.Services;

namespace SSMM_UI.Tests.ServicesTests;

public class ChatConcatenationPolicyTests
{
    [Fact]
    public void TryConcatenate_ShouldMerge_WhenProviderAndAuthorMatchWithinWindow()
    {
        var policy = new ChatConcatenationPolicy();
        var now = DateTime.UtcNow;
        var previous = new ChatMessageDto(AuthProvider.Twitch, "alice", "hello", now, false, "1");
        var incoming = new ChatMessageDto(AuthProvider.Twitch, "alice", "again", now.AddSeconds(2), false, "2");

        var merged = policy.TryConcatenate(previous, incoming, TimeSpan.FromSeconds(8), 4, out var result);

        Assert.True(merged);
        Assert.Contains("hello", result.Message);
        Assert.Contains("again", result.Message);
    }

    [Fact]
    public void TryConcatenate_ShouldNotMerge_WhenProviderDiffers()
    {
        var policy = new ChatConcatenationPolicy();
        var now = DateTime.UtcNow;
        var previous = new ChatMessageDto(AuthProvider.Twitch, "alice", "hello", now, false, "1");
        var incoming = new ChatMessageDto(AuthProvider.Kick, "alice", "again", now.AddSeconds(2), false, "2");

        var merged = policy.TryConcatenate(previous, incoming, TimeSpan.FromSeconds(8), 4, out _);

        Assert.False(merged);
    }
}
