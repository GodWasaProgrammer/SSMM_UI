using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using SSMM_UI.Messenger;

namespace SSMM_UI.Views;

public partial class SelectedServiceView : Window
{
    public SelectedServiceView()
    {
        InitializeComponent();
        WeakReferenceMessenger.Default.Register<CloseWindowMessage>(this, (recipient, message) =>
        {
            this.Close();
        });
    }
}