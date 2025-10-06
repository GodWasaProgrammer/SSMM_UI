using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using SSMM_UI.Services;
using System;
namespace SSMM_UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closed += (_, _) =>
        {
            App.Services?.GetRequiredService<StateService>().SerializeServices();
            App.Services?.GetRequiredService<StateService>().SerializeSettings();
            App.Services?.GetRequiredService<StateService>().SaveWindowPosition(this.Height,this.Width,this.Position,this.WindowState);
        };
    }
}