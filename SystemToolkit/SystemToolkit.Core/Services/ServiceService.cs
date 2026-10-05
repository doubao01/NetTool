namespace SystemToolkit.Core.Services;
using SystemToolkit.Core.Models;
using ServiceStartMode = SystemToolkit.Core.Models.ServiceStartMode;

using System.ServiceProcess;

public interface IServiceService
{
    List<ServiceInfo> GetAllServices();
    ServiceInfo? GetServiceByName(string serviceName);
    void StartService(string serviceName);
    void StopService(string serviceName);
    void RestartService(string serviceName);
    void SetServiceStartMode(string serviceName, ServiceStartMode startMode);
    List<ServiceInfo> GetRunningServices();
    List<ServiceInfo> GetStoppedServices();
}

public class ServiceService : IServiceService
{
    public List<ServiceInfo> GetAllServices()
    {
        var services = new List<ServiceInfo>();

        try
        {
            var serviceControllers = ServiceController.GetServices();
            foreach (var controller in serviceControllers)
            {
                try
                {
                    services.Add(new ServiceInfo
                    {
                        Name = controller.ServiceName,
                        DisplayName = controller.DisplayName,
                        Description = GetServiceDescription(controller.ServiceName),
                        Status = controller.Status,
                        StartMode = GetServiceStartMode(controller.ServiceName),
                        ServiceType = controller.ServiceType.ToString()
                    });
                }
                catch (Exception)
                {
                    // Skip services that can't be accessed
                }
                finally
                {
                    controller.Dispose();
                }
            }
        }
        catch (Exception)
        {
            // Handle permissions issues
        }

        return services;
    }

    public ServiceInfo? GetServiceByName(string serviceName)
    {
        try
        {
            using var controller = new ServiceController(serviceName);
            return new ServiceInfo
            {
                Name = controller.ServiceName,
                DisplayName = controller.DisplayName,
                Description = GetServiceDescription(controller.ServiceName),
                Status = controller.Status,
                StartMode = GetServiceStartMode(controller.ServiceName),
                ServiceType = controller.ServiceType.ToString()
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void StartService(string serviceName)
    {
        try
        {
            using var controller = new ServiceController(serviceName);
            if (controller.Status != ServiceControllerStatus.Running)
            {
                controller.Start();
                controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"无法启动服务：{serviceName}. 错误：{ex.Message}");
        }
    }

    public void StopService(string serviceName)
    {
        try
        {
            using var controller = new ServiceController(serviceName);
            if (controller.Status != ServiceControllerStatus.Stopped)
            {
                controller.Stop();
                controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"无法停止服务：{serviceName}. 错误：{ex.Message}");
        }
    }

    public void RestartService(string serviceName)
    {
        StopService(serviceName);
        Thread.Sleep(1000);
        StartService(serviceName);
    }

    public void SetServiceStartMode(string serviceName, ServiceStartMode startMode)
    {
        try
        {
            // WMI implementation for changing start mode
            using var managementClass = new System.Management.ManagementClass("Win32_Service");
            var options = new System.Management.ObjectGetOptions();
            using var searcher = new System.Management.ManagementObjectSearcher(
                $"SELECT * FROM Win32_Service WHERE Name = '{serviceName}'");

            foreach (var obj in searcher.Get())
            {
                using var service = (System.Management.ManagementObject)obj;
                service.InvokeMethod("ChangeStartMode", new object[] { startMode.ToString().ToLower() });
                break;
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"无法更改服务启动模式：{serviceName}. 错误：{ex.Message}");
        }
    }

    public List<ServiceInfo> GetRunningServices()
    {
        return GetAllServices().Where(s => s.Status == ServiceControllerStatus.Running).ToList();
    }

    public List<ServiceInfo> GetStoppedServices()
    {
        return GetAllServices().Where(s => s.Status == ServiceControllerStatus.Stopped).ToList();
    }

    private string GetServiceDescription(string serviceName)
    {
        try
        {
            using var hklm = Microsoft.Win32.Registry.LocalMachine;
            using var key = hklm.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}\Description");
            return key?.GetValue(null)?.ToString() ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private ServiceStartMode GetServiceStartMode(string serviceName)
    {
        try
        {
            using var hklm = Microsoft.Win32.Registry.LocalMachine;
            using var key = hklm.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}");
            var start = key?.GetValue("Start");

            return start switch
            {
                0 => ServiceStartMode.Boot,
                1 => ServiceStartMode.System,
                2 => ServiceStartMode.Automatic,
                3 => ServiceStartMode.Manual,
                4 => ServiceStartMode.Disabled,
                _ => ServiceStartMode.Manual
            };
        }
        catch (Exception)
        {
            return ServiceStartMode.Manual;
        }
    }
}
