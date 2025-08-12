using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace SSMM_UI.Services;

public class StateService
{
    private const string SerializedServices = "Serialized_Services.json";
    private const string _obsServices = "services.json";

    public ObservableCollection<SelectedService> SelectedServicesToStream { get; private set; } = [];
    public ObservableCollection<RtmpServiceGroup> RtmpServiceGroups { get; } = [];

    public StateService()
    {
        DeSerializeServices();
        LoadRtmpServersFromServicesJson(_obsServices);
    }

    public void SerializeServices()
    {
        var json = JsonSerializer.Serialize(SelectedServicesToStream, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SerializedServices, json);
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
            LogService.Log($"❌ Kunde inte läsa in tjänster: {ex.Message}");
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

            if (rtmpServers.Count > 0)
            {
                RtmpServiceGroups.Add(new RtmpServiceGroup
                {
                    ServiceName = serviceName,
                    Servers = rtmpServers
                });
            }
        }
    }
}
