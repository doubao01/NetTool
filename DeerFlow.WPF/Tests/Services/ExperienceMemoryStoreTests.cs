using System.IO;
using DeerFlow.WPF.Models;
using DeerFlow.WPF.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeerFlow.WPF.Tests.Services;

public class ExperienceMemoryStoreTests
{
    private static ExperienceMemoryStore CreateStore()
    {
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
        var dbPath = Path.Combine(Path.GetTempPath(), "DeerFlowTests", Guid.NewGuid().ToString("N") + ".db");
        return new ExperienceMemoryStore(loggerFactory.Object, dbPath);
    }

    private static ExperienceMemoryItem CreateItem(string content) => new()
    {
        Content = content,
        ExperienceType = "Success",
        Summary = content,
        DetailedContent = content,
        Tags = new List<string> { "test" },
        Importance = 5
    };

    [Fact]
    public async Task SaveExperienceAsync_Then_GetExperience_ReturnsItem()
    {
        using var store = CreateStore();
        var item = CreateItem("使用依赖注入解耦服务-" + Guid.NewGuid());

        var id = await store.SaveExperienceAsync(item);
        var loaded = store.GetExperience(id);

        Assert.NotNull(loaded);
        Assert.Equal(item.Content, loaded!.Content);
    }

    [Fact]
    public async Task UpdateConfidence_Positive_IncrementsValidationCount()
    {
        using var store = CreateStore();
        var item = CreateItem("正向验证-" + Guid.NewGuid());
        var id = await store.SaveExperienceAsync(item);

        store.UpdateConfidence(id, positive: true);

        var loaded = store.GetExperience(id);
        Assert.NotNull(loaded);
        Assert.Equal(1, loaded!.ValidationCount);
        Assert.True(loaded.Confidence > 0);
    }

    [Fact]
    public async Task UpdateConfidence_Negative_DecreasesConfidence()
    {
        using var store = CreateStore();
        var item = CreateItem("负向验证-" + Guid.NewGuid());
        item.Confidence = 1.0;
        var id = await store.SaveExperienceAsync(item);

        store.UpdateConfidence(id, positive: false);

        var loaded = store.GetExperience(id);
        Assert.NotNull(loaded);
        Assert.True(loaded!.Confidence < 1.0);
    }

    [Fact]
    public async Task RetrieveExperiences_RespectsLimit()
    {
        using var store = CreateStore();
        var marker = "分页标记-" + Guid.NewGuid();
        for (var i = 0; i < 5; i++)
        {
            await store.SaveExperienceAsync(CreateItem($"{marker}-{i}"));
        }

        var results = store.RetrieveExperiences(marker, limit: 2).ToList();

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task RetrieveExperiences_SupportsOffset()
    {
        using var store = CreateStore();
        var marker = "偏移标记-" + Guid.NewGuid();
        for (var i = 0; i < 5; i++)
        {
            await store.SaveExperienceAsync(CreateItem($"{marker}-{i}"));
        }

        var page1 = store.RetrieveExperiences(marker, limit: 2, offset: 0).ToList();
        var page2 = store.RetrieveExperiences(marker, limit: 2, offset: 2).ToList();

        Assert.Equal(2, page1.Count);
        Assert.Equal(2, page2.Count);
        Assert.Empty(page1.Select(p => p.Id).Intersect(page2.Select(p => p.Id)));
    }

    [Fact]
    public async Task GetStatistics_ReturnsCounts()
    {
        using var store = CreateStore();
        await store.SaveExperienceAsync(CreateItem("统计-" + Guid.NewGuid()));

        var stats = store.GetStatistics();

        Assert.True((int)stats["TotalExperiences"] >= 1);
    }

    [Fact]
    public async Task ConcurrentReadsAndWrites_DoNotThrowOrLoseData()
    {
        using var store = CreateStore();
        var marker = "并发标记-" + Guid.NewGuid();
        const int writers = 8;
        const int perWriter = 10;

        var writeTasks = Enumerable.Range(0, writers).Select(async w =>
        {
            for (var i = 0; i < perWriter; i++)
            {
                await store.SaveExperienceAsync(CreateItem($"{marker}-{w}-{i}"));
            }
        });

        var readTasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 20; i++)
            {
                var results = store.RetrieveExperiences(marker, limit: 5).ToList();
                Assert.All(results, r => Assert.Contains(marker, r.Content));
            }
        }));

        await Task.WhenAll(writeTasks.Concat(readTasks));

        var all = store.RetrieveExperiences(marker, limit: 500).ToList();
        Assert.Equal(writers * perWriter, all.Count);
    }

    [Fact]
    public async Task UpdateConfidence_ConcurrentIncrements_AccumulatesWithoutLoss()
    {
        using var store = CreateStore();
        var id = await store.SaveExperienceAsync(CreateItem("并发置信度-" + Guid.NewGuid()));
        var initial = store.GetExperience(id)!;
        const int increments = 20;

        await Task.WhenAll(Enumerable.Range(0, increments).Select(_ => Task.Run(() =>
            store.UpdateConfidence(id, positive: true))));

        var loaded = store.GetExperience(id);
        Assert.NotNull(loaded);
        Assert.Equal(initial.ValidationCount + increments, loaded!.ValidationCount);
    }

    [Fact]
    public async Task MultipleInstances_ShareSameDatabase_WithoutLockTimeout()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "DeerFlowTests", Guid.NewGuid().ToString("N") + ".db");
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);

        using var first = new ExperienceMemoryStore(loggerFactory.Object, dbPath);
        using var second = new ExperienceMemoryStore(loggerFactory.Object, dbPath);
        var marker = "多实例-" + Guid.NewGuid();

        await Task.WhenAll(
            Task.Run(async () =>
            {
                for (var i = 0; i < 15; i++) await first.SaveExperienceAsync(CreateItem($"{marker}-a{i}"));
            }),
            Task.Run(async () =>
            {
                for (var i = 0; i < 15; i++) await second.SaveExperienceAsync(CreateItem($"{marker}-b{i}"));
            }));

        var fromFirst = first.RetrieveExperiences(marker, limit: 100).ToList();
        var fromSecond = second.RetrieveExperiences(marker, limit: 100).ToList();

        Assert.Equal(30, fromFirst.Count);
        Assert.Equal(30, fromSecond.Count);
    }

    [Fact]
    public async Task RetrieveExperiences_EmptyTagsAndType_RoundTripsWithoutError()
    {
        using var store = CreateStore();
        var item = CreateItem("空标签-" + Guid.NewGuid());
        item.Tags = new List<string>();
        item.Type = "custom";

        var id = await store.SaveExperienceAsync(item);
        var loaded = store.GetExperience(id);

        Assert.NotNull(loaded);
        Assert.Empty(loaded!.Tags);
        Assert.Equal("custom", loaded.Type);
    }
}
