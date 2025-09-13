using SSMM_UI.Converters;
using SSMM_UI.MetaData;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SSMM_UI.Tests.ConvertersTests;

public class StreamMetaDataConverterUnitTests
{
    [Fact]
    public void Read_ShouldParseBasicProperties()
    {
        // Arrange
        var json = @"{
        ""title"": ""Test Stream"",
        ""thumbnailPath"": ""/path/to/thumb.jpg""
    }";

        // Act
        var metadata = JsonSerializer.Deserialize<StreamMetadata>(json, new JsonSerializerOptions
        {
            Converters = { new StreamMetadataConverter() }
        });

        // Assert
        Assert.Equal("Test Stream", metadata.Title);
        Assert.Equal("/path/to/thumb.jpg", metadata.ThumbnailPath);
    }

    [Theory]
    [InlineData("TITLE", "Test Stream")]
    [InlineData("Title", "Test Stream")]
    [InlineData("title", "Test Stream")]
    public void Read_ShouldBeCaseInsensitive(string propertyName, string expectedValue)
    {
        // Arrange
        var json = $@"{{ ""{propertyName}"": ""{expectedValue}"" }}";

        // Act
        var metadata = JsonSerializer.Deserialize<StreamMetadata>(json, new JsonSerializerOptions
        {
            Converters = { new StreamMetadataConverter() }
        });

        // Assert
        Assert.Equal(expectedValue, metadata.Title);
    }

    [Fact]
    public void Read_ShouldParseTagsArray()
    {
        // Arrange
        var json = @"{
        ""tags"": [""gaming"", ""live"", ""fun""]
    }";

        // Act
        var metadata = JsonSerializer.Deserialize<StreamMetadata>(json, new JsonSerializerOptions
        {
            Converters = { new StreamMetadataConverter() }
        });

        // Assert
        Assert.Equal(new List<string> { "gaming", "live", "fun" }, metadata.Tags);
    }

    // TODO: Fix
    [Fact]
    public void Read_ShouldParseYouTubeCategory()
    {
        // Arrange
        var json = @"{
        ""youTubeCategory"": { 
            ""ETag"": ""0Hh6gbZ9zWjnV3sfdZjKB5LQr6E"",
            ""Id"": ""20"",
            ""Kind"": ""youtube#videoCategory"",
            ""Snippet"": {
                ""Assignable"": true,
                ""ChannelId"": ""UCBR8-60-B28hp2BmDPdntcQ"",
                ""Title"": ""Gaming""
                }
            }
        }";
        var _metaDataJsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        // Act
        var metadata = JsonSerializer.Deserialize<StreamMetadata>(json, _metaDataJsonOptions);

        // Assert
        Assert.NotNull(metadata.YouTubeCategory);
        Assert.Equal("20", metadata.YouTubeCategory.Id);
        Assert.Equal("Gaming", metadata.YouTubeCategory.Snippet.Title);
    }

    [Fact]
    public void Read_ShouldIgnoreUnknownProperties()
    {
        // Arrange
        var json = @"{
        ""title"": ""Test"",
        ""unknownProperty"": ""should be ignored"",
        ""anotherUnknown"": { ""nested"": ""value"" }
    }";

        // Act & Assert (should not throw)
        var exception = Record.Exception(() =>
            JsonSerializer.Deserialize<StreamMetadata>(json, new JsonSerializerOptions
            {
                Converters = { new StreamMetadataConverter() }
            }));

        Assert.Null(exception);
    }

    [Fact]
    public void Write_ShouldSerializeCorrectly()
    {
        // Arrange
        var metadata = new StreamMetadata
        {
            Title = "Test Stream",
            Tags = new List<string> { "gaming", "live" }
        };

        // Act
        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
        {
            Converters = { new StreamMetadataConverter() }
        });

        // Assert
        Assert.Contains("\"title\":\"Test Stream\"", json);
        Assert.Contains("\"tags\":[\"gaming\",\"live\"]", json);
    }

    [Fact]
    public void Write_ShouldExcludeNullValues()
    {
        // Arrange
        var metadata = new StreamMetadata { Title = "Test" }; // andra properties är null

        // Act
        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
        {
            Converters = { new StreamMetadataConverter() }
        });

        // Assert
        Assert.DoesNotContain("\"thumbnailPath\":null", json);
        Assert.DoesNotContain("\"youTubeCategory\":null", json);
    }

}
