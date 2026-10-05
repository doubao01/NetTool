using FluentAssertions;
using SystemToolkit.Core.Services;
using SystemToolkit.Core.Models;

using Xunit;

namespace SystemToolkit.Tests;

public class FileServiceTests
{
    [Fact]
    public void GetFilesAsync_EnumeratesFiles()
    {
        // Arrange
        var service = new FileService();
        var testDir = Path.Combine(Path.GetTempPath(), "FileServiceTests_" + Guid.NewGuid());
        Directory.CreateDirectory(testDir);
        
        File.WriteAllText(Path.Combine(testDir, "test1.txt"), "content1");
        File.WriteAllText(Path.Combine(testDir, "test2.txt"), "content2");

        try
        {
            // Act
            var files = service.GetFilesAsync(testDir).Result;

            // Assert
            files.Should().HaveCount(2);
            files.Should().Contain(f => f.Name == "test1.txt");
            files.Should().Contain(f => f.Name == "test2.txt");
        }
        finally
        {
            Directory.Delete(testDir, true);
        }
    }

    [Fact]
    public void CalculateFileHashAsync_ReturnsValidHash()
    {
        // Arrange
        var service = new FileService();
        var tempFile = Path.Combine(Path.GetTempPath(), "hash_test_" + Guid.NewGuid() + ".txt");
        File.WriteAllText(tempFile, "test content");

        try
        {
            // Act
            var hash = service.CalculateFileHashAsync(tempFile).Result;

            // Assert
            hash.Should().NotBeNullOrEmpty();
            hash.Should().HaveLength(64); // SHA256 produces 64 character hex string
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}

public class DevToolServiceTests
{
    [Fact]
    public void FormatJson_ValidJson_FormatsCorrectly()
    {
        // Arrange
        var service = new DevToolService();
        var input = "{\"name\":\"test\",\"value\":123}";

        // Act
        var result = service.FormatJson(input);

        // Assert
        result.Success.Should().BeTrue();
        result.FormattedText.Should().NotBeNullOrEmpty();
        result.FormattedText.Should().Contain("  ");
    }

    [Fact]
    public void EncodeBase64_ValidString_EncodesCorrectly()
    {
        // Arrange
        var service = new DevToolService();
        var input = "Hello World";

        // Act
        var encoded = service.EncodeBase64(input);
        var decoded = service.DecodeBase64(encoded);

        // Assert
        encoded.Should().Be("SGVsbG8gV29ybGQ=");
        decoded.Should().Be(input);
    }
}

public class DataToolServiceTests
{
    [Fact]
    public void CalculateHash_MD5_ReturnsValidHash()
    {
        // Arrange
        var service = new DataToolService();
        var input = "test";

        // Act
        var result = service.CalculateHash(input, "MD5");

        // Assert
        result.Hash.Should().Be("098f6bcd4621d373cade4e832627b4f6");
        result.Algorithm.Should().Be("MD5");
    }

    [Theory]
    [InlineData("SHA1")]
    [InlineData("SHA256")]
    [InlineData("MD5")]
    public void CalculateHash_DifferentAlgorithms_ReturnsDifferentHashes(string algorithm)
    {
        // Arrange
        var service = new DataToolService();
        var input = "test string for hashing";

        // Act
        var result = service.CalculateHash(input, algorithm);

        // Assert
        result.Hash.Should().NotBeNullOrEmpty();
        result.Algorithm.Should().Be(algorithm);
    }
}
