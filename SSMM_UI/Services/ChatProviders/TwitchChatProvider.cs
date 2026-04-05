using SSMM_UI.DTO;
using SSMM_UI.Enums;
using SSMM_UI.Interfaces;
using System;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SSMM_UI.Oauth.Twitch;

namespace SSMM_UI.Services.ChatProviders;

public class TwitchChatProvider : IChatProvider
{
    private const string TwitchIrcHost = "irc.chat.twitch.tv";
    private const int TwitchIrcPort = 6697;
    private readonly ILogService _logService;
    private readonly StateService _stateService;
    private TcpClient? _tcpClient;
    private SslStream? _sslStream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private Task? _readLoopTask;
    private CancellationTokenSource? _readLoopCts;
    private string? _connectedChannel;

    public TwitchChatProvider(ILogService logService, StateService stateService)
    {
        _logService = logService;
        _stateService = stateService;
    }

    public AuthProvider Provider => AuthProvider.Twitch;
    public event Action<ChatMessageDto>? ChatMessageReceived;
    public event Action<ChatProviderStatusDto>? StatusChanged;

    /// <summary>
    /// Connects to Twitch IRC and starts ingesting live chat messages.
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (!TryGetTwitchToken(out var token))
        {
            const string reason = "No valid Twitch token available.";
            StatusChanged?.Invoke(new ChatProviderStatusDto(Provider, false, reason, ChatProviderRuntimeState.Disconnected));
            return;
        }

        var username = token.Username?.Trim();
        if (string.IsNullOrWhiteSpace(username))
        {
            const string reason = "Twitch token is missing username/channel context.";
            StatusChanged?.Invoke(new ChatProviderStatusDto(Provider, false, reason, ChatProviderRuntimeState.Unavailable));
            return;
        }

        await DisconnectAsync(cancellationToken);

        StatusChanged?.Invoke(new ChatProviderStatusDto(Provider, false, "Connecting to Twitch IRC", ChatProviderRuntimeState.Connecting));

        try
        {
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(TwitchIrcHost, TwitchIrcPort, cancellationToken);

            _sslStream = new SslStream(_tcpClient.GetStream(), leaveInnerStreamOpen: false);
            await _sslStream.AuthenticateAsClientAsync(TwitchIrcHost);

            _reader = new StreamReader(_sslStream, Encoding.UTF8, leaveOpen: true);
            _writer = new StreamWriter(_sslStream, new UTF8Encoding(false), leaveOpen: true)
            {
                NewLine = "\r\n",
                AutoFlush = true
            };

            var oauthToken = token.AccessToken.Trim();
            var nick = username.ToLowerInvariant();
            _connectedChannel = nick;

            await _writer.WriteLineAsync($"PASS oauth:{oauthToken}");
            await _writer.WriteLineAsync($"NICK {nick}");
            await _writer.WriteLineAsync("CAP REQ :twitch.tv/tags twitch.tv/commands");
            await _writer.WriteLineAsync($"JOIN #{nick}");

            _readLoopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _readLoopTask = Task.Run(() => ReadLoopAsync(_readLoopCts.Token), _readLoopCts.Token);

            _logService.Log($"Twitch chat provider connected for channel {nick}.");
            StatusChanged?.Invoke(new ChatProviderStatusDto(Provider, true, null, ChatProviderRuntimeState.Connected));
        }
        catch (OperationCanceledException)
        {
            StatusChanged?.Invoke(new ChatProviderStatusDto(Provider, false, "Connection cancelled.", ChatProviderRuntimeState.Disconnected));
        }
        catch (Exception ex)
        {
            CleanupConnectionResources();
            _logService.Log($"Twitch chat provider unavailable: {ex.GetType().Name}");
            StatusChanged?.Invoke(new ChatProviderStatusDto(Provider, false, "Unable to connect to Twitch chat transport.", ChatProviderRuntimeState.Unavailable));
        }
    }

    private bool TryGetTwitchToken(out TwitchToken token)
    {
        token = null!;

        if (_stateService.AuthObjects.TryGetValue(AuthProvider.Twitch, out var inMemoryToken) &&
            inMemoryToken is TwitchToken twitchToken &&
            twitchToken.IsValid)
        {
            token = twitchToken;
            return true;
        }

        var persistedToken = _stateService.DeserializeToken<TwitchToken>(AuthProvider.Twitch);
        if (persistedToken is null || !persistedToken.IsValid)
        {
            return false;
        }

        token = persistedToken;
        return true;
    }

    /// <summary>
    /// Disconnects Twitch IRC ingestion and releases transport resources.
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            _readLoopCts?.Cancel();
            if (_writer is not null && !string.IsNullOrWhiteSpace(_connectedChannel))
            {
                await _writer.WriteLineAsync($"PART #{_connectedChannel}");
                await _writer.WriteLineAsync("QUIT");
            }
        }
        catch
        {
            // No-op: disconnect path should be best effort only.
        }
        finally
        {
            if (_readLoopTask is not null)
            {
                try
                {
                    await _readLoopTask.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
                }
                catch
                {
                    // No-op: continue cleanup regardless.
                }
            }

            CleanupConnectionResources();
            _logService.Log("Twitch chat provider disconnected.");
            StatusChanged?.Invoke(new ChatProviderStatusDto(Provider, false, "Disconnected", ChatProviderRuntimeState.Disconnected));
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        if (_reader is null || _writer is null)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await _reader.ReadLineAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
                StatusChanged?.Invoke(new ChatProviderStatusDto(Provider, false, "Twitch chat transport dropped.", ChatProviderRuntimeState.Faulted));
                break;
            }
            catch (Exception ex)
            {
                _logService.Log($"Twitch chat read loop faulted: {ex.GetType().Name}");
                StatusChanged?.Invoke(new ChatProviderStatusDto(Provider, false, "Twitch chat read loop faulted.", ChatProviderRuntimeState.Faulted));
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("PING ", StringComparison.Ordinal))
            {
                await _writer.WriteLineAsync(line.Replace("PING", "PONG", StringComparison.Ordinal));
                continue;
            }

            if (TryParsePrivMsg(line, out var message))
            {
                ChatMessageReceived?.Invoke(message);
            }
        }
    }

    private static bool TryParsePrivMsg(string line, out ChatMessageDto message)
    {
        message = default!;

        var privMsgMarker = " PRIVMSG #";
        var markerIndex = line.IndexOf(privMsgMarker, StringComparison.Ordinal);
        if (markerIndex <= 0)
        {
            return false;
        }

        var payloadMarker = " :";
        var payloadIndex = line.IndexOf(payloadMarker, markerIndex + privMsgMarker.Length, StringComparison.Ordinal);
        if (payloadIndex <= 0 || payloadIndex + payloadMarker.Length >= line.Length)
        {
            return false;
        }

        var content = line[(payloadIndex + payloadMarker.Length)..];
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var author = "unknown";
        var displayNameTag = ExtractTagValue(line, "display-name");
        if (!string.IsNullOrWhiteSpace(displayNameTag))
        {
            author = displayNameTag;
        }
        else
        {
            var bangIndex = line.IndexOf('!');
            if (line.StartsWith(':') && bangIndex > 1)
            {
                author = line[1..bangIndex];
            }
        }

        var id = ExtractTagValue(line, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            var hash = Math.Abs(HashCode.Combine(author, content));
            id = $"twitch-{hash}";
        }

        message = new ChatMessageDto(
            AuthProvider.Twitch,
            author,
            content,
            DateTime.UtcNow,
            false,
            id);

        return true;
    }

    private static string? ExtractTagValue(string line, string key)
    {
        if (!line.StartsWith('@'))
        {
            return null;
        }

        var tagsEnd = line.IndexOf(' ');
        if (tagsEnd <= 1)
        {
            return null;
        }

        var tagsSection = line[1..tagsEnd];
        var parts = tagsSection.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var prefix = $"{key}=";
        var tag = parts.FirstOrDefault(x => x.StartsWith(prefix, StringComparison.Ordinal));

        if (tag is null || tag.Length == prefix.Length)
        {
            return null;
        }

        return tag[prefix.Length..];
    }

    private void CleanupConnectionResources()
    {
        _readLoopCts?.Dispose();
        _readLoopCts = null;
        _readLoopTask = null;

        _writer?.Dispose();
        _writer = null;

        _reader?.Dispose();
        _reader = null;

        _sslStream?.Dispose();
        _sslStream = null;

        _tcpClient?.Dispose();
        _tcpClient = null;

        _connectedChannel = null;
    }
}
