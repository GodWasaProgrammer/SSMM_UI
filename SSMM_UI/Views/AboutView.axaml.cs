using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using SSMM_UI.Messenger;
using SSMM_UI.ViewModel;

namespace SSMM_UI.Views;

public partial class AboutView : Window
{
    public AboutView()
    {
        InitializeComponent();
    }

    public AboutView(AboutViewModel aboutVM)
    {
        InitializeComponent();
        DataContext = aboutVM;

        // Registrera f�r CloseWindowMessage
        WeakReferenceMessenger.Default.Register<CloseWindowMessage>(this, (recipient, message) =>
        {
            this.Close();
        });
    }
}
