using System.Net.Http;
using DeerFlow.WPF.Services;
using Moq;
using Xunit;

namespace DeerFlow.WPF.Tests.Services;

public class ApiServiceTests
{
    [Fact]
    public async Task GetAvailableModels_ReturnsExpectedList()
    {
        var mockHttp = new Mock<HttpMessageHandler>();
        var client = new HttpClient(mockHttp.Object);
        var service = new ApiService(client, new AppOptionsProvider());

        var models = await service.GetAvailableModelsAsync();

        Assert.Contains("deepseek-v3", models);
        Assert.Contains("gpt-4o", models);
        Assert.Equal(6, models.Count);
    }

    [Fact]
    public async Task HealthCheck_ReturnsFalse_WhenException()
    {
        var client = new HttpClient { BaseAddress = new Uri("http://localhost:1") };
        client.Timeout = TimeSpan.FromMilliseconds(100);
        var service = new ApiService(client, new AppOptionsProvider());

        var result = await service.HealthCheckAsync();
        Assert.False(result);
    }
}
