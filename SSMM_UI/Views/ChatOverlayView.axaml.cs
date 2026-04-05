using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace SSMM_UI.Views;

public partial class ChatOverlayView : UserControl
{
    public ChatOverlayView()
    {
        InitializeComponent();
    }

    private void OnDragHandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (this.GetVisualRoot() is Window window)
        {
            window.BeginMoveDrag(e);
            e.Handled = true;
        }
    }
}
