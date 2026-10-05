using DeerFlow.WPF.Services;
using Xunit;

namespace DeerFlow.WPF.Tests.Services;

public class AppOptionsProviderTests
{
    [Fact]
    public void DefaultOptions_HaveAvailableModels()
    {
        var options = new AppOptions();

        Assert.NotEmpty(options.AvailableModels);
        Assert.Contains("deepseek-v3", options.AvailableModels);
    }

    [Fact]
    public void DefaultOptions_HaveReasonableDefaults()
    {
        var options = new AppOptions();

        Assert.False(string.IsNullOrWhiteSpace(options.DefaultApiBaseUrl));
        Assert.False(string.IsNullOrWhiteSpace(options.DefaultModel));
        Assert.True(options.ChatTimeoutMinutes > 0);
        Assert.True(options.WebSearchTimeoutSeconds > 0);
    }

    [Fact]
    public void Provider_LoadsWithoutThrowing_WhenConfigMissing()
    {
        var provider = new AppOptionsProvider();

        Assert.NotNull(provider.Options);
        Assert.NotEmpty(provider.Options.AvailableModels);
    }
}
