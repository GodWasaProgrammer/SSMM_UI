using Google.Apis.YouTube.v3.Data;
using SSMM_UI.MetaData;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SSMM_UI.Converters;

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
