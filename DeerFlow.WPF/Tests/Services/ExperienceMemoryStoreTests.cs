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
        return new ExperienceMemoryStore(loggerFactory.Object);
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
    public void UpdateConfidence_Positive_IncrementsValidationCount()
    {
        using var store = CreateStore();
        var item = CreateItem("正向验证-" + Guid.NewGuid());
        var id = store.SaveExperienceAsync(item).GetAwaiter().GetResult();

        store.UpdateConfidence(id, positive: true);

        var loaded = store.GetExperience(id);
        Assert.NotNull(loaded);
        Assert.Equal(1, loaded!.ValidationCount);
        Assert.True(loaded.Confidence > 0);
    }

    [Fact]
    public void UpdateConfidence_Negative_DecreasesConfidence()
    {
        using var store = CreateStore();
        var item = CreateItem("负向验证-" + Guid.NewGuid());
        item.Confidence = 1.0;
        var id = store.SaveExperienceAsync(item).GetAwaiter().GetResult();

        store.UpdateConfidence(id, positive: false);

        var loaded = store.GetExperience(id);
        Assert.NotNull(loaded);
        Assert.True(loaded!.Confidence < 1.0);
    }

    [Fact]
    public void RetrieveExperiences_RespectsLimit()
    {
        using var store = CreateStore();
        var marker = "分页标记-" + Guid.NewGuid();
        for (var i = 0; i < 5; i++)
        {
            store.SaveExperienceAsync(CreateItem($"{marker}-{i}")).GetAwaiter().GetResult();
        }

        var results = store.RetrieveExperiences(marker, limit: 2).ToList();

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void RetrieveExperiences_SupportsOffset()
    {
        using var store = CreateStore();
        var marker = "偏移标记-" + Guid.NewGuid();
        for (var i = 0; i < 5; i++)
        {
            store.SaveExperienceAsync(CreateItem($"{marker}-{i}")).GetAwaiter().GetResult();
        }

        var page1 = store.RetrieveExperiences(marker, limit: 2, offset: 0).ToList();
        var page2 = store.RetrieveExperiences(marker, limit: 2, offset: 2).ToList();

        Assert.Equal(2, page1.Count);
        Assert.Equal(2, page2.Count);
        Assert.Empty(page1.Select(p => p.Id).Intersect(page2.Select(p => p.Id)));
    }

    [Fact]
    public void GetStatistics_ReturnsCounts()
    {
        using var store = CreateStore();
        store.SaveExperienceAsync(CreateItem("统计-" + Guid.NewGuid())).GetAwaiter().GetResult();

        var stats = store.GetStatistics();

        Assert.True((int)stats["TotalExperiences"] >= 1);
    }
}
