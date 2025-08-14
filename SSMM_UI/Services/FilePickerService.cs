using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace SSMM_UI.Services;

public class FilePickerService : IFilePickerService
{
    private readonly Window _window;

    public FilePickerService(Window window)
    {
        _window = window;
    }

    public async Task<Bitmap?> PickImageAsync()
    {
        var options = new FilePickerOpenOptions
        {
            Title = "Select Thumbnail Image",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
            new FilePickerFileType("Image Files")
            {
                Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp" }
            }
        }
        };

        var files = await _window.StorageProvider.OpenFilePickerAsync(options);

        if (files is { Count: > 0 })
        {
            await using var stream = await files[0].OpenReadAsync();
            return new Bitmap(stream);
        }

        return null;
    }
}
public interface IFilePickerService
{
    Task<Bitmap?> PickImageAsync();
}