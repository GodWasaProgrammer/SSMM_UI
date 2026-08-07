using SSMM_UI.DTO;
using SSMM_UI.Enums;
using SSMM_UI.Services.ChatProviders.Resolvers;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SSMM_UI.Services.ChatProviders.ChatClients;

public class KickChatClient
{
    private readonly KickResolver _resolver;
    private readonly ILogService _logService;

    private ClientWebSocket? _socket;

    public event Action<ChatMessageDto>? MessageReceived;


    public KickChatClient(
        KickResolver resolver,
        ILogService logService)
    {
        _resolver = resolver;
        _logService = logService;
    }


    public async Task ConnectAsync(
       string channelName,
       CancellationToken token)
    {
        var chatroomId =
            await _resolver.ResolveChatroomId(channelName);


        if (chatroomId == null)
            throw new Exception("Could not resolve chatroom");


        _socket = new ClientWebSocket();


        await _socket.ConnectAsync(
            new Uri(
                "wss://ws-us2.pusher.com/app/32cbd69e4b950bf97679?protocol=7&client=js&version=8.5.0&flash=false"),
            token);


        var subscribe =
    "{\"event\":\"pusher:subscribe\",\"data\":{\"channel\":\"chatrooms."
    + chatroomId +
    ".v2\"}}";


        await SendAsync(subscribe, token);


        _ = ReceiveLoop(token);
    }

    private async Task SendAsync(
    string message,
    CancellationToken token)
    {
        var bytes = Encoding.UTF8.GetBytes(message);

        await _socket!.SendAsync(
            bytes,
            WebSocketMessageType.Text,
            true,
            token);
    }

    private async Task ReceiveLoop(
    CancellationToken token)
    {
        var buffer = new byte[8192];

        try
        {
            while (!token.IsCancellationRequested &&
                   _socket?.State == WebSocketState.Open)
            {
                var result =
                    await _socket.ReceiveAsync(
                        buffer,
                        token);


                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closed",
                        CancellationToken.None);

                    break;
                }


                var json =
                    Encoding.UTF8.GetString(
                        buffer,
                        0,
                        result.Count);


                //_logService.Log(json);


                if (json.Contains("ChatMessageEvent"))
                {
                    var message = ParseChatMessage(json);

                    if (message != null)
                    {
                        MessageReceived?.Invoke(message);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
        catch (Exception ex)
        {
            _logService.Log(
                $"Kick receive loop crashed: {ex.Message}");
        }
    }

    private ChatMessageDto? ParseChatMessage(string json)
    {
        try
        {
            using var outer =
                JsonDocument.Parse(json);


            var eventName =
                outer.RootElement
                     .GetProperty("event")
                     .GetString();


            if (eventName != "App\\Events\\ChatMessageEvent")
                return null;


            // Kick skickar data som en JSON-string
            var innerJson =
                outer.RootElement
                     .GetProperty("data")
                     .GetString();


            if (string.IsNullOrWhiteSpace(innerJson))
                return null;


            using var data =
                JsonDocument.Parse(innerJson);


            var root = data.RootElement;


            var author =
                root.GetProperty("sender")
                    .GetProperty("username")
                    .GetString()
                ?? "Unknown";


            var message =
                root.GetProperty("content")
                    .GetString()
                ?? string.Empty;


            var messageId =
                root.GetProperty("id")
                    .GetString()
                ?? Guid.NewGuid().ToString();


            var timestamp =
                root.GetProperty("created_at")
                    .GetDateTime();


            return new ChatMessageDto(
                AuthProvider.Kick,
                author,
                message,
                timestamp.ToUniversalTime(),
                false,
                messageId
            );
        }
        catch (Exception ex)
        {
            _logService.Log(
                $"Failed parsing Kick message: {ex.Message}");

            return null;
        }
    }

    public async Task DisconnectAsync(
    CancellationToken token)
    {
        if (_socket == null)
            return;


        if (_socket.State == WebSocketState.Open)
        {
            await _socket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Disconnect requested",
                token);
        }


        _socket.Dispose();
        _socket = null;
    }
}