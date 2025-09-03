using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia.Media.Imaging;
using System.Text.Json.Serialization.Metadata;
using Google.Apis.YouTube.v3.Data;
using SSMM_UI.MetaData;
using SSMM_UI.RTMP;
using SSMM_UI.Settings;
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
    private readonly JsonSerializerOptions _jsonOptions;

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

    private ILogService _logger;
    public StateService(ILogService logger)
    {
        _jsonOptions = new JsonSerializerOptions
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
        var json = JsonSerializer.Serialize(SelectedServicesToStream, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SerializedServices, json);
    }

    public void SerializeSettings()
    {
        var json = JsonSerializer.Serialize(UserSettingsObj, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_userSettings, json);
    }

    private void DeserializeSettings()
    {
        if(File.Exists(_userSettings))
        {
            var json = File.ReadAllText(_userSettings);
            var deserialized = JsonSerializer.Deserialize<UserSettings>(json);
            if(deserialized != null)
            {
                UserSettingsObj = deserialized;
            }
        }
    }

    private void SerializeMetaData()
    {
        try
        {
            string json = JsonSerializer.Serialize(CurrentMetaData, _jsonOptions);
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
            var deserialized = JsonSerializer.Deserialize<StreamMetadata>(json, _jsonOptions);
            if (deserialized != null)
            {
                CurrentMetaData = deserialized;
                if(CurrentMetaData != null)
                {
                    if(CurrentMetaData.ThumbnailPath != null)
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
                recommended.SupportedVideoCodes = codecs.EnumerateArray()
                                                        .Select(c => c.GetString()!)
                                                        .ToArray();
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

public class StreamMetadataConverter : JsonConverter<StreamMetadata>
{
    public override StreamMetadata Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var metadata = new StreamMetadata();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName?.ToLower())
                {
                    case "title":
                        metadata.Title = reader.GetString();
                        break;
                    case "thumbnailpath":
                        metadata.ThumbnailPath = reader.GetString();
                        break;
                    case "youtubecategory":
                        metadata.YouTubeCategory = JsonSerializer.Deserialize<VideoCategory>(ref reader, options);
                        break;
                    case "twitchcategory":
                        metadata.TwitchCategory = JsonSerializer.Deserialize<TwitchCategory>(ref reader, options);
                        break;
                    case "tags":
                        metadata.Tags = JsonSerializer.Deserialize<List<string>>(ref reader, options);
                        break;
                    default:
                        reader.Skip(); // Ignorera okända properties (t.ex. thumbnail)
                        break;
                }
            }
        }

        return metadata;
    }

    public override void Write(Utf8JsonWriter writer, StreamMetadata value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        if (value.Title != null)
            writer.WriteString("title", value.Title);

        if (value.ThumbnailPath != null)
            writer.WriteString("thumbnailPath", value.ThumbnailPath);

        if (value.YouTubeCategory != null)
        {
            writer.WritePropertyName("youTubeCategory");
            JsonSerializer.Serialize(writer, value.YouTubeCategory, options);
        }

        if (value.TwitchCategory != null)
        {
            writer.WritePropertyName("twitchCategory");
            JsonSerializer.Serialize(writer, value.TwitchCategory, options);
        }

        if (value.Tags != null)
        {
            writer.WritePropertyName("tags");
            JsonSerializer.Serialize(writer, value.Tags, options);
        }

        writer.WriteEndObject();
    }
}
