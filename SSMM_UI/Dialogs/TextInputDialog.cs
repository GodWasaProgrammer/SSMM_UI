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
            var textBox = this.FindControl<TextBox>("InputTextBox");
            if (textBox != null)
            {
                Close(textBox.Text);
            }
            else
            {
                // Hantera fallet när TextBox inte hittas
                Close(string.Empty);

                // Alternativt: logga ett fel eller kasta exception
                // throw new InvalidOperationException("InputTextBox not found");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close(null);
        }
    }
}
