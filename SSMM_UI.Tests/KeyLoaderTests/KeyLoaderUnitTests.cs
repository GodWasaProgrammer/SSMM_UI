using SSMM_UI.API_Key_Secrets_Loader;

namespace SSMM_UI.Tests.KeyLoaderTests;

public class KeyLoaderUnitTests
{
    [Fact]
    public void LoadApiKeys_ShouldParseKeyValuePairsCorrectly()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        File.WriteAllLines(tempFile, new[] { "key1=value1", "key2=value2" });

        // Act
        var result = KeyLoader.LoadApiKeys(tempFile);

        // Assert
        Assert.Equal("value1", result["key1"]);
        Assert.Equal("value2", result["key2"]);
    }

    [Fact]
    public void LoadApiKeys_ShouldThrowFileNotFoundException()
    {
        // Act & Assert
        Assert.Throws<FileNotFoundException>(() =>
            KeyLoader.LoadApiKeys("nonexistent.txt"));
    }

    [Fact]
    public void LoadApiKeys_ShouldFindFilesInCorrectDirectory()
    {
        // Arrange - Skapa en testfil i rätt struktur
        var testDir = Path.Combine(Path.GetTempPath(), "API_Key_Secrets_Loader");
        Directory.CreateDirectory(testDir);
        var testFile = Path.Combine(testDir, "test.txt");
        File.WriteAllText(testFile, "test=value");

        // Act & Assert - Simulera path resolution
        // (Detta kräver lite refactoring för testbarhet)
    }

    [Theory]
    [InlineData("")] // tom rad
    [InlineData("#comment")] // kommentar
    [InlineData("key=")] // tomt värde
    [InlineData("=value")] // tom nyckel
    public void LoadApiKeys_ShouldHandleInvalidLines(string line)
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, line);

        // Act
        var result = KeyLoader.LoadApiKeys(tempFile);

        // Assert
        Assert.Empty(result);
    }
}
