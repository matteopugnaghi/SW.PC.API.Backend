// 💻 IPC Hardware Info Models - System Information for Industrial PC
// Provides hardware, OS, and security information for EU CRA compliance

namespace SW.PC.API.Backend.Models;

/// <summary>
/// Complete IPC system information
/// </summary>
public class IpcSystemInfo
{
    // Operating System
    public OsInfo OperatingSystem { get; set; } = new();
    
    // Hardware
    public CpuInfo Cpu { get; set; } = new();
    public MemoryInfo Memory { get; set; } = new();
    public DiskInfo Disk { get; set; } = new();
    
    // Network
    public NetworkInfo Network { get; set; } = new();
    
    // Runtime
    public RuntimeInfo Runtime { get; set; } = new();
    
    // Security
    public SecurityInfo Security { get; set; } = new();
    
    // Timestamp
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Operating System information
/// </summary>
public class OsInfo
{
    public string Name { get; set; } = "";           // Windows 11
    public string Edition { get; set; } = "";        // IoT Enterprise, Pro, etc.
    public string Version { get; set; } = "";        // 22H2
    public string Build { get; set; } = "";          // 22621.1234
    public string Architecture { get; set; } = "";   // 64-bit
    public DateTime? InstallDate { get; set; }
    public TimeSpan Uptime { get; set; }
    public string UptimeFormatted { get; set; } = "";
}

/// <summary>
/// CPU information
/// </summary>
public class CpuInfo
{
    public string Name { get; set; } = "";           // Intel Core i5-8500T
    public string Manufacturer { get; set; } = "";   // Intel
    public int Cores { get; set; }                   // Physical cores
    public int LogicalProcessors { get; set; }       // Logical processors (threads)
    public double SpeedMHz { get; set; }             // Current speed
    public double MaxSpeedMHz { get; set; }          // Max speed
    public double UsagePercent { get; set; }         // Current total usage %
    public List<CoreUsage> CoreUsages { get; set; } = new(); // Per-core usage
}

/// <summary>
/// Individual CPU core usage
/// </summary>
public class CoreUsage
{
    public int CoreId { get; set; }
    public double UsagePercent { get; set; }
}

/// <summary>
/// Memory (RAM) information
/// </summary>
public class MemoryInfo
{
    public long TotalBytes { get; set; }
    public long AvailableBytes { get; set; }
    public long UsedBytes { get; set; }
    public double UsagePercent { get; set; }
    public string TotalFormatted { get; set; } = "";      // "16 GB"
    public string AvailableFormatted { get; set; } = "";  // "8.2 GB"
    public string UsedFormatted { get; set; } = "";       // "7.8 GB"
}

/// <summary>
/// Disk information
/// </summary>
public class DiskInfo
{
    public string DriveLetter { get; set; } = "C:";
    public string DriveType { get; set; } = "";      // SSD, HDD
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public long UsedBytes { get; set; }
    public double UsagePercent { get; set; }
    public string TotalFormatted { get; set; } = "";
    public string FreeFormatted { get; set; } = "";
    public string UsedFormatted { get; set; } = "";
}

/// <summary>
/// Network information
/// </summary>
public class NetworkInfo
{
    public string Hostname { get; set; } = "";
    public string DomainName { get; set; } = "";
    public List<NetworkAdapter> Adapters { get; set; } = new();
    public string PrimaryIpAddress { get; set; } = "";
    public string PrimaryMacAddress { get; set; } = "";
}

/// <summary>
/// Network adapter details
/// </summary>
public class NetworkAdapter
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public string? SubnetMask { get; set; }
    public string? Gateway { get; set; }
    public string MacAddress { get; set; } = "";
    public string Status { get; set; } = "";         // Up, Down
    public long SpeedMbps { get; set; }
    public string Speed { get; set; } = "";          // Formatted: "1 Gbps", "100 Mbps"
    public string AdapterType { get; set; } = "";    // Ethernet, WiFi, Loopback, etc.
    public bool IsDhcpEnabled { get; set; }
    public string? DnsServers { get; set; }
}

/// <summary>
/// Runtime environment information
/// </summary>
public class RuntimeInfo
{
    public string DotNetVersion { get; set; } = "";
    public string DotNetRuntime { get; set; } = "";
    public string ProcessArchitecture { get; set; } = "";
    public int ProcessId { get; set; }
    public long WorkingSetBytes { get; set; }
    public string WorkingSetFormatted { get; set; } = "";
    public DateTime ProcessStartTime { get; set; }
    public TimeSpan ProcessUptime { get; set; }
    public string ProcessUptimeFormatted { get; set; } = "";
}

/// <summary>
/// Security status information
/// </summary>
public class SecurityInfo
{
    public bool WindowsDefenderEnabled { get; set; }
    public bool RealTimeProtection { get; set; }  // Defender real-time protection
    public bool FirewallEnabled { get; set; }
    public bool AutoUpdateEnabled { get; set; }
    public bool UacEnabled { get; set; }
    public DateTime? LastUpdateCheck { get; set; }
    public string SecurityStatus { get; set; } = "";  // Good, Warning, Critical
    public List<string> Warnings { get; set; } = new();
    
    // Third-party antivirus detection
    public List<AntivirusProduct> InstalledAntivirus { get; set; } = new();
    public bool HasThirdPartyAntivirus { get; set; }
}

/// <summary>
/// Installed antivirus product information
/// </summary>
public class AntivirusProduct
{
    public string Name { get; set; } = "";           // Kaspersky, Norton, ESET, etc.
    public string Publisher { get; set; } = "";
    public string State { get; set; } = "";          // Enabled, Disabled, Outdated
    public bool IsEnabled { get; set; }
    public bool IsUpToDate { get; set; }
    public string ProductType { get; set; } = "";    // Antivirus, Firewall, AntiSpyware
}
