using SSMM_UI.DTO;
using SSMM_UI.Services.ChatProviders;
using System.Reflection;

namespace SSMM_UI.Tests.ServicesTests;

public class TwitchChatProviderParsingTests
{
    private static readonly MethodInfo TryParsePrivMsgMethod = typeof(TwitchChatProvider)
        .GetMethod("TryParsePrivMsg", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Fact]
    public void TryParsePrivMsg_ShouldParseDisplayNameAndId_WhenTagsPresent()
    {
        var line = "@badge-info=;display-name=Alice;id=abc-123 :alice!alice@alice.tmi.twitch.tv PRIVMSG #alice :hello world";

        var args = new object?[] { line, null };
        var parsed = (bool)TryParsePrivMsgMethod.Invoke(null, args)!;
        var message = Assert.IsType<ChatMessageDto>(args[1]);

        Assert.True(parsed);
        Assert.Equal("Alice", message.Author);
        Assert.Equal("hello world", message.Message);
        Assert.Equal("abc-123", message.MessageId);
    }

    [Fact]
    public void TryParsePrivMsg_ShouldFallbackToPrefixAuthorAndSyntheticId_WhenTagsMissing()
    {
        var line = ":bob!bob@bob.tmi.twitch.tv PRIVMSG #bob :hey there";

        var args = new object?[] { line, null };
        var parsed = (bool)TryParsePrivMsgMethod.Invoke(null, args)!;
        var message = Assert.IsType<ChatMessageDto>(args[1]);

        Assert.True(parsed);
        Assert.Equal("bob", message.Author);
        Assert.Equal("hey there", message.Message);
        Assert.StartsWith("twitch-", message.MessageId, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("PING :tmi.twitch.tv")]
    [InlineData(":alice!alice@alice.tmi.twitch.tv PRIVMSG #alice :")]
    public void TryParsePrivMsg_ShouldReturnFalse_ForNonParsableLines(string line)
    {
        var args = new object?[] { line, null };
        var parsed = (bool)TryParsePrivMsgMethod.Invoke(null, args)!;

        Assert.False(parsed);
    }
}
