using Xunit;
using Moq;
using DeerFlow.WPF.Models;
using DeerFlow.WPF.Services;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Linq;

namespace DeerFlow.WPF.Tests.Services;

public class FeedbackServiceTests
{
    private readonly FeedbackService _service;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;

    public FeedbackServiceTests()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _service = new FeedbackService(_loggerFactoryMock.Object);
    }

    [Fact]
    public async Task SubmitFeedbackAsync_CreatesFeedback_WithValidData()
    {
        // Arrange
        var taskId = "test-task-123";
        var feedbackType = "Like";
        var rating = 5;

        // Act
        var feedbackId = await _service.SubmitFeedbackAsync(taskId, feedbackType, rating);

        // Assert
        Assert.NotNull(feedbackId);
        Assert.NotEmpty(feedbackId);
    }

    [Fact]
    public async Task SubmitFeedbackAsync_SavesFeedback_WithAllFields()
    {
        // Arrange
        var taskId = "test-task-456";
        var feedbackType = "Dislike";
        var rating = 2;
        var comment = "需要改进响应速度";
        var relatedPatternId = "pattern-789";

        // Act
        var feedbackId = await _service.SubmitFeedbackAsync(
            taskId, feedbackType, rating, comment, relatedPatternId);

        // Assert
        var feedback = _service.GetFeedbackForTask(taskId).FirstOrDefault();
        Assert.NotNull(feedback);
        Assert.Equal(feedbackId, feedback.Id);
        Assert.Equal(taskId, feedback.TaskId);
        Assert.Equal(feedbackType, feedback.FeedbackType);
        Assert.Equal(rating, feedback.Rating);
        Assert.Equal(comment, feedback.Comment);
        Assert.Equal(relatedPatternId, feedback.RelatedPatternId);
        Assert.False(feedback.IsProcessed);
    }

    [Fact]
    public async Task SubmitFeedbackAsync_LogsWarning_ForNegativeFeedback()
    {
        // Arrange
        var taskId = "test-task-negative";
        var feedbackType = "Dislike";
        var rating = 1;

        // Act
        await _service.SubmitFeedbackAsync(taskId, feedbackType, rating, "很差");

        // Assert
        var negativeFeedback = _service.AnalyzeNegativeFeedback().FirstOrDefault();
        Assert.NotNull(negativeFeedback);
        Assert.Equal(taskId, negativeFeedback.TaskId);
    }

    [Fact]
    public void GetFeedbackForTask_ReturnsOnlyMatchingTask()
    {
        // Arrange
        var task1 = "task-1";
        var task2 = "task-2";

        // Act
        Task.Run(async () =>
        {
            await _service.SubmitFeedbackAsync(task1, "Like", 5);
            await _service.SubmitFeedbackAsync(task2, "Like", 5);
            await _service.SubmitFeedbackAsync(task1, "Dislike", 2);
        }).GetAwaiter().GetResult();

        var feedback = _service.GetFeedbackForTask(task1);

        // Assert
        Assert.Equal(2, feedback.Count);
        Assert.All(feedback, f => Assert.Equal(task1, f.TaskId));
    }

    [Fact]
    public void GetAllFeedback_ReturnsAllFeedback_OrderByDate()
    {
        // Arrange
        // Act
        Task.Run(async () =>
        {
            await _service.SubmitFeedbackAsync("task-1", "Like", 5);
            await Task.Delay(10);
            await _service.SubmitFeedbackAsync("task-2", "Like", 4);
        }).GetAwaiter().GetResult();

        var feedback = _service.GetAllFeedback();

        // Assert
        Assert.Equal(2, feedback.Count);
        Assert.True(feedback[0].CreatedAt >= feedback[1].CreatedAt);
    }

    [Fact]
    public void GetFeedbackStatistics_ReturnsCorrectStats()
    {
        // Arrange
        Task.Run(async () =>
        {
            await _service.SubmitFeedbackAsync("task-1", "Like", 5);
            await _service.SubmitFeedbackAsync("task-2", "Like", 4);
            await _service.SubmitFeedbackAsync("task-3", "Dislike", 2);
        }).GetAwaiter().GetResult();

        // Act
        var stats = _service.GetFeedbackStatistics();

        // Assert
        Assert.Equal(3, stats["TotalFeedback"]);
        Assert.Equal(2, stats["LikeCount"]);
        Assert.Equal(1, stats["DislikeCount"]);
        Assert.Equal(0, stats["ProcessedCount"]);
        Assert.Equal(3, stats["UnprocessedCount"]);
        var avgRating = Assert.IsType<double>(stats["AverageRating"]);
        Assert.InRange(avgRating, 3.6, 3.7);
    }

    [Fact]
    public void AnalyzeNegativeFeedback_ReturnsOnlyNegative()
    {
        // Arrange
        Task.Run(async () =>
        {
            await _service.SubmitFeedbackAsync("task-1", "Like", 5);
            await _service.SubmitFeedbackAsync("task-2", "Dislike", 1);
            await _service.SubmitFeedbackAsync("task-3", "Like", 4);
            await _service.SubmitFeedbackAsync("task-4", "Dislike", 2);
        }).GetAwaiter().GetResult();

        // Act
        var negative = _service.AnalyzeNegativeFeedback();

        // Assert
        Assert.Equal(2, negative.Count);
        Assert.All(negative, f => Assert.True(f.FeedbackType == "Dislike" || f.Rating <= 2));
    }

    [Fact]
    public void MarkFeedbackAsProcessed_SetsProcessedFlag()
    {
        // Arrange
        var feedbackId = Task.Run(async () => 
            await _service.SubmitFeedbackAsync("task-1", "Dislike", 2)).Result;

        // Act
        _service.MarkFeedbackAsProcessed(feedbackId);

        // Assert
        var feedback = _service.GetAllFeedback().FirstOrDefault(f => f.Id == feedbackId);
        Assert.NotNull(feedback);
        Assert.True(feedback.IsProcessed);
    }

    [Fact]
    public void MarkFeedbackAsProcessed_IgnoresInvalidId()
    {
        // Act & Assert
        // Should not throw exception
        _service.MarkFeedbackAsProcessed("invalid-id");
    }

    [Fact]
    public void GetFeedbackStatistics_CalculatesSatisfactionRate()
    {
        // Arrange
        Task.Run(async () =>
        {
            await _service.SubmitFeedbackAsync("task-1", "Like", 5);
            await _service.SubmitFeedbackAsync("task-2", "Like", 4);
            await _service.SubmitFeedbackAsync("task-3", "Dislike", 3);
            await _service.SubmitFeedbackAsync("task-4", "Dislike", 1);
        }).GetAwaiter().GetResult();

        // Act
        var stats = _service.GetFeedbackStatistics();

        // Assert
        var satisfactionRate = Assert.IsType<double>(stats["SatisfactionRate"]);
        Assert.InRange(satisfactionRate, 0.49, 0.51); // 2 out of 4 (Like or rating >= 4)
    }
}
