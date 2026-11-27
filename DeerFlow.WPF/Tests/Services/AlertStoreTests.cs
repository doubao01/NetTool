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

    [Fact]
    public void Constructor_CreatesMissingParentDirectories()
    {
        var newRoot = Path.Combine(
            Path.GetTempPath(), "DeerFlowTests", "nested-" + Guid.NewGuid().ToString("N"), "deeper");
        var dbPath = Path.Combine(newRoot, "alerts.db");
        Assert.False(Directory.Exists(newRoot));

        using var store = new AlertStore(dbPath);
        var alert = CreateAlert("多级目录告警");
        store.Upsert(alert);

        Assert.True(Directory.Exists(newRoot));
        Assert.Single(store.Load());
    }

    [Fact]
    public void Upsert_SameId_RefreshesAllFields()
    {
        using var store = new AlertStore(NewDbPath());
        var alert = CreateAlert("旧标题");
        alert.Id = "fixed-id";
        store.Upsert(alert);

        alert.Title = "新标题";
        alert.Message = "新消息";
        alert.Severity = AlertSeverity.Critical;
        store.Upsert(alert);

        var single = Assert.Single(store.Load());
        Assert.Equal("新标题", single.Title);
        Assert.Equal("新消息", single.Message);
        Assert.Equal(AlertSeverity.Critical, single.Severity);
    }

    [Fact]
    public void Load_RestoresContextAsPrimitiveTypes()
    {
        using var store = new AlertStore(NewDbPath());
        store.Upsert(new Alert
        {
            Title = "类型往返",
            Message = "m",
            Context = new Dictionary<string, object>
            {
                ["count"] = 123,
                ["enabled"] = true,
                ["ratio"] = 1.5,
                ["name"] = "abc"
            }
        });

        var context = store.Load()[0].Context;
        Assert.IsType<long>(context["count"]);
        Assert.Equal(123L, context["count"]);
        Assert.IsType<bool>(context["enabled"]);
        Assert.True((bool)context["enabled"]);
        Assert.IsType<double>(context["ratio"]);
        Assert.Equal("abc", context["name"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Load_NonPositiveLimit_FallsBackToDefaultInsteadOfWholeTable(int limit)
    {
        using var store = new AlertStore(NewDbPath());
        for (var i = 0; i < 5; i++)
        {
            store.Upsert(CreateAlert($"告警 {i}"));
        }

        var loaded = store.Load(limit);

        Assert.True(loaded.Count <= AlertStore.DefaultLoadLimit);
        Assert.Equal(5, loaded.Count);
    }

    [Fact]
    public void Dispose_DoesNotAffectOtherStoreInstances()
    {
        var first = new AlertStore(NewDbPath());
        using var second = new AlertStore(NewDbPath());
        second.Upsert(CreateAlert("保留告警"));

        first.Dispose();

        second.Upsert(CreateAlert("释放后写入"));
        Assert.Equal(2, second.Load().Count);
    }

    [Fact]
    public void RaiseAlert_SuppressesDuplicatesWithinWindow()
    {
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
        var service = new AlertService(loggerFactory.Object);

        service.CheckNegativeFeedbackRate(0.5, 10);
        service.CheckNegativeFeedbackRate(0.5, 10);
        service.CheckNegativeFeedbackRate(0.5, 10);

        Assert.Single(service.GetAllAlerts());
    }

    [Fact]
    public void RaiseAlert_ConcurrentDuplicates_CreateSingleAlert()
    {
        var service = new AlertService(CreateLoggerFactory());

        Parallel.For(0, 8, _ => service.CheckNegativeFeedbackRate(0.9, 100));

        Assert.Single(service.GetAllAlerts());
    }

    [Fact]
    public void RaiseAlert_RepeatWithHigherSeverity_UpgradesExisting()
    {
        var service = new AlertService(CreateLoggerFactory());
        service.RaiseAlert(AlertType.SystemError, AlertSeverity.Low, "同一事件", "同一正文");

        service.RaiseAlert(AlertType.SystemError, AlertSeverity.Critical, "同一事件", "同一正文");

        var single = Assert.Single(service.GetAllAlerts());
        Assert.Equal(AlertSeverity.Critical, single.Severity);
    }

    [Fact]
    public void RaiseAlert_SameTitleDifferentMessage_NotSuppressed()
    {
        var service = new AlertService(CreateLoggerFactory());

        service.CheckResponseTime(6000, "task-A");
        service.CheckResponseTime(6000, "task-B");

        Assert.Equal(2, service.GetAllAlerts().Count);
    }

    [Fact]
    public void RaiseAlert_DifferentTitles_AreNotSuppressed()
    {
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
        var service = new AlertService(loggerFactory.Object);

        service.RaiseAlert(AlertType.SystemError, AlertSeverity.High, "告警 A", "m");
        service.RaiseAlert(AlertType.SystemError, AlertSeverity.High, "告警 B", "m");

        Assert.Equal(2, service.GetAllAlerts().Count);
    }

    [Fact]
    public void AlertService_RestartLoadsMoreThanDefaultPageSize()
    {
        var dbPath = NewDbPath();
        const int total = 210;

        using (var store = new AlertStore(dbPath))
        {
            var service = new AlertService(CreateLoggerFactory(), store);
            for (var i = 0; i < total; i++)
            {
                service.RaiseAlert(AlertType.SystemError, AlertSeverity.High, $"告警 {i}", "m");
            }
        }

        using var reopened = new AlertStore(dbPath);
        var restarted = new AlertService(CreateLoggerFactory(), reopened);

        Assert.Equal(total, restarted.GetAllAlerts(limit: 1000).Count);
        Assert.Equal(total, restarted.GetUnacknowledgedAlerts().Count);
    }

    [Fact]
    public void ClearAcknowledgedAlerts_RemovesEveryRowBeyondDefaultPageSize()
    {
        var dbPath = NewDbPath();
        using (var store = new AlertStore(dbPath))
        {
            for (var i = 0; i < 250; i++)
            {
                var alert = CreateAlert($"告警 {i}");
                alert.IsAcknowledged = true;
                store.Upsert(alert);
            }
        }

        using (var store = new AlertStore(dbPath))
        {
            var service = new AlertService(CreateLoggerFactory(), store);
            service.ClearAcknowledgedAlerts();
        }

        using var reopened = new AlertStore(dbPath);
        Assert.Empty(reopened.Load(limit: 1000));
    }

    [Fact]
    public void AlertService_AcknowledgedAlertsCannotBeDuplicate_AllowsRetrigger()
    {
        var service = new AlertService(CreateLoggerFactory());
        service.CheckResponseTime(6000, "task-x");
        service.AcknowledgeAlert(service.GetAllAlerts().First().Id);

        service.CheckResponseTime(6000, "task-x");

        Assert.Equal(2, service.GetAllAlerts().Count);
    }

    [Fact]
    public void ClearAcknowledgedAlerts_KeepsDatabaseConsistentWithMemory()
    {
        var dbPath = NewDbPath();
        using (var store = new AlertStore(dbPath))
        {
            var service = new AlertService(CreateLoggerFactory(), store);
            for (var i = 0; i < 40; i++)
            {
                var id = service.RaiseAlert(
                    AlertType.SystemError,
                    AlertSeverity.Low,
                    $"事件 {i}",
                    $"消息 {i}");
                if (i % 2 == 0)
                {
                    service.AcknowledgeAlert(id);
                }
            }

            service.ClearAcknowledgedAlerts();

            Assert.Equal(20, service.GetAllAlerts(limit: 1000).Count);
        }

        using var reopened = new AlertStore(dbPath);
        var remaining = reopened.Load(limit: 1000);
        Assert.Equal(20, remaining.Count);
        Assert.All(remaining, a => Assert.False(a.IsAcknowledged));
    }

    private static ILoggerFactory CreateLoggerFactory()
    {
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
        return loggerFactory.Object;
    }
}