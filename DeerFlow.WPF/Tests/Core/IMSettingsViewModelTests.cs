using DeerFlow.WPF.Models;
using DeerFlow.WPF.Services;
using DeerFlow.WPF.ViewModels;
using Moq;
using Xunit;

namespace DeerFlow.WPF.Tests.Core;

/// <summary>
/// IM 设置页 ViewModel 编辑状态测试
/// </summary>
public class IMSettingsViewModelTests
{
    private static (IMSettingsViewModel Vm, Mock<IIMPlatformService> Service) CreateViewModel()
    {
        var service = new Mock<IIMPlatformService>();
        service.Setup(s => s.GetAllConnections()).Returns(new List<IMConnection>());
        var logger = new Mock<ILoggerService>().Object;
        return (new IMSettingsViewModel(service.Object, logger), service);
    }

    [Fact]
    public void SelectingConnection_LoadsEnabledStateIntoForm()
    {
        var (vm, _) = CreateViewModel();
        var connection = new IMConnection
        {
            DisplayName = "已禁用飞书",
            IsEnabled = false
        };

        vm.SelectedConnection = connection;

        Assert.False(vm.EditingIsEnabled);
    }

    [Fact]
    public void SelectingEnabledConnection_LoadsEnabledStateIntoForm()
    {
        var (vm, _) = CreateViewModel();
        var connection = new IMConnection
        {
            DisplayName = "已启用飞书",
            IsEnabled = true
        };

        vm.SelectedConnection = connection;

        Assert.True(vm.EditingIsEnabled);
    }

    [Fact]
    public void NewConnection_ResetsEnabledState()
    {
        var (vm, _) = CreateViewModel();
        vm.EditingIsEnabled = true;

        vm.NewConnectionCommand.Execute(null);

        Assert.False(vm.EditingIsEnabled);
        Assert.True(vm.IsEditing);
    }

    [Fact]
    public void EditingIsEnabled_RaisesPropertyChanged()
    {
        var (vm, _) = CreateViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.EditingIsEnabled = true;

        Assert.Contains(nameof(IMSettingsViewModel.EditingIsEnabled), changed);
    }
}