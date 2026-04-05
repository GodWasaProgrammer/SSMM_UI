using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Controls.Platform;
using SSMM_UI.ViewModel;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SSMM_UI.Views;

public partial class ChatOverlayWindow : Window
{
    private ChatOverlayViewModel? _currentViewModel;
    private IntPtr _windowHandle;
    private IntPtr _originalWindowProc;
    private WndProcDelegate? _windowProcDelegate;

    public ChatOverlayWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        if (DataContext is ChatOverlayViewModel viewModel && viewModel.CloseOverlayCommand.CanExecute(null))
        {
            viewModel.CloseOverlayCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        EnsureWindowProcHooked();
        ApplyClickThroughFromViewModel();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        UnhookWindowProc();
        DetachFromViewModel();
        DataContextChanged -= OnDataContextChanged;
        Opened -= OnOpened;
        Closed -= OnClosed;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        AttachToViewModel(DataContext as ChatOverlayViewModel);
    }

    private void AttachToViewModel(ChatOverlayViewModel? viewModel)
    {
        DetachFromViewModel();
        _currentViewModel = viewModel;

        if (_currentViewModel is null)
        {
            return;
        }

        _currentViewModel.PropertyChanged += OnOverlayViewModelPropertyChanged;
        ApplyClickThroughFromViewModel();
    }

    private void DetachFromViewModel()
    {
        if (_currentViewModel is null)
        {
            return;
        }

        _currentViewModel.PropertyChanged -= OnOverlayViewModelPropertyChanged;
        _currentViewModel = null;
    }

    private void OnOverlayViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatOverlayViewModel.IsClickThrough))
        {
            ApplyClickThroughFromViewModel();
        }
    }

    private void ApplyClickThroughFromViewModel()
    {
        if (_currentViewModel is null)
        {
            return;
        }

        ApplyClickThrough(_currentViewModel.IsClickThrough);
    }

    /// <summary>
    /// Applies click-through mode using a guarded Windows-specific implementation.
    /// On unsupported platforms this is intentionally a no-op fallback.
    /// </summary>
    private void ApplyClickThrough(bool enabled)
    {
        if (!OperatingSystem.IsWindows())
        {
            Debug.WriteLine("ChatOverlayWindow: click-through is not supported on this platform. Ignoring toggle.");
            return;
        }

        var platformHandle = this.TryGetPlatformHandle();
        if (platformHandle is null || platformHandle.Handle == IntPtr.Zero)
        {
            // Window handle can be unavailable during startup; mode will be retried on next trigger/open.
            return;
        }

        try
        {
            var exStyle = GetWindowLongPtrCompat(platformHandle.Handle, GwlExStyle).ToInt64();
            var hasTransparent = (exStyle & WsExTransparent) == WsExTransparent;
            var hasLayered = (exStyle & WsExLayered) == WsExLayered;
            var hasNoActivate = (exStyle & WsExNoActivate) == WsExNoActivate;
            var targetStyle = enabled
                ? exStyle | WsExTransparent | WsExLayered | WsExNoActivate
                : exStyle & ~WsExTransparent & ~WsExNoActivate;

            if (hasTransparent == enabled && (!enabled || (hasLayered && hasNoActivate)))
            {
                return;
            }

            _ = SetWindowLongPtrCompat(platformHandle.Handle, GwlExStyle, new IntPtr(targetStyle));

            // Force style refresh on Windows after toggling extended styles.
            _ = SetWindowPos(
                platformHandle.Handle,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                SwpNomove | SwpNosize | SwpNozorder | SwpFramechanged | SwpNoactivate);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ChatOverlayWindow: failed to apply click-through mode: {ex.Message}");
        }
    }

    private void EnsureWindowProcHooked()
    {
        if (!OperatingSystem.IsWindows() || _windowHandle != IntPtr.Zero)
        {
            return;
        }

        var platformHandle = this.TryGetPlatformHandle();
        if (platformHandle is null || platformHandle.Handle == IntPtr.Zero)
        {
            return;
        }

        _windowHandle = platformHandle.Handle;
        _windowProcDelegate = WindowProc;
        var replacementPtr = Marshal.GetFunctionPointerForDelegate(_windowProcDelegate);
        _originalWindowProc = SetWindowLongPtrCompat(_windowHandle, GwlpWndProc, replacementPtr);
    }

    private void UnhookWindowProc()
    {
        if (!OperatingSystem.IsWindows() || _windowHandle == IntPtr.Zero)
        {
            return;
        }

        if (_originalWindowProc != IntPtr.Zero)
        {
            _ = SetWindowLongPtrCompat(_windowHandle, GwlpWndProc, _originalWindowProc);
        }

        _windowHandle = IntPtr.Zero;
        _originalWindowProc = IntPtr.Zero;
        _windowProcDelegate = null;
    }

    private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (_currentViewModel?.IsClickThrough == true && msg == WmNcHitTest)
        {
            return new IntPtr(HtTransparent);
        }

        if (_originalWindowProc == IntPtr.Zero)
        {
            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        return CallWindowProc(_originalWindowProc, hWnd, msg, wParam, lParam);
    }

    private static IntPtr GetWindowLongPtrCompat(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, nIndex)
            : new IntPtr(GetWindowLong32(hWnd, nIndex));
    }

    private static IntPtr SetWindowLongPtrCompat(IntPtr hWnd, int nIndex, IntPtr newLong)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, newLong)
            : new IntPtr(SetWindowLong32(hWnd, nIndex, newLong.ToInt32()));
    }

    private const int GwlExStyle = -20;
    private const int GwlpWndProc = -4;
    private const long WsExTransparent = 0x20L;
    private const long WsExLayered = 0x80000L;
    private const long WsExNoActivate = 0x8000000L;
    private const uint WmNcHitTest = 0x0084;
    private const int HtTransparent = -1;
    private const uint SwpNosize = 0x0001;
    private const uint SwpNomove = 0x0002;
    private const uint SwpNozorder = 0x0004;
    private const uint SwpNoactivate = 0x0010;
    private const uint SwpFramechanged = 0x0020;
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallWindowProc(
        IntPtr lpPrevWndFunc,
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr DefWindowProc(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam);
}
