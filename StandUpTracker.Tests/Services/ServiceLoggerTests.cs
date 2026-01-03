using StandUpTracker.Services;

namespace StandUpTracker.Tests.Services;

/// <summary>
/// Tests for ServiceLogger - Infrastructure logging
/// </summary>
public class ServiceLoggerTests : IDisposable
{
    private readonly string _testLogDir;

    public ServiceLoggerTests()
    {
        _testLogDir = Path.Combine(Path.GetTempPath(), $"StandUpTracker_Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testLogDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testLogDir))
        {
            try
            {
                Directory.Delete(_testLogDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public void Log_ExecutesWithoutError()
    {
        // Arrange
        var sut = new ServiceLogger();

        // Act & Assert
        sut.Log("TEST", "Category", "Test message");
        Assert.True(true);
    }

    [Fact]
    public void Info_ExecutesWithoutError()
    {
        // Arrange
        var sut = new ServiceLogger();

        // Act & Assert
        sut.Info("Category", "Info message");
        Assert.True(true);
    }

    [Fact]
    public void Warning_ExecutesWithoutError()
    {
        // Arrange
        var sut = new ServiceLogger();

        // Act & Assert
        sut.Warning("Category", "Warning message");
        Assert.True(true);
    }

    [Fact]
    public void Error_ExecutesWithoutError()
    {
        // Arrange
        var sut = new ServiceLogger();

        // Act & Assert
        sut.Error("Category", "Error message");
        Assert.True(true);
    }

    [Fact]
    public void Debug_ExecutesWithoutError()
    {
        // Arrange
        var sut = new ServiceLogger();

        // Act & Assert
        sut.Debug("Category", "Debug message");
        Assert.True(true);
    }
}
