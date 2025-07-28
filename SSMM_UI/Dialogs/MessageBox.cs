using Avalonia.Controls;
using System.Threading.Tasks;

namespace SSMM_UI.Dialogs
{
    public static class MessageBox
    {
        public static async Task Show(Window parent, string message, string title = "")
        {
            var dialog = new Window
            {
                Title = title,
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new TextBlock
                {
                    Text = message,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                }
            };

            await dialog.ShowDialog(parent);
        }
    }
}
