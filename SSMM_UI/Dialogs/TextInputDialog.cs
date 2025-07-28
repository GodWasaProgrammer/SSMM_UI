using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace SSMM_UI.Dialogs
{
    public partial class TextInputDialog : Window
    {
        public static readonly StyledProperty<string> WatermarkProperty =
            AvaloniaProperty.Register<TextInputDialog, string>(nameof(Watermark));

        public string Watermark
        {
            get => GetValue(WatermarkProperty);
            set => SetValue(WatermarkProperty, value);
        }

        public TextInputDialog()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Close(this.FindControl<TextBox>("InputTextBox").Text);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close(null);
        }
    }
}
