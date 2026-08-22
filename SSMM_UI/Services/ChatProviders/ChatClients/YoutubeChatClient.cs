using Grpc.Core;
using Grpc.Net.Client;
using SSMM_UI.Services.ChatProviders.ChatClients.Protos;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace SSMM_UI.Services.ChatProviders.ChatClients;

public sealed class YoutubeChatClient : IDisposable
{
    private readonly GrpcChannel _channel;
    private readonly V3DataLiveChatMessageService.V3DataLiveChatMessageServiceClient _client;

    public YoutubeChatClient(string liveChatId, int maxResults = 500, string? pageToken = null)
    {
        LiveChatId = liveChatId;
        MaxResults = maxResults;
        PageToken = pageToken;

        _channel = GrpcChannel.ForAddress("dns:///youtube.googleapis.com:443", new GrpcChannelOptions { Credentials = ChannelCredentials.SecureSsl });

        _client =
            new V3DataLiveChatMessageService
                .V3DataLiveChatMessageServiceClient(_channel);
    }

    public string LiveChatId { get; }

    public int MaxResults { get; }

    public string? PageToken { get; }

    public async IAsyncEnumerable<LiveChatMessage> StreamAsync(
        string accessToken,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var nextPageToken = PageToken;

        while (!cancellationToken.IsCancellationRequested)
        {
            var request = new LiveChatMessageListRequest
            {
                LiveChatId = LiveChatId,
                MaxResults = (uint)MaxResults
            };

            request.Part.Add("snippet");
            request.Part.Add("authorDetails");

            if (!string.IsNullOrWhiteSpace(nextPageToken))
            {
                request.PageToken = nextPageToken;
            }

            var headers = new Metadata
            {
                { "Authorization", $"Bearer {accessToken}" }
            };

            using var call = _client.StreamList(
                request,
                headers,
                cancellationToken: cancellationToken);

            var receivedNextPageToken = false;

            while (await call.ResponseStream.MoveNext(
                cancellationToken))
            {
                var response = call.ResponseStream.Current;

                foreach (var message in response.Items)
                {
                    yield return message;
                }

                if (!string.IsNullOrWhiteSpace(
                    response.NextPageToken))
                {
                    nextPageToken = response.NextPageToken;
                    receivedNextPageToken = true;
                }
            }

            if (!receivedNextPageToken)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        _channel.Dispose();
    }
}
