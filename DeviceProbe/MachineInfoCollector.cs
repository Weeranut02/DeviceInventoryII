using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.IO;

#if WINDOWS
using System.Management; // ต้องอยู่ระดับบนสุดของไฟล์เท่านั้น
#endif

public static class MachineInfoCollector
{
    public static MachineInfo Collect()
    {
        var info = new MachineInfo
        {
            MachineName = Environment.MachineName,
            UserName = Environment.UserName,
            OSDescription = RuntimeInformation.OSDescription,
            OSArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            Framework = RuntimeInformation.FrameworkDescription,
            ProcessorCount = Environment.ProcessorCount,
            Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64)
        };

        // -------- Network (cross-platform)
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                nic.OperationalStatus != OperationalStatus.Up) continue;

            var ni = new NetAdapterInfo { Name = nic.Name, Mac = nic.GetPhysicalAddress().ToString() };
            var ipprops = nic.GetIPProperties();
            foreach (var ua in ipprops.UnicastAddresses)
                if (ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    ni.IPv4.Add(ua.Address.ToString());
            info.NetworkAdapters.Add(ni);
        }

        // -------- Disks (cross-platform: ได้ชื่อไดรฟ์/ขนาด)
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (d.IsReady)
                {
                    info.Disks.Add(new DiskInfo {
                        Model = d.Name,
                        SizeBytes = (ulong)d.TotalSize
                    });
                }
            }
            catch { /* บางไดรฟ์อาจ throw */ }
        }

#if WINDOWS
        // -------- ส่วนลึกบน Windows (WMI)

        // CPU
        using (var s = new ManagementObjectSearcher(
            "SELECT Name,NumberOfCores,NumberOfLogicalProcessors,ProcessorId FROM Win32_Processor"))
        {
            foreach (ManagementObject mo in s.Get())
            {
                info.Cpu = new CpuInfo
                {
                    Name = mo["Name"]?.ToString(),
                    Cores = mo["NumberOfCores"] is uint c ? (int)c : null,
                    Logical = mo["NumberOfLogicalProcessors"] is uint l ? (int)l : null,
                    Id = mo["ProcessorId"]?.ToString()
                };
            }
        }

        // Memory
        using (var s = new ManagementObjectSearcher(
            "SELECT TotalVisibleMemorySize,FreePhysicalMemory FROM Win32_OperatingSystem"))
        {
            foreach (ManagementObject mo in s.Get())
            {
                info.Memory = new MemoryInfo
                {
                    TotalMB = ToULong(mo["TotalVisibleMemorySize"]),
                    FreeMB  = ToULong(mo["FreePhysicalMemory"])
                };
            }
        }

        // Physical Disks
        using (var s = new ManagementObjectSearcher(
            "SELECT Model,SerialNumber,Size,InterfaceType FROM Win32_DiskDrive"))
        {
            foreach (ManagementObject mo in s.Get())
            {
                info.Disks.Add(new DiskInfo
                {
                    Model = mo["Model"]?.ToString(),
                    Serial = mo["SerialNumber"]?.ToString()?.Trim(),
                    SizeBytes = ToULong(mo["Size"]),
                    InterfaceType = mo["InterfaceType"]?.ToString()
                });
            }
        }

        static ulong? ToULong(object? o) => o is null ? null : (ulong?)Convert.ToUInt64(o);
#endif

        return info;
    }
}
