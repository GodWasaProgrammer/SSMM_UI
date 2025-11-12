using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Google.Apis.YouTube.v3.Data;
using SSMM_UI.Enums;
using SSMM_UI.Helpers;
using SSMM_UI.Interfaces;
using SSMM_UI.MetaData;
using SSMM_UI.RTMP;
using SSMM_UI.Settings;
using SSMM_UI.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SSMM_UI.Services;

public class StateService
{
    public StateService(ILogService logger)
    {
        _metaDataJsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        _webHooksPath = StorageHelper.GetFilePath(StorageScope.Roaming, _Webhooks, _webHooksFolder);
        _windowsPath = StorageHelper.GetFilePath(StorageScope.Roaming, _windowSettings, _settingsFolder);
        _metaDataPath = StorageHelper.GetFilePath(StorageScope.Roaming, _savedMetaData, _MetadataFolder);
        _savedServicesPath = StorageHelper.GetFilePath(StorageScope.Roaming, _serializedServices, _servicesFolder);
        _settingsPath = StorageHelper.GetFilePath(StorageScope.Roaming, _userSettings, _settingsFolder);

        _logger = logger;
        DeSerializeServices();
        DeserializeSettings();
        DeSerializeWebhooks();
        DeSerializeMetaData();
        LoadRtmpServersFromServicesJson(_obsServices);
        DeSerializeYoutubeCategories();
    }

    // Filenames
    private const string _serializedServices = "Serialized_Services.json";
    private const string _obsServices = "services.json";
    private const string _youtubeCategories = "youtube_categories.json";
    private const string _userSettings = "UserSettings.json";
    private const string _savedMetaData = "MetaData_State.json";
    private const string _windowSettings = "WindowSettings.json";
    private const string _Webhooks = "Webhooks.json";

    // Folders
    private const string _tokenFolder = "Tokens";
    private const string _settingsFolder = "Settings";
    private const string _MetadataFolder = "Metadata";
    private const string _servicesFolder = "Services";
    private const string _webHooksFolder = "WebHooks";

    private readonly JsonSerializerOptions _metaDataJsonOptions;
    private readonly JsonSerializerOptions _regularJsonOptions = new() { WriteIndented = true };
    private readonly Dictionary<OAuthServices, IAuthToken> _authObjects = [];
    public event Action? OnAuthObjectsUpdated;
    public Dictionary<OAuthServices, IAuthToken> AuthObjects { get { return _authObjects; } }
    public ObservableCollection<SelectedService> SelectedServicesToStream { get; private set; } = [];
    public ObservableCollection<RtmpServiceGroup> RtmpServiceGroups { get; } = [];
    public ObservableCollection<VideoCategory> YoutubeVideoCategories { get; private set; } = [];
    public ObservableCollection<KeyValueItem> Webhooks { get; private set; } = [];

    private StreamMetadata _currentMetaData { get; set; } = new StreamMetadata();

    // Paths
    private readonly string _webHooksPath;
    private readonly string _windowsPath;
    private readonly string _metaDataPath;
    private readonly string _savedServicesPath;
    private readonly string _settingsPath;

    public void SerializeWebhooks()
    {
        try
        {
            var json = JsonSerializer.Serialize(Webhooks, _regularJsonOptions);
            File.WriteAllText(_webHooksPath, json);
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to serialize webhooks: {ex.Message}");
        }
    }

    public void DeSerializeWebhooks()
    {
        try
        {
            if (!File.Exists(_webHooksPath)) return;

            var json = File.ReadAllText(_webHooksPath);
            var deserialized = JsonSerializer.Deserialize<ObservableCollection<KeyValueItem>>(json);

            if (deserialized is not null)
            {
                Webhooks.Clear();
                foreach (var item in deserialized)
                    Webhooks.Add(item);
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to deserialize webhooks: {ex.Message}");
        }
    }

    public void SaveWindowPosition(double Height, double Width, PixelPoint Position, WindowState windowState)
    {
        try
        {
            var windowstate = new WindowSettings
            {
                Width = Width,
                Height = Height,
                Pos = Position,
                WindowState = windowState
            };

            var json = JsonSerializer.Serialize(windowstate, _regularJsonOptions);
            File.WriteAllText(_windowsPath, json);
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to save window position: {ex.Message}");
        }
    }

    public static WindowSettings? LoadWindowPosition()
    {
        try
        {
            // Skapa samma sökväg som du använde i SaveWindowPosition
            var fullPath = StorageHelper.GetFilePath(
                StorageScope.Roaming,
                _windowSettings,
                _settingsFolder
            );

            if (!File.Exists(fullPath))
                return null;

            var json = File.ReadAllText(fullPath);
            return JsonSerializer.Deserialize<WindowSettings>(json);
        }
        catch (Exception ex)
        {
            // Logga gärna, men returnera null om något går fel
            //_logger.Log($"Failed to load window position: {ex.Message}");
            return null;
        }
    }

    public void SerializeToken<T>(OAuthServices service, T token) where T : class, IAuthToken
    {
        try
        {
            ArgumentNullException.ThrowIfNull(token);

            var fullPath = StorageHelper.GetFilePath(
                StorageScope.Roaming,
                $"{service}Token.json",
                _tokenFolder);

            //var json = JsonSerializer.Serialize(token, _regularJsonOptions);

            //File.WriteAllText(fullPath, json);

            SecureStorage.SaveEncrypted(fullPath, token, _regularJsonOptions);

            _authObjects[service] = token;

            OnAuthObjectsUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.Log($"{ex.Message} Failed to Serialize Token for:{service}");
        }
    }

    public T? DeserializeToken<T>(OAuthServices service) where T : class, IAuthToken
    {
        try
        {
            // Full path till tokenfilen i Roaming/Tokens
            var fullPath = StorageHelper.GetFilePath(
                StorageScope.Roaming,
                $"{service}Token.json",
                _tokenFolder);

            if (!File.Exists(fullPath))
                return null;

            //var json = File.ReadAllText(fullPath);
            var token = SecureStorage.LoadEncrypted<T>(fullPath, _regularJsonOptions);

            if (token != null)
            {
                _authObjects[service] = token;
                OnAuthObjectsUpdated?.Invoke();
            }

            return token;
        }
        catch (Exception ex)
        {
            _logger.Log($"{ex.Message} Failed to deserialize token for: {service}");
            return null;
        }
    }

    public StreamMetadata GetCurrentMetaData()
    {
        return _currentMetaData;
    }

    public void UpdateCurrentMetaData(StreamMetadata metadata)
    {
        _currentMetaData = metadata;
        SerializeMetaData();
    }

    public UserSettings UserSettingsObj { get; private set; } = new UserSettings();

    private readonly ILogService _logger;

    public void SettingsChanged(UserSettings settings)
    {
        UserSettingsObj = settings;
    }


    public void SerializeSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(UserSettingsObj, _regularJsonOptions);
            File.WriteAllText(_settingsPath, json);
        }
        catch(Exception ex)
        {
            _logger.Log($"Error in saving Settings:{ex.Message}");
        }
    }
    private void DeserializeSettings()
    {
        if (File.Exists(_settingsPath))
        {
            var json = File.ReadAllText(_settingsPath);
            var deserialized = JsonSerializer.Deserialize<UserSettings>(json);
            if (deserialized != null)
            {
                UserSettingsObj = deserialized;
            }
        }
    }

    private void SerializeMetaData()
    {
        try
        {
            string json = JsonSerializer.Serialize(_currentMetaData, _metaDataJsonOptions);
            File.WriteAllText(_metaDataPath, json);
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to serialize metadata: {ex.Message}");
        }
    }
    private void DeSerializeMetaData()
    {
        try
        {
            if (!File.Exists(_metaDataPath)) return;

            var json = File.ReadAllText(_metaDataPath);
            var deserialized = JsonSerializer.Deserialize<StreamMetadata>(json, _metaDataJsonOptions);
            if (deserialized != null)
            {
                _currentMetaData = deserialized;

                if (_currentMetaData.ThumbnailPath != null)
                {
                    _currentMetaData.Thumbnail = new Bitmap(_currentMetaData.ThumbnailPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to deserialize metadata: {ex.Message}");
        }
    }

    public void SerializeServices()
    {
        try
        {
            var json = JsonSerializer.Serialize(SelectedServicesToStream, _regularJsonOptions);
            File.WriteAllText(_savedServicesPath, json);
        }
        catch (Exception ex)
        {
            _logger.Log($"Error in Serialize Services:{ex.Message}");
        }
    }
    private void DeSerializeServices()
    {
        if (!File.Exists(_savedServicesPath))
            return;

        try
        {
            var json = File.ReadAllText(_savedServicesPath);
            var deserialized = JsonSerializer.Deserialize<ObservableCollection<SelectedService>>(json);
            if (deserialized != null)
            {
                SelectedServicesToStream.Clear();
                foreach (var service in deserialized)
                {
                    SelectedServicesToStream.Add(service);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"❌ Failed to load RTMP services: {ex.Message}");
        }
    }

    private void DeSerializeYoutubeCategories()
    {
        try
        {
            YoutubeVideoCategories.Clear();
            if (!File.Exists(_youtubeCategories)) return;

            var json = File.ReadAllText(_youtubeCategories);
            var deserialized = JsonSerializer.Deserialize<ObservableCollection<VideoCategory>>(json);
            if (deserialized != null)
                YoutubeVideoCategories = deserialized;
            if (deserialized == null)
            {
                throw new Exception("There was an issue deserializing Youtube Category list");
            }
        }
        catch (Exception ex)
        {
            _logger.Log(ex.Message);
        }
    }

    private void LoadRtmpServersFromServicesJson(string jsonPath)
    {
        try
        {
            if (!File.Exists(jsonPath)) return;

            using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
            var services = doc.RootElement.GetProperty("services");

            foreach (var service in services.EnumerateArray())
            {
                if (service.TryGetProperty("protocol", out var proto) && !proto.GetString()!.Contains("rtmp", StringComparison.CurrentCultureIgnoreCase))
                    continue;

                var serviceName = service.GetProperty("name").GetString() ?? "Unknown";

                var rtmpServers = new List<RtmpServerInfo>();

                foreach (var server in service.GetProperty("servers").EnumerateArray())
                {
                    var url = server.GetProperty("url").GetString() ?? "";
                    if (!url.StartsWith("rtmp")) continue;

                    rtmpServers.Add(new RtmpServerInfo
                    {
                        ServiceName = serviceName,
                        ServerName = server.GetProperty("name").GetString() ?? "Unnamed",
                        Url = url
                    });
                }

                RecommendedSettings? recommended = null;

                if (service.TryGetProperty("recommended", out var rec))
                {
                    recommended = JsonSerializer.Deserialize<RecommendedSettings>(rec.GetRawText());
                }

                // Plocka supported video codecs
                if (service.TryGetProperty("supported video codecs", out var codecs))
                {
                    recommended ??= new RecommendedSettings();
                    recommended.SupportedVideoCodes = [.. codecs.EnumerateArray().Select(c => c.GetString()!)];
                }

                if (rtmpServers.Count > 0)
                {
                    RtmpServiceGroups.Add(new RtmpServiceGroup
                    {
                        ServiceName = serviceName,
                        Servers = rtmpServers,
                        RecommendedSettings = recommended
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"❌ Failed to load RTMP services from services.json: {ex.Message}");
        }
    }
}