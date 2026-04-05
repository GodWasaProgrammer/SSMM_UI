using Moq;
using SSMM_UI.DTO;
using SSMM_UI.Enums;
using SSMM_UI.Interfaces;
using SSMM_UI.RTMP;
using SSMM_UI.Services;
using SSMM_UI.Settings;

namespace SSMM_UI.Tests.ServicesTests;

public class ChatAggregationServiceTests
{
    [Fact]
    public async Task RefreshConnectionsAsync_ShouldConnect_WhenActiveServiceAndTokenExist()
    {
        var logger = new Mock<ILogService>();
        var state = new Mock<StateService>(logger.Object) { CallBase = true };
        var token = new Mock<IAuthToken>();
        token.SetupGet(x => x.IsValid).Returns(true);
        state.Object.AuthObjects[AuthProvider.Twitch] = token.Object;
        state.Object.SelectedServicesToStream.Add(new SelectedService
        {
            IsActive = true,
            ServiceGroup = new RtmpServiceGroup { ServiceName = "Twitch" }
        });

        var provider = new Mock<IChatProvider>();
        provider.SetupGet(x => x.Provider).Returns(AuthProvider.Twitch);
        provider.Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        provider.Setup(x => x.DisconnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var registry = new ChatProviderRegistryService(new[] { provider.Object });
        var service = new ChatAggregationService(logger.Object, state.Object, registry, new ChatConcatenationPolicy());

        await service.RefreshConnectionsAsync(TestContext.Current.CancellationToken);

        provider.Verify(x => x.ConnectAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ApplySettings_ShouldNormalizeInvalidValues()
    {
        var logger = new Mock<ILogService>();
        var state = new Mock<StateService>(logger.Object) { CallBase = true };
        var registry = new ChatProviderRegistryService(Array.Empty<IChatProvider>());
        var service = new ChatAggregationService(logger.Object, state.Object, registry, new ChatConcatenationPolicy());
        var settings = new UserSettings
        {
            ChatOverlay = new ChatOverlaySettings
            {
                MaxMessages = 0,
                ConcatenationWindowSeconds = 0
            }
        };

        service.ApplySettings(settings);

        Assert.Equal(200, settings.ChatOverlay.MaxMessages);
        Assert.Equal(8, settings.ChatOverlay.ConcatenationWindowSeconds);
    }

    [Fact]
    public async Task RefreshConnectionsAsync_ShouldExposeUnavailableStatus_WhenProviderNotImplemented()
    {
        var logger = new Mock<ILogService>();
        var state = new Mock<StateService>(logger.Object) { CallBase = true };
        var token = new Mock<IAuthToken>();
        token.SetupGet(x => x.IsValid).Returns(true);
        state.Object.AuthObjects[AuthProvider.Kick] = token.Object;
        state.Object.SelectedServicesToStream.Add(new SelectedService
        {
            IsActive = true,
            ServiceGroup = new RtmpServiceGroup { ServiceName = "Kick" }
        });

        var provider = new Mock<IChatProvider>();
        provider.SetupGet(x => x.Provider).Returns(AuthProvider.Kick);
        provider.SetupAdd(x => x.ChatMessageReceived += It.IsAny<Action<ChatMessageDto>>());
        provider.SetupRemove(x => x.ChatMessageReceived -= It.IsAny<Action<ChatMessageDto>>());
        provider.SetupAdd(x => x.StatusChanged += It.IsAny<Action<ChatProviderStatusDto>>())
            .Callback<Action<ChatProviderStatusDto>>(handler =>
                handler(new ChatProviderStatusDto(
                    AuthProvider.Kick,
                    false,
                    "Kick chat transport is not implemented in this build.",
                    ChatProviderRuntimeState.Unavailable)));
        provider.SetupRemove(x => x.StatusChanged -= It.IsAny<Action<ChatProviderStatusDto>>());
        provider.Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        provider.Setup(x => x.DisconnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var registry = new ChatProviderRegistryService(new[] { provider.Object });
        var service = new ChatAggregationService(logger.Object, state.Object, registry, new ChatConcatenationPolicy());

        await service.RefreshConnectionsAsync(TestContext.Current.CancellationToken);

        Assert.Contains(service.ProviderStatuses, status =>
            status.Provider == AuthProvider.Kick &&
            status.State == ChatProviderRuntimeState.Unavailable);
    }

    [Fact]
    public async Task RefreshConnectionsAsync_ShouldExposeUnavailableStatus_WhenProviderIsNotRegistered()
    {
        var logger = new Mock<ILogService>();
        var state = new Mock<StateService>(logger.Object) { CallBase = true };
        var token = new Mock<IAuthToken>();
        token.SetupGet(x => x.IsValid).Returns(true);
        state.Object.AuthObjects[AuthProvider.Twitch] = token.Object;
        state.Object.SelectedServicesToStream.Add(new SelectedService
        {
            IsActive = true,
            ServiceGroup = new RtmpServiceGroup { ServiceName = "Twitch" }
        });

        var service = new ChatAggregationService(
            logger.Object,
            state.Object,
            new ChatProviderRegistryService(Array.Empty<IChatProvider>()),
            new ChatConcatenationPolicy());

        await service.RefreshConnectionsAsync(TestContext.Current.CancellationToken);

        Assert.Contains(service.ProviderStatuses, status =>
            status.Provider == AuthProvider.Twitch &&
            status.State == ChatProviderRuntimeState.Unavailable &&
            status.Reason == "Provider is not registered.");
    }

    [Fact]
    public void TryInjectSyntheticMessage_ShouldInject_WhenProviderUnavailable()
    {
        var logger = new Mock<ILogService>();
        var state = new Mock<StateService>(logger.Object) { CallBase = true };
        var service = new ChatAggregationService(
            logger.Object,
            state.Object,
            new ChatProviderRegistryService(Array.Empty<IChatProvider>()),
            new ChatConcatenationPolicy());

        var canInjectWhenConnected = service.TryInjectSyntheticMessage(AuthProvider.Twitch, "seed", out var blockedReason);
        Assert.False(canInjectWhenConnected);
        Assert.Contains("disabled", blockedReason, StringComparison.OrdinalIgnoreCase);

        var provider = new Mock<IChatProvider>();
        provider.SetupGet(x => x.Provider).Returns(AuthProvider.Kick);
        provider.SetupAdd(x => x.ChatMessageReceived += It.IsAny<Action<ChatMessageDto>>());
        provider.SetupRemove(x => x.ChatMessageReceived -= It.IsAny<Action<ChatMessageDto>>());
        provider.SetupAdd(x => x.StatusChanged += It.IsAny<Action<ChatProviderStatusDto>>())
            .Callback<Action<ChatProviderStatusDto>>(handler =>
                handler(new ChatProviderStatusDto(
                    AuthProvider.Kick,
                    false,
                    "Unavailable",
                    ChatProviderRuntimeState.Unavailable)));
        provider.SetupRemove(x => x.StatusChanged -= It.IsAny<Action<ChatProviderStatusDto>>());
        provider.Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        provider.Setup(x => x.DisconnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        state.Object.AuthObjects[AuthProvider.Kick] = new Mock<IAuthToken>().Object;
        state.Object.SelectedServicesToStream.Add(new SelectedService
        {
            IsActive = true,
            ServiceGroup = new RtmpServiceGroup { ServiceName = "Kick" }
        });

        var serviceWithStatus = new ChatAggregationService(
            logger.Object,
            state.Object,
            new ChatProviderRegistryService(new[] { provider.Object }),
            new ChatConcatenationPolicy());

        serviceWithStatus.RefreshConnectionsAsync(TestContext.Current.CancellationToken).GetAwaiter().GetResult();

        var injected = serviceWithStatus.TryInjectSyntheticMessage(AuthProvider.Kick, "seed", out var reason);

        Assert.True(injected);
        Assert.Contains("injected", reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(serviceWithStatus.Messages, x => x.IsSystem && x.Message.Contains("Synthetic(seed) #1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TryInjectSyntheticMessage_ShouldBlock_WhenProviderIsConnecting()
    {
        var logger = new Mock<ILogService>();
        var state = new Mock<StateService>(logger.Object) { CallBase = true };

        var token = new Mock<IAuthToken>();
        token.SetupGet(x => x.IsValid).Returns(true);
        state.Object.AuthObjects[AuthProvider.Twitch] = token.Object;
        state.Object.SelectedServicesToStream.Add(new SelectedService
        {
            IsActive = true,
            ServiceGroup = new RtmpServiceGroup { ServiceName = "Twitch" }
        });

        var provider = new Mock<IChatProvider>();
        provider.SetupGet(x => x.Provider).Returns(AuthProvider.Twitch);
        provider.SetupAdd(x => x.ChatMessageReceived += It.IsAny<Action<ChatMessageDto>>());
        provider.SetupRemove(x => x.ChatMessageReceived -= It.IsAny<Action<ChatMessageDto>>());
        provider.SetupAdd(x => x.StatusChanged += It.IsAny<Action<ChatProviderStatusDto>>())
            .Callback<Action<ChatProviderStatusDto>>(handler =>
                handler(new ChatProviderStatusDto(
                    AuthProvider.Twitch,
                    false,
                    "Connecting",
                    ChatProviderRuntimeState.Connecting)));
        provider.SetupRemove(x => x.StatusChanged -= It.IsAny<Action<ChatProviderStatusDto>>());
        provider.Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        provider.Setup(x => x.DisconnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var service = new ChatAggregationService(
            logger.Object,
            state.Object,
            new ChatProviderRegistryService(new[] { provider.Object }),
            new ChatConcatenationPolicy());

        await service.RefreshConnectionsAsync(TestContext.Current.CancellationToken);

        var injected = service.TryInjectSyntheticMessage(AuthProvider.Twitch, "seed", out var reason);
        Assert.False(injected);
        Assert.Contains("disabled", reason, StringComparison.OrdinalIgnoreCase);
    }
}
