using DeerFlow.WPF.Services;
using DeerFlow.WPF.ViewModels;
using Moq;
using Xunit;

namespace DeerFlow.WPF.Tests.Core;

/// <summary>
/// 设置页 ViewModel 面板切换测试
/// </summary>
public class SettingsViewModelTests
{
    private static SettingsViewModel CreateViewModel()
    {
        var logger = new Mock<ILoggerService>().Object;
        var secretStore = new Mock<ISecretStore>().Object;
        var options = new AppOptions();
        return new SettingsViewModel(logger, secretStore, options);
    }

    [Fact]
    public void Constructor_PanelsAreHidden()
    {
        var vm = CreateViewModel();

        Assert.False(vm.ShowExperimentCenter);
        Assert.False(vm.ShowDocumentGeneration);
    }

    [Fact]
    public void ToggleExperimentCenterCommand_ShowsPanel()
    {
        var vm = CreateViewModel();

        vm.ToggleExperimentCenterCommand.Execute(null);

        Assert.True(vm.ShowExperimentCenter);
        Assert.False(vm.ShowDocumentGeneration);
    }

    [Fact]
    public void ToggleExperimentCenterCommand_TwiceReturnsHidden()
    {
        var vm = CreateViewModel();

        vm.ToggleExperimentCenterCommand.Execute(null);
        vm.ToggleExperimentCenterCommand.Execute(null);

        Assert.False(vm.ShowExperimentCenter);
    }

    [Fact]
    public void ToggleDocumentGenerationCommand_ShowsPanel()
    {
        var vm = CreateViewModel();

        vm.ToggleDocumentGenerationCommand.Execute(null);

        Assert.True(vm.ShowDocumentGeneration);
        Assert.False(vm.ShowExperimentCenter);
    }

    [Fact]
    public void OpeningDocumentGeneration_ClosesExperimentCenter()
    {
        var vm = CreateViewModel();
        vm.ShowExperimentCenter = true;

        vm.ToggleDocumentGenerationCommand.Execute(null);

        Assert.True(vm.ShowDocumentGeneration);
        Assert.False(vm.ShowExperimentCenter);
    }

    [Fact]
    public void OpeningExperimentCenter_ClosesDocumentGeneration()
    {
        var vm = CreateViewModel();
        vm.ShowDocumentGeneration = true;

        vm.ToggleExperimentCenterCommand.Execute(null);

        Assert.True(vm.ShowExperimentCenter);
        Assert.False(vm.ShowDocumentGeneration);
    }
}