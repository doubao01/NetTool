using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DeerFlow.WPF.Models;

/// <summary>
/// 聊天消息模型，实现 INotifyPropertyChanged 以支持流式响应 UI 更新
/// </summary>
public class ChatMessage : INotifyPropertyChanged
{
    /// <summary>消息唯一标识</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    private string _role = "user";
    /// <summary>角色（user/assistant/system）</summary>
    public string Role
    {
        get => _role;
        set { _role = value; OnPropertyChanged(); }
    }

    private string _content = string.Empty;
    /// <summary>消息内容</summary>
    public string Content
    {
        get => _content;
        set { _content = value; OnPropertyChanged(); }
    }

    private DateTime _timestamp = DateTime.Now;
    /// <summary>发送时间</summary>
    public DateTime Timestamp
    {
        get => _timestamp;
        set { _timestamp = value; OnPropertyChanged(); }
    }

    private bool _isStreaming;
    /// <summary>是否为流式响应中的消息</summary>
    public bool IsStreaming
    {
        get => _isStreaming;
        set { _isStreaming = value; OnPropertyChanged(); }
    }

    /// <summary>关联的任务ID</summary>
    public string? TaskId { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// 用户反馈类型（Like/Dislike）
    /// </summary>
    public string? FeedbackType { get; set; }

    /// <summary>
    /// 反馈评分（1-5）
    /// </summary>
    public int? FeedbackRating { get; set; }
}
