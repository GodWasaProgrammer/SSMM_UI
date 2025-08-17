using Avalonia.Controls;
using Avalonia.Interactivity;
using SSMM_UI.Dialogs;
using SSMM_UI.ViewModel;

namespace SSMM_UI
{
    public partial class ServerDetailsWindow : Window
    {
        public RtmpServiceGroup ServiceGroup { get; }
        public RtmpServerInfo SelectedServer { get; set; }

        public ServerDetailsWindow()
        {
            InitializeComponent();
        }

        public ServerDetailsWindow(RtmpServiceGroup serviceGroup)
        {
            InitializeComponent();
            ServiceGroup = serviceGroup;
            DataContext = this;
        }

        private async void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedServer == null)
            {
                await MessageBox.Show(this, "Please select a server first.", "No Server Selected");
                return;
            }

            var dialog = new TextInputDialog
            {
                Title = "Enter Stream Key",
                Watermark = $"Stream key for {SelectedServer.ServerName}"
            };

            var streamKey = await dialog.ShowDialog<string>(this);

            if (!string.IsNullOrWhiteSpace(streamKey))
            {
                if (Owner?.DataContext is MainWindowViewModel mainVm)
                {

                    mainVm.LeftSideBarViewModel.SelectedServicesToStream.Add(new SelectedService
                    {
                        ServiceGroup = ServiceGroup,
                        SelectedServer = SelectedServer,
                        StreamKey = streamKey
                    });
                }
                Close(true);
            }
            else
            {
                Close(false);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}