using Avalonia;
using Avalonia.Controls;

namespace SSMM_UI.Settings;

public class WindowSettings
{
    public WindowSettings() 
    {

    }
    public double Width { get; set; }
    public double Height { get; set; }
    public PixelPoint Pos { get; set; }
    public WindowState WindowState { get; set; }
}