using Avalonia.Controls;

namespace SSMM_UI;

public partial class ServerDetailsWindow : Window
{
    public ServerDetailsWindow(RtmpServiceGroup group)
    {
        InitializeComponent();
        DataContext = group;
    }
}