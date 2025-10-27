using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting.Unicode;
using Google.Apis.YouTube.v3.Data;
using SSMM_UI.Enums;
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
    private const string SerializedServices = "Serialized_Services.json";
    private const string _obsServices = "services.json";
    private const string YoutubeCategories = "youtube_categories.json";
    private const string _userSettings = "UserSettings.json";
    private const string _savedMetaData = "MetaData_State.json";
    private const string _windowSettings = "WindowSettings.json";
    private const string _tokenPath = "Tokens";
    private const string _Webhooks = "Webhooks.json";
    private readonly JsonSerializerOptions _metaDataJsonOptions;
    private readonly JsonSerializerOptions _regularJsonOptions = new() { WriteIndented = true };
    private Dictionary<OAuthServices, IAuthToken> _authObjects = [];
    public event Action? OnAuthObjectsUpdated;
    public Dictionary<OAuthServices, IAuthToken> AuthObjects { get { return _authObjects; } }
    public ObservableCollection<SelectedService> SelectedServicesToStream { get; private set; } = [];
    public ObservableCollection<RtmpServiceGroup> RtmpServiceGroups { get; } = [];
    public ObservableCollection<VideoCategory> YoutubeVideoCategories { get; private set; } = [];
    public ObservableCollection<KeyValueItem> Webhooks { get; private set; } = [];

    private StreamMetadata CurrentMetaData { get; set; } = new StreamMetadata();


    public void SaveWebHook(KeyValueItem webhook)
    {
        Webhooks.Add(webhook);
        SerializeWebhooks();
    }

    public void SerializeWebhooks()
    {
        var json = JsonSerializer.Serialize(Webhooks, _regularJsonOptions);
        File.WriteAllText(_Webhooks, json);
    }

    public void DeSerializeWebhooks()
    {
        if (File.Exists(_Webhooks))
        {
            var json = File.ReadAllText(_Webhooks);

            var deserialized = JsonSerializer.Deserialize<ObservableCollection<KeyValueItem>>(json);

            if (deserialized is not null)
            {
                Webhooks.Clear();
                foreach (var item in deserialized)
                    Webhooks.Add(item);
            }
        }
    }

    public void SaveWindowPosition(double Height, double Width, PixelPoint Position, WindowState windowState)
    {
        var windowstate = new WindowSettings
        {
            Width = Width,
            Height = Height,
            Pos = Position,
            WindowState = windowState
        };
        var json = JsonSerializer.Serialize(windowstate, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_windowSettings, json);
    }

    public WindowSettings? LoadWindowPosition()
    {
        if (File.Exists(_windowSettings))
        {
            var json = File.ReadAllText(_windowSettings);
            var res = JsonSerializer.Deserialize<WindowSettings>(json);
            if(res != null)
            {
                return res;
            }
            else
            {
                return null;
            }
        }
        else
        {
            return null;
        }
        
    }

    public void SerializeToken<T>(OAuthServices service, T token) where T : class, IAuthToken
    {
        try
        {
            ArgumentNullException.ThrowIfNull(token);

            var json = JsonSerializer.Serialize(token, _regularJsonOptions);

            var path = Path.Combine(_tokenPath, $"{service}Token.json");
            if (!Path.Exists(_tokenPath))
            {
                Directory.CreateDirectory(path);
            }
            File.WriteAllText(path, json);

            // CA1853 says you dont need to guard against if it contains it, its built in
            _authObjects.Remove(service);
            _authObjects.Add(service, token);
            OnAuthObjectsUpdated?.Invoke();

        }
        catch (Exception ex)
        {
            _logger.Log($"{ex.Message} Failed to Serialize Token for:{service}");
        }
    }

    public T DeserializeToken<T>(OAuthServices service) where T : class, IAuthToken
    {
        try
        {
            string filePath = Path.Combine(_tokenPath, $"{service}Token.json");
            if (!File.Exists(filePath))
            {
                return null!;
            }
            var json = File.ReadAllText(filePath);
            var deserializedobj = JsonSerializer.Deserialize<T>(json, _regularJsonOptions);

            //CA1853 claims you dont need to guard against removing something as it ignores if its not contained within
            _authObjects.Remove(service);
            if (deserializedobj != null)
            {
                _authObjects.Add(service, deserializedobj);
                OnAuthObjectsUpdated?.Invoke();
            }
            return deserializedobj!;
        }
        catch (Exception ex)
        {
            _logger.Log($"{ex.Message} Failed to Deserialize token for:{service}");
            return null!;
        }
    }

    public StreamMetadata GetCurrentMetaData()
    {
        return CurrentMetaData;
    }

    public void UpdateCurrentMetaData(StreamMetadata metadata)
    {
        CurrentMetaData = metadata;
        SerializeMetaData();
    }

    public UserSettings UserSettingsObj { get; private set; } = new UserSettings();

    private readonly ILogService _logger;

    //TODO: When running initially, check what boxart we have available, build a list, supply list to VM's
    public StateService(ILogService logger)
    {
        _metaDataJsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        _logger = logger;
        DeSerializeServices();
        DeserializeSettings();
        DeSerializeWebhooks();
        DeSerializeMetaData();
        LoadRtmpServersFromServicesJson(_obsServices);
        DeSerializeYoutubeCategories();
    }

    public void SettingsChanged(UserSettings settings)
    {
        UserSettingsObj = settings;
    }

    public void SerializeServices()
    {
        var json = JsonSerializer.Serialize(SelectedServicesToStream, _regularJsonOptions);
        File.WriteAllText(SerializedServices, json);
    }

    public void SerializeSettings()
    {
        var json = JsonSerializer.Serialize(UserSettingsObj, _regularJsonOptions);
        File.WriteAllText(_userSettings, json);
    }

    private void DeserializeSettings()
    {
        if (File.Exists(_userSettings))
        {
            var json = File.ReadAllText(_userSettings);
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
            string json = JsonSerializer.Serialize(CurrentMetaData, _metaDataJsonOptions);
            File.WriteAllText(_savedMetaData, json);
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to serialize metadata: {ex.Message}");
        }
    }

    private void DeSerializeMetaData()
    {
        if (File.Exists(_savedMetaData))
        {
            var json = File.ReadAllText(_savedMetaData);
            var deserialized = JsonSerializer.Deserialize<StreamMetadata>(json, _metaDataJsonOptions);
            if (deserialized != null)
            {
                CurrentMetaData = deserialized;
                if (CurrentMetaData != null)
                {
                    if (CurrentMetaData.ThumbnailPath != null)
                    {
                        CurrentMetaData.Thumbnail = new Bitmap(CurrentMetaData.ThumbnailPath);
                    }
                }
            }
        }
    }

    private void DeSerializeServices()
    {
        if (!File.Exists(SerializedServices))
            return;

        try
        {
            var json = File.ReadAllText(SerializedServices);
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
            if (File.Exists(YoutubeCategories))
            {
                var json = File.ReadAllText(YoutubeCategories);
                var deserialized = JsonSerializer.Deserialize<ObservableCollection<VideoCategory>>(json);
                if (deserialized != null)
                    YoutubeVideoCategories = deserialized;
                if (deserialized == null)
                {
                    throw new Exception("There was an issue deserializing Youtube Category list");
                }
            }
            else
            {
                throw new Exception("File for Youtube Categories was missing");
            }
        }
        catch (Exception ex)
        {
            _logger.Log(ex.Message);
        }
    }

    private void LoadRtmpServersFromServicesJson(string jsonPath)
    {
        if (!File.Exists(jsonPath))
            return;

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
}