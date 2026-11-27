using System.Windows;
using DeerFlow.WPF.Core;
using DeerFlow.WPF.Models;
using DeerFlow.WPF.Services;

namespace DeerFlow.WPF.ViewModels;

/// <summary>
/// IM 配置页 ViewModel，管理 IM 连接的列表选择、表单编辑和操作命令
/// </summary>
public class IMSettingsViewModel : ViewModelBase
{
    private readonly IIMPlatformService _imService;
    private readonly ILoggerService _logger;

    #region 列表属性

    /// <summary>所有 IM 连接列表</summary>
    public AsyncObservableCollection<IMConnection> Connections { get; } = new();

    private IMConnection? _selectedConnection;
    /// <summary>当前选中的连接</summary>
    public IMConnection? SelectedConnection
    {
        get => _selectedConnection;
        set
        {
            if (SetProperty(ref _selectedConnection, value) && value != null)
            {
                LoadConnectionToForm(value);
            }
        }
    }

    #endregion

    #region 编辑表单属性

    private string _editingDisplayName = string.Empty;
    /// <summary>编辑中的显示名称</summary>
    public string EditingDisplayName
    {
        get => _editingDisplayName;
        set => SetProperty(ref _editingDisplayName, value);
    }

    private PlatformType _editingPlatformType = PlatformType.Feishu;
    /// <summary>编辑中的平台类型</summary>
    public PlatformType EditingPlatformType
    {
        get => _editingPlatformType;
        set => SetProperty(ref _editingPlatformType, value);
    }

    private string _editingWebhookUrl = string.Empty;
    /// <summary>编辑中的 Webhook URL</summary>
    public string EditingWebhookUrl
    {
        get => _editingWebhookUrl;
        set => SetProperty(ref _editingWebhookUrl, value);
    }

    private string _editingToken = string.Empty;
    /// <summary>编辑中的令牌</summary>
    public string EditingToken
    {
        get => _editingToken;
        set => SetProperty(ref _editingToken, value);
    }

    private bool _editingIsEnabled;
    /// <summary>编辑中的启用状态</summary>
    public bool EditingIsEnabled
    {
        get => _editingIsEnabled;
        set => SetProperty(ref _editingIsEnabled, value);
    }

    #endregion

    #region 状态属性

    private bool _isEditing;
    /// <summary>是否正在编辑</summary>
    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    private bool _isTesting;
    /// <summary>是否正在测试连接</summary>
    public bool IsTesting
    {
        get => _isTesting;
        set => SetProperty(ref _isTesting, value);
    }

    private string _statusMessage = string.Empty;
    /// <summary>状态提示消息</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private bool _isTokenVisible;
    /// <summary>Token 是否以明文显示</summary>
    public bool IsTokenVisible
    {
        get => _isTokenVisible;
        set
        {
            if (SetProperty(ref _isTokenVisible, value))
            {
                OnPropertyChanged(nameof(IsTokenHidden));
            }
        }
    }

    /// <summary>Token 是否以密码遮蔽（与 IsTokenVisible 互斥）</summary>
    public bool IsTokenHidden => !_isTokenVisible;

    #endregion

    #region 命令

    /// <summary>新建连接命令</summary>
    public RelayCommand NewConnectionCommand { get; }

    /// <summary>保存连接命令</summary>
    public RelayCommand SaveConnectionCommand { get; }

    /// <summary>删除连接命令</summary>
    public RelayCommand DeleteConnectionCommand { get; }

    /// <summary>测试连接命令</summary>
    public RelayCommand TestConnectionCommand { get; }

    /// <summary>切换 Token 可见性命令</summary>
    public RelayCommand ToggleTokenVisibilityCommand { get; }

    #endregion

    public IMSettingsViewModel(IIMPlatformService imService, ILoggerService logger)
    {
        _imService = imService;
        _logger = logger;

        NewConnectionCommand = new RelayCommand(_ => CreateNewConnection());
        SaveConnectionCommand = new RelayCommand(new Action(async () => await SaveConnectionAsync()));
        DeleteConnectionCommand = new RelayCommand(new Action(async () => await DeleteConnectionAsync()));
        TestConnectionCommand = new RelayCommand(new Action(async () => await TestConnectionAsync()));
        ToggleTokenVisibilityCommand = new RelayCommand(_ => IsTokenVisible = !IsTokenVisible);

        RefreshConnections();
    }

    /// <summary>
    /// 刷新连接列表
    /// </summary>
    private void RefreshConnections()
    {
        var connections = _imService.GetAllConnections();
        Connections.ReplaceAll(connections);
    }

    /// <summary>
    /// 创建新连接
    /// </summary>
    private void CreateNewConnection()
    {
        SelectedConnection = null;
        IsEditing = true;
        EditingDisplayName = string.Empty;
        EditingPlatformType = PlatformType.Feishu;
        EditingWebhookUrl = string.Empty;
        EditingToken = string.Empty;
        EditingIsEnabled = false;
        StatusMessage = "正在新建连接...";
    }

    /// <summary>
    /// 将选中连接的信息加载到编辑表单
    /// </summary>
    private void LoadConnectionToForm(IMConnection connection)
    {
        IsEditing = true;
        EditingDisplayName = connection.DisplayName;
        EditingPlatformType = connection.PlatformType;
        EditingWebhookUrl = connection.WebhookUrl;
        EditingToken = connection.Token;
        EditingIsEnabled = connection.IsEnabled;
    }

    /// <summary>
    /// 保存当前连接
    /// </summary>
    private async Task SaveConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(EditingDisplayName) || string.IsNullOrWhiteSpace(EditingWebhookUrl))
        {
            StatusMessage = "名称和 Webhook URL 不能为空";
            return;
        }

        if (SelectedConnection != null)
        {
            SelectedConnection.DisplayName = EditingDisplayName;
            SelectedConnection.PlatformType = EditingPlatformType;
            SelectedConnection.WebhookUrl = EditingWebhookUrl;
            SelectedConnection.Token = EditingToken;
            SelectedConnection.IsEnabled = EditingIsEnabled;
            _imService.UpdateConnection(SelectedConnection);
            StatusMessage = $"已更新: {EditingDisplayName}";
        }
        else
        {
            var connection = new IMConnection
            {
                DisplayName = EditingDisplayName,
                PlatformType = EditingPlatformType,
                WebhookUrl = EditingWebhookUrl,
                Token = EditingToken,
                IsEnabled = EditingIsEnabled
            };
            _imService.AddConnection(connection);
            StatusMessage = $"已添加: {EditingDisplayName}";
        }

        await Task.Delay(200);
        RefreshConnections();
        _logger.Info($"IM 连接已保存: {EditingDisplayName}");
    }

    /// <summary>
    /// 删除当前选中连接
    /// </summary>
    private async Task DeleteConnectionAsync()
    {
        if (SelectedConnection == null)
            return;

        var result = MessageBox.Show(
            $"确认删除连接 \"{SelectedConnection.DisplayName}\" ?",
            "确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        var name = SelectedConnection.DisplayName;
        _imService.RemoveConnection(SelectedConnection.Id);
        SelectedConnection = null;
        IsEditing = false;
        RefreshConnections();
        StatusMessage = $"已删除: {name}";
    }

    /// <summary>
    /// 测试当前选中连接
    /// </summary>
    private async Task TestConnectionAsync()
    {
        if (SelectedConnection == null)
        {
            StatusMessage = "请先选择一个连接";
            return;
        }

        IsTesting = true;
        StatusMessage = $"正在测试: {SelectedConnection.DisplayName}...";

        var success = await _imService.TestConnectionAsync(SelectedConnection.Id);
        StatusMessage = success
            ? $"连接成功: {SelectedConnection.DisplayName}"
            : $"连接失败: {SelectedConnection.DisplayName}";

        IsTesting = false;
    }

    /// <summary>
    /// 切换启用/禁用状态
    /// </summary>
    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}
