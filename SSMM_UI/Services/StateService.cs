using Avalonia.Media.Imaging;
using Google.Apis.YouTube.v3.Data;
using SSMM_UI.MetaData;
using SSMM_UI.RTMP;
using SSMM_UI.Settings;
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
    //TODO: Centralize auth token serialization to here

    private const string SerializedServices = "Serialized_Services.json";
    private const string _obsServices = "services.json";
    private const string YoutubeCategories = "youtube_categories.json";
    private const string _userSettings = "UserSettings.json";
    private const string _savedMetaData = "MetaData_State.json";
    private readonly JsonSerializerOptions _metaDataJsonOptions;
    private readonly JsonSerializerOptions _regularJsonOptions = new() { WriteIndented = true };


    public ObservableCollection<SelectedService> SelectedServicesToStream { get; private set; } = [];
    public ObservableCollection<RtmpServiceGroup> RtmpServiceGroups { get; } = [];
    public ObservableCollection<VideoCategory> YoutubeVideoCategories { get; private set; } = [];

    public StreamMetadata CurrentMetaData { get; private set; } = new StreamMetadata();

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
            Console.WriteLine($"Failed to serialize metadata: {ex.Message}");
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
            _logger.Log($"❌ Kunde inte läsa in tjänster: {ex.Message}");
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