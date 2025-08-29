public class MachineInfo
{
    public string MachineName { get; set; } = "";
    public string UserName { get; set; } = "";
    public string OSDescription { get; set; } = "";
    public string OSArchitecture { get; set; } = "";
    public string Framework { get; set; } = "";
    public int ProcessorCount { get; set; }
    public TimeSpan Uptime { get; set; }
    public CpuInfo Cpu { get; set; } = new();
    public MemoryInfo Memory { get; set; } = new();
    public List<DiskInfo> Disks { get; set; } = new();
    public List<NetAdapterInfo> NetworkAdapters { get; set; } = new();
}

public class CpuInfo { public string? Name { get; set; } public int? Cores { get; set; } public int? Logical { get; set; } public string? Id { get; set; } }
public class MemoryInfo { public ulong? TotalMB { get; set; } public ulong? FreeMB { get; set; } }
public class DiskInfo { public string? Model { get; set; } public string? Serial { get; set; } public ulong? SizeBytes { get; set; } public string? InterfaceType { get; set; } }
public class NetAdapterInfo { public string? Name { get; set; } public string? Mac { get; set; } public List<string> IPv4 { get; set; } = new(); }
