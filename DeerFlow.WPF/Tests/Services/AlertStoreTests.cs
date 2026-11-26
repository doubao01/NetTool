using System.IO;
using DeerFlow.WPF.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeerFlow.WPF.Tests.Services;

public class AlertStoreTests
{
    private static string NewDbPath() =>
        Path.Combine(Path.GetTempPath(), "DeerFlowTests", Guid.NewGuid().ToString("N") + ".db");

    private static Alert CreateAlert(string title, AlertSeverity severity = AlertSeverity.Medium) => new()
    {
        Type = AlertType.PerformanceThreshold,
        Severity = severity,
        Title = title,
        Message = title + " 的详细信息",
        Context = new Dictionary<string, object> { ["k"] = "v" }
    };

    [Fact]
    public void Upsert_Then_Load_ReturnsAlert()
    {
        using var store = new AlertStore(NewDbPath());
        var alert = CreateAlert("测试告警");

        store.Upsert(alert);
        var loaded = store.Load();

        var single = Assert.Single(loaded);
        Assert.Equal(alert.Id, single.Id);
        Assert.Equal(alert.Title, single.Title);
        Assert.Equal(AlertSeverity.Medium, single.Severity);
        Assert.Equal("v", single.Context["k"].ToString());
    }

    [Fact]
    public void Upsert_SameId_UpdatesAcknowledgedState()
    {
        using var store = new AlertStore(NewDbPath());
        var alert = CreateAlert("确认告警");
        store.Upsert(alert);

        alert.IsAcknowledged = true;
        store.Upsert(alert);

        var loaded = store.Load();
        Assert.Single(loaded);
        Assert.True(loaded[0].IsAcknowledged);
    }

    [Fact]
    public void Delete_RemovesAlert()
    {
        using var store = new AlertStore(NewDbPath());
        var alert = CreateAlert("待删除告警");
        store.Upsert(alert);

        store.Delete(alert.Id);

        Assert.Empty(store.Load());
    }

    [Fact]
    public void DeleteAcknowledged_RemovesOnlyAcknowledged()
    {
        using var store = new AlertStore(NewDbPath());
        var ack = CreateAlert("已确认");
        ack.IsAcknowledged = true;
        var pending = CreateAlert("待确认");
        store.Upsert(ack);
        store.Upsert(pending);

        var removed = store.DeleteAcknowledged();

        Assert.Equal(1, removed);
        var loaded = store.Load();
        var single = Assert.Single(loaded);
        Assert.Equal(pending.Id, single.Id);
    }

    [Fact]
    public void PersistedAlerts_AreReloadedByNewInstance()
    {
        var dbPath = NewDbPath();
        var alert = CreateAlert("持久化告警");

        using (var first = new AlertStore(dbPath))
        {
            first.Upsert(alert);
        }

        using var second = new AlertStore(dbPath);
        var loaded = second.Load();
        Assert.Single(loaded);
        Assert.Equal(alert.Id, loaded[0].Id);
    }

    [Fact]
    public void AlertService_WithStore_PersistsRaisedAlert()
    {
        var dbPath = NewDbPath();
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);

        using var store = new AlertStore(dbPath);
        var service = new AlertService(loggerFactory.Object, store);

        var alertId = service.RaiseAlert(
            AlertType.NegativeFeedback, AlertSeverity.High, "负面反馈告警", "负面反馈率过高");

        using var reopened = new AlertStore(dbPath);
        var persisted = reopened.Load();
        var single = Assert.Single(persisted);
        Assert.Equal(alertId, single.Id);
        Assert.Equal(AlertSeverity.High, single.Severity);
    }

    [Fact]
    public void AlertService_AcknowledgeAlert_PersistsState()
    {
        var dbPath = NewDbPath();
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);

        using var store = new AlertStore(dbPath);
        var service = new AlertService(loggerFactory.Object, store);
        var alertId = service.RaiseAlert(
            AlertType.PerformanceThreshold, AlertSeverity.Medium, "性能告警", "响应时间过长");

        service.AcknowledgeAlert(alertId);

        using var reopened = new AlertStore(dbPath);
        var single = Assert.Single(reopened.Load());
        Assert.True(single.IsAcknowledged);
    }

    [Fact]
    public void AlertService_WithoutStore_RemainsInMemoryOnly()
    {
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);

        var service = new AlertService(loggerFactory.Object);
        service.RaiseAlert(AlertType.SystemError, AlertSeverity.Critical, "系统错误", "发生错误");

        Assert.Single(service.GetAllAlerts());
    }
}