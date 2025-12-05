// 💻 IPC System Info Service - Hardware and OS Information
// Collects system information for Industrial PC monitoring

using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Management;
using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Services;

/// <summary>
/// Interface for IPC system information
/// </summary>
public interface IIpcInfoService
{
    /// <summary>Get complete system information</summary>
    Task<IpcSystemInfo> GetSystemInfoAsync();
    
    /// <summary>Get quick summary (for frequent polling)</summary>
    Task<IpcQuickStatus> GetQuickStatusAsync();
}

/// <summary>
/// Quick status for frequent updates
/// </summary>
public class IpcQuickStatus
{
    public double CpuUsagePercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public double DiskUsagePercent { get; set; }
    public string OsName { get; set; } = "";
    public string Uptime { get; set; } = "";
}

/// <summary>
/// Service to collect IPC system information
/// </summary>
public class IpcInfoService : IIpcInfoService
{
    private readonly ILogger<IpcInfoService> _logger;
    private IpcSystemInfo? _cachedInfo;
    private DateTime _cacheTimestamp;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
    
    // Performance counters
    private PerformanceCounter? _cpuCounter;
    private List<PerformanceCounter>? _coreCounters;

    public IpcInfoService(ILogger<IpcInfoService> logger)
    {
        _logger = logger;
        InitializePerformanceCounters();
    }

    private void InitializePerformanceCounters()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuCounter.NextValue(); // First call always returns 0
                
                // Initialize per-core counters
                _coreCounters = new List<PerformanceCounter>();
                int processorCount = Environment.ProcessorCount;
                for (int i = 0; i < processorCount; i++)
                {
                    var counter = new PerformanceCounter("Processor", "% Processor Time", i.ToString());
                    counter.NextValue(); // First call always returns 0
                    _coreCounters.Add(counter);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not initialize performance counters");
        }
    }

    /// <summary>
    /// Get complete system information
    /// </summary>
    public async Task<IpcSystemInfo> GetSystemInfoAsync()
    {
        // Check cache
        if (_cachedInfo != null && DateTime.UtcNow - _cacheTimestamp < _cacheExpiration)
        {
            // Update dynamic values only
            _cachedInfo.Cpu.UsagePercent = GetCpuUsage();
            _cachedInfo.Memory = GetMemoryInfo();
            _cachedInfo.OperatingSystem.Uptime = GetSystemUptime();
            _cachedInfo.OperatingSystem.UptimeFormatted = FormatUptime(_cachedInfo.OperatingSystem.Uptime);
            _cachedInfo.Runtime.ProcessUptime = DateTime.Now - _cachedInfo.Runtime.ProcessStartTime;
            _cachedInfo.Runtime.ProcessUptimeFormatted = FormatUptime(_cachedInfo.Runtime.ProcessUptime);
            _cachedInfo.CollectedAt = DateTime.UtcNow;
            return _cachedInfo;
        }

        _logger.LogInformation("💻 Collecting IPC system information...");

        var info = new IpcSystemInfo
        {
            OperatingSystem = GetOsInfo(),
            Cpu = await GetCpuInfoAsync(),
            Memory = GetMemoryInfo(),
            Disk = GetDiskInfo(),
            Network = GetNetworkInfo(),
            Runtime = GetRuntimeInfo(),
            Security = await GetSecurityInfoAsync(),
            CollectedAt = DateTime.UtcNow
        };

        _cachedInfo = info;
        _cacheTimestamp = DateTime.UtcNow;

        _logger.LogInformation("💻 IPC info collected: {OS} | {CPU} | RAM {RAM}%", 
            info.OperatingSystem.Name, 
            info.Cpu.Name,
            info.Memory.UsagePercent.ToString("F0"));

        return info;
    }

    /// <summary>
    /// Get quick status for frequent polling
    /// </summary>
    public Task<IpcQuickStatus> GetQuickStatusAsync()
    {
        var memory = GetMemoryInfo();
        var disk = GetDiskInfo();
        var uptime = GetSystemUptime();

        return Task.FromResult(new IpcQuickStatus
        {
            CpuUsagePercent = GetCpuUsage(),
            MemoryUsagePercent = memory.UsagePercent,
            DiskUsagePercent = disk.UsagePercent,
            OsName = $"{Environment.OSVersion.Platform} {Environment.OSVersion.Version}",
            Uptime = FormatUptime(uptime)
        });
    }

    // ==================== OS Information ====================

    private OsInfo GetOsInfo()
    {
        var info = new OsInfo
        {
            Architecture = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit",
            Uptime = GetSystemUptime()
        };

        info.UptimeFormatted = FormatUptime(info.Uptime);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                info.Name = GetWindowsProductName();
                info.Build = Environment.OSVersion.Version.ToString();
                info.Version = GetWindowsDisplayVersion();
                info.Edition = GetWindowsEdition();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting Windows details");
                info.Name = RuntimeInformation.OSDescription;
            }
        }
        else
        {
            info.Name = RuntimeInformation.OSDescription;
        }

        return info;
    }

    private string GetWindowsProductName()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            return key?.GetValue("ProductName")?.ToString() ?? "Windows";
        }
        catch
        {
            return "Windows";
        }
    }

    private string GetWindowsDisplayVersion()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            return key?.GetValue("DisplayVersion")?.ToString() ?? "";
        }
        catch
        {
            return "";
        }
    }

    private string GetWindowsEdition()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            return key?.GetValue("EditionID")?.ToString() ?? "";
        }
        catch
        {
            return "";
        }
    }

    private TimeSpan GetSystemUptime()
    {
        return TimeSpan.FromMilliseconds(Environment.TickCount64);
    }

    // ==================== CPU Information ====================

    private async Task<CpuInfo> GetCpuInfoAsync()
    {
        var info = new CpuInfo
        {
            LogicalProcessors = Environment.ProcessorCount,
            CoreUsages = new List<CoreUsage>()
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    info.Name = obj["Name"]?.ToString()?.Trim() ?? "Unknown CPU";
                    info.Manufacturer = obj["Manufacturer"]?.ToString() ?? "";
                    info.Cores = Convert.ToInt32(obj["NumberOfCores"] ?? 0);
                    info.MaxSpeedMHz = Convert.ToDouble(obj["MaxClockSpeed"] ?? 0);
                    info.SpeedMHz = Convert.ToDouble(obj["CurrentClockSpeed"] ?? 0);
                    break; // Usually only one CPU
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting CPU info via WMI");
                info.Name = "Unknown CPU";
                info.Cores = Environment.ProcessorCount;
            }
        }

        info.UsagePercent = GetCpuUsage();
        info.CoreUsages = GetCoreUsages();

        return info;
    }

    private double GetCpuUsage()
    {
        try
        {
            if (_cpuCounter != null)
            {
                return Math.Round(_cpuCounter.NextValue(), 1);
            }
        }
        catch { }
        return 0;
    }

    private List<CoreUsage> GetCoreUsages()
    {
        var usages = new List<CoreUsage>();
        try
        {
            if (_coreCounters != null)
            {
                for (int i = 0; i < _coreCounters.Count; i++)
                {
                    usages.Add(new CoreUsage
                    {
                        CoreId = i,
                        UsagePercent = Math.Round(_coreCounters[i].NextValue(), 1)
                    });
                }
            }
        }
        catch { }
        return usages;
    }

    // ==================== Memory Information ====================

    private MemoryInfo GetMemoryInfo()
    {
        var info = new MemoryInfo();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                var memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                {
                    info.TotalBytes = (long)memStatus.ullTotalPhys;
                    info.AvailableBytes = (long)memStatus.ullAvailPhys;
                    info.UsedBytes = info.TotalBytes - info.AvailableBytes;
                    info.UsagePercent = Math.Round((double)info.UsedBytes / info.TotalBytes * 100, 1);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting memory info");
            }
        }

        info.TotalFormatted = FormatBytes(info.TotalBytes);
        info.AvailableFormatted = FormatBytes(info.AvailableBytes);
        info.UsedFormatted = FormatBytes(info.UsedBytes);

        return info;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    // ==================== Disk Information ====================

    private DiskInfo GetDiskInfo()
    {
        var info = new DiskInfo { DriveLetter = "C:" };

        try
        {
            var drive = new DriveInfo("C");
            if (drive.IsReady)
            {
                info.TotalBytes = drive.TotalSize;
                info.FreeBytes = drive.AvailableFreeSpace;
                info.UsedBytes = info.TotalBytes - info.FreeBytes;
                info.UsagePercent = Math.Round((double)info.UsedBytes / info.TotalBytes * 100, 1);
                info.DriveType = drive.DriveType.ToString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting disk info");
        }

        info.TotalFormatted = FormatBytes(info.TotalBytes);
        info.FreeFormatted = FormatBytes(info.FreeBytes);
        info.UsedFormatted = FormatBytes(info.UsedBytes);

        return info;
    }

    // ==================== Network Information ====================

    private NetworkInfo GetNetworkInfo()
    {
        var info = new NetworkInfo
        {
            Hostname = Environment.MachineName,
            DomainName = Environment.UserDomainName
        };

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                // Include all adapters except Loopback for full visibility
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                var adapter = new NetworkAdapter
                {
                    Name = nic.Name,
                    Description = nic.Description,
                    Status = nic.OperationalStatus.ToString(),
                    SpeedMbps = nic.Speed / 1_000_000,
                    Speed = FormatNetworkSpeed(nic.Speed),
                    MacAddress = FormatMacAddress(nic.GetPhysicalAddress().ToString()),
                    AdapterType = GetAdapterType(nic.NetworkInterfaceType)
                };

                var ipProps = nic.GetIPProperties();
                
                // Get DHCP status
                try
                {
                    adapter.IsDhcpEnabled = ipProps.GetIPv4Properties()?.IsDhcpEnabled ?? false;
                }
                catch { adapter.IsDhcpEnabled = false; }

                // Get IP addresses
                foreach (var addr in ipProps.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        adapter.IpAddress = addr.Address.ToString();
                        adapter.SubnetMask = addr.IPv4Mask?.ToString();
                        break;
                    }
                }

                // Get Gateway
                var gateway = ipProps.GatewayAddresses
                    .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);
                adapter.Gateway = gateway?.Address.ToString();

                // Get DNS Servers
                var dnsServers = ipProps.DnsAddresses
                    .Where(d => d.AddressFamily == AddressFamily.InterNetwork)
                    .Select(d => d.ToString())
                    .Take(2);
                adapter.DnsServers = string.Join(", ", dnsServers);

                // Set primary if UP and not set yet
                if (nic.OperationalStatus == OperationalStatus.Up && 
                    !string.IsNullOrEmpty(adapter.IpAddress) &&
                    string.IsNullOrEmpty(info.PrimaryIpAddress))
                {
                    info.PrimaryIpAddress = adapter.IpAddress;
                    info.PrimaryMacAddress = adapter.MacAddress;
                }

                info.Adapters.Add(adapter);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting network info");
        }

        return info;
    }

    private string GetAdapterType(NetworkInterfaceType type)
    {
        return type switch
        {
            NetworkInterfaceType.Ethernet => "Ethernet",
            NetworkInterfaceType.Wireless80211 => "WiFi",
            NetworkInterfaceType.GigabitEthernet => "Gigabit Ethernet",
            NetworkInterfaceType.FastEthernetT => "Fast Ethernet",
            NetworkInterfaceType.Ppp => "VPN/PPP",
            NetworkInterfaceType.Tunnel => "VPN Tunnel",
            _ => type.ToString()
        };
    }

    private string FormatMacAddress(string mac)
    {
        if (string.IsNullOrEmpty(mac) || mac.Length != 12) return mac;
        return string.Join(":", Enumerable.Range(0, 6).Select(i => mac.Substring(i * 2, 2)));
    }

    private string FormatNetworkSpeed(long speedBps)
    {
        if (speedBps <= 0) return "Unknown";
        if (speedBps >= 1_000_000_000) return $"{speedBps / 1_000_000_000} Gbps";
        if (speedBps >= 1_000_000) return $"{speedBps / 1_000_000} Mbps";
        if (speedBps >= 1_000) return $"{speedBps / 1_000} Kbps";
        return $"{speedBps} bps";
    }

    // ==================== Runtime Information ====================

    private RuntimeInfo GetRuntimeInfo()
    {
        var process = Process.GetCurrentProcess();
        var startTime = process.StartTime;

        return new RuntimeInfo
        {
            DotNetVersion = Environment.Version.ToString(),
            DotNetRuntime = RuntimeInformation.FrameworkDescription,
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            ProcessId = process.Id,
            WorkingSetBytes = process.WorkingSet64,
            WorkingSetFormatted = FormatBytes(process.WorkingSet64),
            ProcessStartTime = startTime,
            ProcessUptime = DateTime.Now - startTime,
            ProcessUptimeFormatted = FormatUptime(DateTime.Now - startTime)
        };
    }

    // ==================== Security Information ====================

    private async Task<SecurityInfo> GetSecurityInfoAsync()
    {
        var info = new SecurityInfo
        {
            SecurityStatus = "Unknown",
            Warnings = new List<string>(),
            InstalledAntivirus = new List<AntivirusProduct>(),
            RealTimeProtection = false
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                // Check Windows Defender (including real-time protection)
                var (defenderEnabled, realTimeEnabled) = await CheckWindowsDefenderWithDetailsAsync();
                info.WindowsDefenderEnabled = defenderEnabled;
                info.RealTimeProtection = realTimeEnabled;
                
                // Check Firewall
                info.FirewallEnabled = CheckFirewallEnabled();
                
                // Check Auto Update
                info.AutoUpdateEnabled = CheckAutoUpdateEnabled();
                
                // Check UAC
                info.UacEnabled = CheckUacEnabled();
                
                // Check for third-party antivirus products via WMI SecurityCenter2
                await DetectAntivirusProductsAsync(info);

                // Determine overall status
                bool hasActiveProtection = info.WindowsDefenderEnabled || 
                    info.InstalledAntivirus.Any(av => av.IsEnabled);
                
                if (hasActiveProtection && info.FirewallEnabled)
                {
                    info.SecurityStatus = "Good";
                }
                else if (hasActiveProtection || info.FirewallEnabled)
                {
                    info.SecurityStatus = "Warning";
                }
                else
                {
                    info.SecurityStatus = "Critical";
                }
                
                // Generate warnings
                if (!info.WindowsDefenderEnabled && !info.HasThirdPartyAntivirus)
                    info.Warnings.Add("No active antivirus protection detected");
                else if (!info.WindowsDefenderEnabled && info.HasThirdPartyAntivirus)
                    info.Warnings.Add("Windows Defender disabled (third-party AV active)");
                    
                if (!info.FirewallEnabled)
                    info.Warnings.Add("Windows Firewall is disabled");
                    
                if (!info.UacEnabled)
                    info.Warnings.Add("User Account Control (UAC) is disabled");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking security status");
                info.SecurityStatus = "Unknown";
            }
        }

        return info;
    }

    private async Task DetectAntivirusProductsAsync(SecurityInfo info)
    {
        try
        {
            // Query Windows Security Center for registered antivirus products
            // This works on Windows 7+ and detects third-party AV like Kaspersky, Norton, ESET, etc.
            using var searcher = new ManagementObjectSearcher(
                @"root\SecurityCenter2",
                "SELECT * FROM AntiVirusProduct");

            foreach (var obj in searcher.Get())
            {
                var displayName = obj["displayName"]?.ToString() ?? "";
                var productState = Convert.ToUInt32(obj["productState"] ?? 0);
                
                // Decode product state (bit field)
                // Bits 4-7: Scanner state (0x10 = on, 0x00 = off)
                // Bits 8-15: Definition state (0x00 = up to date, 0x10 = out of date)
                var isEnabled = ((productState >> 12) & 0xF) == 0x1;
                var isUpToDate = ((productState >> 4) & 0xF) == 0x0;
                
                var product = new AntivirusProduct
                {
                    Name = displayName,
                    Publisher = GetPublisherFromName(displayName),
                    IsEnabled = isEnabled,
                    IsUpToDate = isUpToDate,
                    State = isEnabled ? (isUpToDate ? "Active" : "Outdated") : "Disabled",
                    ProductType = "Antivirus"
                };
                
                info.InstalledAntivirus.Add(product);
                
                // Check if it's NOT Windows Defender
                if (!displayName.Contains("Windows Defender", StringComparison.OrdinalIgnoreCase) &&
                    !displayName.Contains("Microsoft Defender", StringComparison.OrdinalIgnoreCase))
                {
                    info.HasThirdPartyAntivirus = true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not query SecurityCenter2 (may not be available on server OS)");
        }
        
        await Task.CompletedTask;
    }

    private string GetPublisherFromName(string name)
    {
        if (name.Contains("Kaspersky", StringComparison.OrdinalIgnoreCase)) return "Kaspersky Lab";
        if (name.Contains("Norton", StringComparison.OrdinalIgnoreCase)) return "NortonLifeLock";
        if (name.Contains("ESET", StringComparison.OrdinalIgnoreCase)) return "ESET";
        if (name.Contains("Avast", StringComparison.OrdinalIgnoreCase)) return "Avast";
        if (name.Contains("AVG", StringComparison.OrdinalIgnoreCase)) return "AVG Technologies";
        if (name.Contains("Bitdefender", StringComparison.OrdinalIgnoreCase)) return "Bitdefender";
        if (name.Contains("McAfee", StringComparison.OrdinalIgnoreCase)) return "McAfee";
        if (name.Contains("Trend Micro", StringComparison.OrdinalIgnoreCase)) return "Trend Micro";
        if (name.Contains("Sophos", StringComparison.OrdinalIgnoreCase)) return "Sophos";
        if (name.Contains("F-Secure", StringComparison.OrdinalIgnoreCase)) return "F-Secure";
        if (name.Contains("Malwarebytes", StringComparison.OrdinalIgnoreCase)) return "Malwarebytes";
        if (name.Contains("Windows Defender", StringComparison.OrdinalIgnoreCase)) return "Microsoft";
        if (name.Contains("Microsoft Defender", StringComparison.OrdinalIgnoreCase)) return "Microsoft";
        return "Unknown";
    }

    private bool CheckUacEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
            var value = key?.GetValue("EnableLUA");
            return value != null && (int)value == 1;
        }
        catch
        {
            return false;
        }
    }

    private Task<(bool DefenderEnabled, bool RealTimeEnabled)> CheckWindowsDefenderWithDetailsAsync()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\Microsoft\Windows\Defender",
                "SELECT * FROM MSFT_MpComputerStatus");
            
            foreach (var obj in searcher.Get())
            {
                var antivirusEnabled = obj["AntivirusEnabled"];
                var realTimeEnabled = obj["RealTimeProtectionEnabled"];
                
                bool defenderOn = antivirusEnabled != null && (bool)antivirusEnabled;
                bool realTimeOn = realTimeEnabled != null && (bool)realTimeEnabled;
                
                return Task.FromResult((defenderOn, realTimeOn));
            }
        }
        catch
        {
            // Windows Defender WMI may not be available
        }
        return Task.FromResult((false, false));
    }

    private bool CheckFirewallEnabled()
    {
        try
        {
            // Check all three firewall profiles (Domain, Private, Public)
            string[] profiles = {
                @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\DomainProfile",
                @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile",
                @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\PublicProfile"
            };

            foreach (var profilePath in profiles)
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(profilePath);
                var value = key?.GetValue("EnableFirewall");
                if (value != null && (int)value == 1)
                {
                    return true; // At least one profile has firewall enabled
                }
            }
            
            // Fallback: Try WMI method for Windows Firewall
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    @"root\StandardCimv2",
                    "SELECT * FROM MSFT_NetFirewallProfile");
                
                foreach (var obj in searcher.Get())
                {
                    var enabled = obj["Enabled"];
                    if (enabled != null && Convert.ToBoolean(enabled))
                    {
                        return true;
                    }
                }
            }
            catch { /* WMI fallback failed */ }
            
            return false;
        }
        catch
        {
            return false;
        }
    }

    private bool CheckAutoUpdateEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update");
            var value = key?.GetValue("AUOptions");
            // 4 = Auto download and schedule install
            return value != null && (int)value >= 3;
        }
        catch
        {
            return false;
        }
    }

    // ==================== Helpers ====================

    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.#} {sizes[order]}";
    }

    private string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
            return $"{(int)uptime.TotalDays}d {uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
        return $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
    }
}
