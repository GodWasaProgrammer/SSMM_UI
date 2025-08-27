using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SSMM_UI.Dialogs;
using SSMM_UI.RTMP;
using SSMM_UI.ViewModel;

namespace SSMM_UI.Views;

public partial class ServerDetailsWindow : Window
{
    public ServerDetailsWindow(RtmpServiceGroup serviceGroup, Action<bool, string, RtmpServerInfo, RtmpServiceGroup> callback)
    {
        InitializeComponent();

        // ViewModel förväntar sig (serviceGroup, window, callback) men du skickar bara (serviceGroup, callback)
        DataContext = new ServerDetailsWindowViewModel(serviceGroup, this,
            (success, streamKey, server) => callback(success, streamKey, server, serviceGroup));
    }
}