namespace SSMM_UI.Helpers;

using SSMM_UI.Enums;
using System;
using System.IO;

public static class StorageHelper
{
    public static string CompanyName { get; set; } = "GWAP Technologies";
    public static string AppName { get; set; } = "Streamer & Social Media Manager";

    /// <summary>
    /// Root folder for User specific settings(roaming).
    /// Ex: C:\Users\<user>\AppData\Roaming\GWAP Technologies\SSMM
    /// </summary>
    public static string RoamingDataPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), CompanyName, AppName);

    /// <summary>
    /// Root folder for user specific cache or temp files.
    /// Ex: C:\Users\<user>\AppData\Local\GWAP Technologies\SSMM
    /// </summary>
    public static string LocalDataPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), CompanyName, AppName);

    /// <summary>
    /// Root folder for shared data between users
    /// Ex: C:\ProgramData\GWAP Technologies\SSMM
    /// </summary>
    public static string SharedDataPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), CompanyName, AppName);

    /// <summary>
    /// Returns a subfolder for the selected scope, and creates if missing
    /// </summary>
    public static string GetOrCreateDirectory(StorageScope scope, string? subfolder = null)
    {
        string basePath = scope switch
        {
            StorageScope.Roaming => RoamingDataPath,
            StorageScope.Local => LocalDataPath,
            StorageScope.Shared => SharedDataPath,
            _ => RoamingDataPath
        };

        string fullPath = subfolder == null ? basePath : Path.Combine(basePath, subfolder);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    /// <summary>
    /// Returns a full path to the file in the selected scope.
    /// Creates folder if it does not exist.
    /// </summary>
    public static string GetFilePath(StorageScope scope, string fileName, string? subfolder = null)
    {
        string dir = GetOrCreateDirectory(scope, subfolder);
        return Path.Combine(dir, fileName);
    }
}