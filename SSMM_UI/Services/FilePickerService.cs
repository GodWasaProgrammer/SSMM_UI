using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using System.Threading.Tasks;

namespace SSMM_UI.Services;

public class FilePickerService : IFilePickerService
{
    private readonly Window _window;

    public FilePickerService(Window window)
    {
        _window = window;
    }

    public async Task<(Bitmap?, string Path)?> PickImageAsync()
    {
        var options = new FilePickerOpenOptions
        {
            Title = "Select Thumbnail Image",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
            new FilePickerFileType("Image Files")
            {
                Patterns = ["*.jpg", "*.jpeg", "*.png", "*.bmp"]
            }
        }
        };

        var files = await _window.StorageProvider.OpenFilePickerAsync(options);

        if (files is { Count: > 0 })
        {
            await using var stream = await files[0].OpenReadAsync();

            var returntuple = (new Bitmap(stream), files[0].Path.LocalPath);
            return returntuple;
        }

        return null;
    }
}
public interface IFilePickerService
{
    Task<(Bitmap?, string Path)?> PickImageAsync();
}