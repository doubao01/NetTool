using DeerFlow.WPF.Services;
using DeerFlow.WPF.ViewModels;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeerFlow.WPF.Tests.Services;

public class AlertsViewModelTests
{
    private static ILoggerFactory CreateLoggerFactory()
    {
        var factory = new Mock<ILoggerFactory>();
        factory
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
        return factory.Object;
    }

    [Fact]
    public void Constructor_WithoutService_ShowsDisabledStatus()
    {
        var logger = new Mock<ILoggerService>().Object;

        var vm = new AlertsViewModel(logger);

        Assert.Empty(vm.Alerts);
        Assert.Equal(0, vm.TotalCount);
        Assert.Equal(0, vm.UnacknowledgedCount);
    }

    [Fact]
    public void Constructor_WithService_LoadsExistingAlerts()
    {
        var service = new AlertService(CreateLoggerFactory());
        service.RaiseAlert(AlertType.SystemError, AlertSeverity.High, "已有告警", "m");

        var vm = new AlertsViewModel(new Mock<ILoggerService>().Object, service);

        Assert.Single(vm.Alerts);
        Assert.Equal(1, vm.TotalCount);
        Assert.Equal(1, vm.UnacknowledgedCount);
        Assert.Contains("1 条告警", vm.StatusMessage);
    }

    [Fact]
    public void AcknowledgeCommand_UpdatesCounters()
    {
        var service = new AlertService(CreateLoggerFactory());
        service.RaiseAlert(AlertType.SystemError, AlertSeverity.High, "待确认", "m");
        var vm = new AlertsViewModel(new Mock<ILoggerService>().Object, service)
        {
            SelectedAlert = service.GetAllAlerts().First()
        };

        Assert.True(vm.AcknowledgeCommand.CanExecute(null));
        vm.AcknowledgeCommand.Execute(null);

        Assert.Equal(0, vm.UnacknowledgedCount);
        Assert.Equal(1, vm.TotalCount);
        Assert.True(vm.Alerts[0].IsAcknowledged);
    }

    [Fact]
    public void DeleteCommand_RemovesSelectedAlert()
    {
        var service = new AlertService(CreateLoggerFactory());
        service.RaiseAlert(AlertType.SystemError, AlertSeverity.High, "待删除", "m");
        var vm = new AlertsViewModel(new Mock<ILoggerService>().Object, service)
        {
            SelectedAlert = service.GetAllAlerts().First()
        };

        vm.DeleteCommand.Execute(null);

        Assert.Empty(vm.Alerts);
        Assert.Equal(0, vm.TotalCount);
        Assert.Null(vm.SelectedAlert);
    }

    [Fact]
    public void ClearAcknowledgedCommand_RemovesOnlyAcknowledged()
    {
        var service = new AlertService(CreateLoggerFactory());
        var ackId = service.RaiseAlert(AlertType.SystemError, AlertSeverity.High, "已确认", "m");
        service.RaiseAlert(AlertType.SystemError, AlertSeverity.Low, "待确认", "m");
        service.AcknowledgeAlert(ackId);
        var vm = new AlertsViewModel(new Mock<ILoggerService>().Object, service);

        vm.ClearAcknowledgedCommand.Execute(null);

        Assert.Single(vm.Alerts);
        Assert.Equal("待确认", vm.Alerts[0].Title);
    }

    [Fact]
    public void AcknowledgeCommand_CannotExecute_WhenSelectionMissingOrAcknowledged()
    {
        var service = new AlertService(CreateLoggerFactory());
        var vm = new AlertsViewModel(new Mock<ILoggerService>().Object, service);

        Assert.False(vm.AcknowledgeCommand.CanExecute(null));
        Assert.False(vm.DeleteCommand.CanExecute(null));

        var id = service.RaiseAlert(AlertType.SystemError, AlertSeverity.High, "x", "m");
        service.AcknowledgeAlert(id);
        vm.SelectedAlert = service.GetAllAlerts().First();

        Assert.False(vm.AcknowledgeCommand.CanExecute(null));
        Assert.True(vm.DeleteCommand.CanExecute(null));
    }
}