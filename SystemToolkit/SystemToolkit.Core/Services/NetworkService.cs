namespace SystemToolkit.Core.Services;
using SystemToolkit.Core.Models;

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

public interface INetworkService
{
    Task<PingResult> PingAsync(string host, int timeout = 5000);
    Task<List<PortScanResult>> ScanPortsAsync(string host, List<int> ports, CancellationToken ct = default);
    Task<List<TracertHop>> TraceRouteAsync(string host, int maxHops = 30, CancellationToken ct = default);
    Task<HttpTestResponse> SendHttpRequestAsync(HttpTestRequest request, CancellationToken ct = default);
    List<PortMonitorInfo> GetPortUsage();
    Task<long> DownloadFileAsync(string url, string savePath, IProgress<double>? progress = null, CancellationToken ct = default);
}

public class NetworkService : INetworkService
{
    public async Task<PingResult> PingAsync(string host, int timeout = 5000)
    {
        using var ping = new Ping();
        try
        {
            var reply = await ping.SendPingAsync(host, timeout);
            return new PingResult
            {
                Host = host,
                TimeToLive = reply.Options?.Ttl ?? 0,
                RoundtripTime = reply.RoundtripTime,
                Status = reply.Status.ToString(),
                Success = reply.Status == IPStatus.Success,
                Error = reply.Status != IPStatus.Success ? reply.Status.ToString() : null
            };
        }
        catch (Exception ex)
        {
            return new PingResult
            {
                Host = host,
                Success = false,
                Status = "Error",
                Error = ex.Message
            };
        }
    }

    public async Task<List<PortScanResult>> ScanPortsAsync(string host, List<int> ports, CancellationToken ct = default)
    {
        var results = new List<PortScanResult>();

        foreach (var port in ports)
        {
            ct.ThrowIfCancellationRequested();

            var result = await ScanPortAsync(host, port);
            results.Add(result);
        }

        return results;
    }

    private async Task<PortScanResult> ScanPortAsync(string host, int port)
    {
        using var tcpClient = new TcpClient();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var cts = new CancellationTokenSource(3000);
            await tcpClient.ConnectAsync(host, port, cts.Token);
            stopwatch.Stop();

            return new PortScanResult
            {
                Host = host,
                Port = port,
                IsOpen = true,
                ResponseTime = stopwatch.ElapsedMilliseconds,
                ServiceName = GetServiceName(port)
            };
        }
        catch (Exception)
        {
            return new PortScanResult
            {
                Host = host,
                Port = port,
                IsOpen = false,
                ResponseTime = stopwatch.ElapsedMilliseconds
            };
        }
        finally
        {
            tcpClient.Close();
        }
    }

    private string? GetServiceName(int port)
    {
        return port switch
        {
            21 => "FTP",
            22 => "SSH",
            23 => "Telnet",
            25 => "SMTP",
            53 => "DNS",
            80 => "HTTP",
            110 => "POP3",
            143 => "IMAP",
            443 => "HTTPS",
            993 => "IMAPS",
            995 => "POP3S",
            3306 => "MySQL",
            3389 => "RDP",
            5432 => "PostgreSQL",
            6379 => "Redis",
            8080 => "HTTP-Proxy",
            _ => null
        };
    }

    public async Task<List<TracertHop>> TraceRouteAsync(string host, int maxHops = 30, CancellationToken ct = default)
    {
        var hops = new List<TracertHop>();

        using var ping = new Ping();

        for (int ttl = 1; ttl <= maxHops; ttl++)
        {
            ct.ThrowIfCancellationRequested();

            var options = new PingOptions(ttl, true);
            try
            {
                var reply = await ping.SendPingAsync(host, 3000, new byte[32], options);

                var hop = new TracertHop
                {
                    HopNumber = ttl,
                    IpAddress = reply.Address?.ToString() ?? "*",
                    RoundtripTime1 = reply.RoundtripTime,
                    RoundtripTime2 = reply.RoundtripTime,
                    RoundtripTime3 = reply.RoundtripTime
                };

                if (reply.Address != null)
                {
                    try
                    {
                        var hostEntry = Dns.GetHostEntry(reply.Address);
                        hop.Hostname = hostEntry.HostName;
                    }
                    catch
                    {
                        hop.Hostname = null;
                    }
                }

                hops.Add(hop);

                if (reply.Status == IPStatus.Success)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                hops.Add(new TracertHop
                {
                    HopNumber = ttl,
                    IpAddress = "*",
                    RoundtripTime1 = -1,
                    RoundtripTime2 = -1,
                    RoundtripTime3 = -1
                });
            }
        }

        return hops;
    }

    public async Task<HttpTestResponse> SendHttpRequestAsync(HttpTestRequest request, CancellationToken ct = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var handler = new HttpClientHandler();
            if (request.ContentType?.Contains("application/json") == true)
            {
                handler.AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate;
            }

            using var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(30);

            // Add headers
            foreach (var header in request.Headers)
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }

            HttpResponseMessage response;
            if (!string.IsNullOrEmpty(request.Body))
            {
                var content = new StringContent(request.Body);
                if (!string.IsNullOrEmpty(request.ContentType))
                {
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(request.ContentType);
                }

                response = request.Method.ToUpperInvariant() switch
                {
                    "POST" => await client.PostAsync(request.Url, content, ct),
                    "PUT" => await client.PutAsync(request.Url, content, ct),
                    "DELETE" => await client.DeleteAsync(request.Url, ct),
                    "PATCH" => await client.SendAsync(new HttpRequestMessage(new HttpMethod("PATCH"), request.Url) { Content = content }, ct),
                    _ => await client.GetAsync(request.Url, ct)
                };
            }
            else
            {
                response = request.Method.ToUpperInvariant() switch
                {
                    "POST" => await client.PostAsync(request.Url, null, ct),
                    "PUT" => await client.PutAsync(request.Url, null, ct),
                    "DELETE" => await client.DeleteAsync(request.Url, ct),
                    _ => await client.GetAsync(request.Url, ct)
                };
            }

            stopwatch.Stop();

            var responseString = await response.Content.ReadAsStringAsync(ct);

            return new HttpTestResponse
            {
                StatusCode = (int)response.StatusCode,
                StatusDescription = response.ReasonPhrase ?? string.Empty,
                Headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
                Body = responseString,
                ResponseTime = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            return new HttpTestResponse
            {
                StatusCode = 0,
                StatusDescription = "Error",
                Body = string.Empty,
                ResponseTime = stopwatch.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    public List<PortMonitorInfo> GetPortUsage()
    {
        var portInfo = new List<PortMonitorInfo>();

        try
        {
            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            var connections = ipGlobalProperties.GetActiveTcpConnections();

            foreach (var conn in connections)
            {
                portInfo.Add(new PortMonitorInfo
                {
                    Port = conn.LocalEndPoint.Port,
                    LocalAddress = conn.LocalEndPoint.Address.ToString(),
                    RemoteAddress = conn.RemoteEndPoint.Address.ToString(),
                    State = conn.State.ToString(),
                    ProcessId = GetProcessIdForPort(conn.LocalEndPoint.Port),
                    ProcessName = GetProcessNameForPort(conn.LocalEndPoint.Port)
                });
            }
        }
        catch (Exception)
        {
            // Handle permissions issues
        }

        return portInfo;
    }

    private int GetProcessIdForPort(int port)
    {
        // This would require P/Invoke to IPHelper API for full implementation
        return 0;
    }

    private string? GetProcessNameForPort(int port)
    {
        var pid = GetProcessIdForPort(port);
        if (pid > 0)
        {
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById(pid);
                return process.ProcessName;
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    public async Task<long> DownloadFileAsync(string url, string savePath, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromMinutes(10);

        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        var downloadedBytes = 0L;

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var fileStream = File.Create(savePath);

        var buffer = new byte[81920];
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var read = await stream.ReadAsync(buffer, ct);
            if (read == 0)
                break;

            await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
            downloadedBytes += read;

            if (totalBytes > 0 && progress != null)
            {
                var percent = (double)downloadedBytes / totalBytes * 100;
                progress.Report(percent);
            }
        }

        return downloadedBytes;
    }
}
