using System.Collections.ObjectModel;
using DeerFlow.WPF.Core;
using DeerFlow.WPF.Services;

namespace DeerFlow.WPF.ViewModels;

/// <summary>
/// 告警中心 ViewModel，展示告警列表、统计与确认/清除操作
/// </summary>
public class AlertsViewModel : ViewModelBase
{
    private readonly IAlertService? _alertService;
    private readonly ILoggerService _logger;

    public ObservableCollection<Alert> Alerts { get; } = new();

    private Alert? _selectedAlert;
    public Alert? SelectedAlert
    {
        get => _selectedAlert;
        set
        {
            if (SetProperty(ref _selectedAlert, value))
            {
                AcknowledgeCommand.RaiseCanExecuteChanged();
                DeleteCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private int _totalCount;
    public int TotalCount
    {
        get => _totalCount;
        set => SetProperty(ref _totalCount, value);
    }

    private int _unacknowledgedCount;
    public int UnacknowledgedCount
    {
        get => _unacknowledgedCount;
        set => SetProperty(ref _unacknowledgedCount, value);
    }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand AcknowledgeCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand ClearAcknowledgedCommand { get; }

    /// <summary>
    /// 告警中心视图模型。显式传入服务便于单元测试，缺省时从应用容器解析。
    /// </summary>
    public AlertsViewModel(ILoggerService logger, IAlertService? alertService = null)
    {
        _logger = logger;
        _alertService = alertService ?? ResolveAlertService();

        RefreshCommand = new RelayCommand(_ => Refresh());
        AcknowledgeCommand = new RelayCommand(_ => Acknowledge(), _ => SelectedAlert is { IsAcknowledged: false });
        DeleteCommand = new RelayCommand(_ => Delete(), _ => SelectedAlert is not null);
        ClearAcknowledgedCommand = new RelayCommand(_ => ClearAcknowledged());

        Refresh();
    }

    private static IAlertService? ResolveAlertService()
    {
        try
        {
            return App.Services?.GetService(typeof(IAlertService)) as IAlertService;
        }
        catch
        {
            // 单元测试等未初始化应用容器场景，退化为 "服务未启用"
            return null;
        }
    }

    private void Refresh()
    {
        Alerts.Clear();

        if (_alertService is null)
        {
            StatusMessage = "告警服务未启用";
            TotalCount = 0;
            UnacknowledgedCount = 0;
            return;
        }

        try
        {
            var stats = _alertService.GetAlertStatistics();
            TotalCount = stats.TryGetValue("TotalAlerts", out var total) && total is int t ? t : 0;
            UnacknowledgedCount = stats.TryGetValue("UnacknowledgedCount", out var un) && un is int u ? u : 0;

            foreach (var alert in _alertService.GetAllAlerts(limit: 200))
            {
                Alerts.Add(alert);
            }

            StatusMessage = Alerts.Count == 0
                ? "暂无告警，系统运行正常"
                : $"共 {TotalCount} 条告警，{UnacknowledgedCount} 条待确认";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载告警失败：{ex.Message}";
            _logger.Warn($"加载告警失败：{ex.Message}");
        }
    }

    private void Acknowledge()
    {
        if (_alertService is null || SelectedAlert is null) return;

        _alertService.AcknowledgeAlert(SelectedAlert.Id);
        Refresh();
    }

    private void Delete()
    {
        if (_alertService is null || SelectedAlert is null) return;

        _alertService.DeleteAlert(SelectedAlert.Id);
        SelectedAlert = null;
        Refresh();
    }

    private void ClearAcknowledged()
    {
        if (_alertService is null) return;

        _alertService.ClearAcknowledgedAlerts();
        Refresh();
    }
}