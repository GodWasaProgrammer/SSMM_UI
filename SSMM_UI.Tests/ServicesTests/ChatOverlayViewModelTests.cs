using Moq;
using SSMM_UI.DTO;
using SSMM_UI.Enums;
using SSMM_UI.Interfaces;
using SSMM_UI.Services;
using SSMM_UI.Settings;
using SSMM_UI.ViewModel;

namespace SSMM_UI.Tests.ServicesTests;

public class ChatOverlayViewModelTests
{
    [Fact]
    public async Task RefreshConnectionsAsync_ShouldNotCallAggregation_WhenOverlayDisabled()
    {
        var logger = new Mock<ILogService>();
        var state = new Mock<StateService>(logger.Object) { CallBase = true };
        state.Object.UserSettingsObj.ChatOverlay.Enabled = false;

        var provider = new Mock<IChatProvider>();
        provider.SetupGet(x => x.Provider).Returns(AuthProvider.Twitch);

        var aggregation = new ChatAggregationService(
            logger.Object,
            state.Object,
            new ChatProviderRegistryService(new[] { provider.Object }),
            new ChatConcatenationPolicy());

        var vm = new ChatOverlayViewModel(aggregation, state.Object);
        await vm.RefreshConnectionsAsync();

        provider.Verify(x => x.ConnectAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void CloseOverlayCommand_ShouldRaiseCloseRequestedEvent()
    {
        var logger = new Mock<ILogService>();
        var state = new Mock<StateService>(logger.Object) { CallBase = true };
        var aggregation = new ChatAggregationService(
            logger.Object,
            state.Object,
            new ChatProviderRegistryService(Array.Empty<IChatProvider>()),
            new ChatConcatenationPolicy());

        var vm = new ChatOverlayViewModel(aggregation, state.Object);
        var raised = false;
        vm.CloseOverlayRequested += (_, _) => raised = true;

        vm.CloseOverlayCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void OnUnavailableNonTwitchStatus_ShouldSetDebugInjectionProvider()
    {
        var logger = new Mock<ILogService>();
        var state = new Mock<StateService>(logger.Object) { CallBase = true };

        var aggregation = new ChatAggregationService(
            logger.Object,
            state.Object,
            new ChatProviderRegistryService(Array.Empty<IChatProvider>()),
            new ChatConcatenationPolicy());

        var vm = new ChatOverlayViewModel(aggregation, state.Object);
        Assert.Equal(AuthProvider.Kick, vm.DebugInjectionProvider);

        var provider = new Mock<IChatProvider>();
        provider.SetupGet(x => x.Provider).Returns(AuthProvider.YouTube);
        provider.SetupAdd(x => x.ChatMessageReceived += It.IsAny<Action<ChatMessageDto>>());
        provider.SetupRemove(x => x.ChatMessageReceived -= It.IsAny<Action<ChatMessageDto>>());
        provider.SetupAdd(x => x.StatusChanged += It.IsAny<Action<ChatProviderStatusDto>>())
            .Callback<Action<ChatProviderStatusDto>>(handler =>
                handler(new ChatProviderStatusDto(
                    AuthProvider.YouTube,
                    false,
                    "Unavailable",
                    ChatProviderRuntimeState.Unavailable)));
        provider.SetupRemove(x => x.StatusChanged -= It.IsAny<Action<ChatProviderStatusDto>>());
        provider.Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        provider.Setup(x => x.DisconnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        state.Object.AuthObjects[AuthProvider.YouTube] = new Mock<IAuthToken>().Object;
        state.Object.SelectedServicesToStream.Add(new SSMM_UI.RTMP.SelectedService
        {
            IsActive = true,
            ServiceGroup = new SSMM_UI.RTMP.RtmpServiceGroup { ServiceName = "YouTube" }
        });

        var aggregationWithStatus = new ChatAggregationService(
            logger.Object,
            state.Object,
            new ChatProviderRegistryService(new[] { provider.Object }),
            new ChatConcatenationPolicy());

        var vmWithStatus = new ChatOverlayViewModel(aggregationWithStatus, state.Object);
        aggregationWithStatus.RefreshConnectionsAsync(TestContext.Current.CancellationToken).GetAwaiter().GetResult();

        Assert.Equal(AuthProvider.YouTube, vmWithStatus.DebugInjectionProvider);
    }
}
