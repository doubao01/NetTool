using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SystemToolkit.App.ViewModels;
using SystemToolkit.App.Views;
using SystemToolkit.Core.Services;

namespace SystemToolkit.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        Services = ConfigureServices();
        var window = Services.GetRequiredService<MainWindow>();
        window.Show();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<INetworkService, NetworkService>();
        services.AddSingleton<IProcessService, ProcessService>();
        services.AddSingleton<IDiskService, DiskService>();
        services.AddSingleton<IDevToolService, DevToolService>();
        services.AddSingleton<IDataToolService, DataToolService>();
        services.AddSingleton<IMonitorService, MonitorService>();
        services.AddSingleton<IServiceService, ServiceService>();
        services.AddSingleton<IRegistryService, RegistryService>();
        services.AddSingleton<IAppLauncherService, AppLauncherService>();
        services.AddSingleton<ISystemInfoService, SystemInfoService>();
        services.AddSingleton<ITextToolService, TextToolService>();
        services.AddSingleton<IDirectoryDiffService, DirectoryDiffService>();
        services.AddSingleton<IHostsFileService, HostsFileService>();
        services.AddSingleton<IUtilityWorkbenchService, UtilityWorkbenchService>();

        services.AddTransient<FileRenameView>();
        services.AddTransient<FileDedupView>();
        services.AddTransient<LargeFileFinderView>();
        services.AddTransient<PingToolView>();
        services.AddTransient<ProcessMonitorView>();
        services.AddTransient<EncryptionView>();
        services.AddTransient<SystemResourceMonitorView>();
        services.AddTransient<DiskAnalysisView>();
        services.AddTransient<FormatterView>();
        services.AddTransient<ServiceControlView>();
        services.AddTransient<PortMonitorView>();
        services.AddTransient<RegistryToolView>();
        services.AddTransient<QuickLaunchView>();
        services.AddTransient<PortScanView>();
        services.AddTransient<TracertView>();
        services.AddTransient<HttpTestView>();
        services.AddTransient<DownloadToolView>();
        services.AddTransient<MonitorAlertsView>();
        services.AddTransient<CsvToolView>();
        services.AddTransient<LogAnalyzerView>();
        services.AddTransient<CodeGenView>();
        services.AddTransient<SystemInfoView>();
        services.AddTransient<TextToolView>();
        services.AddTransient<DirectoryDiffView>();
        services.AddTransient<HostsView>();
        services.AddTransient<UtilityWorkbenchView>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }
}
