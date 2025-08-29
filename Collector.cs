using System;
using System.Linq;
using System.Management;  // WMI
using System.Text;

public static class Collector
{
    public static MachineSnapshot CollectAll()
    {
        var snap = new MachineSnapshot();
        snap.Machine = CollectMachine();
        snap.Monitors = CollectMonitors();
        snap.Printers = CollectPrinters();
        snap.Scanners = CollectScanners();
        snap.CardReaders = CollectCardReaders();
        return snap;
    }

    // ---------- 1) Machine / OS / BIOS ----------
    private static MachineRecord CollectMachine()
    {
        var rec = new MachineRecord
        {
            MachineName = Environment.MachineName,
            UserName = Environment.UserName
        };

        // Manufacturer/Model
        using (var s = new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem"))
        foreach (ManagementObject mo in s.Get())
        {
            rec.Manufacturer = mo["Manufacturer"]?.ToString()?.Trim();
            rec.Model = mo["Model"]?.ToString()?.Trim();
            break;
        }

        // BIOS Serial
        using (var s = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS"))
        foreach (ManagementObject mo in s.Get())
        {
            rec.BiosSerial = mo["SerialNumber"]?.ToString()?.Trim();
            break;
        }

        // OS
        using (var s = new ManagementObjectSearcher("SELECT Caption, Version, OSArchitecture FROM Win32_OperatingSystem"))
        foreach (ManagementObject mo in s.Get())
        {
            rec.OsCaption = mo["Caption"]?.ToString()?.Trim();
            rec.OsVersion = mo["Version"]?.ToString()?.Trim();
            rec.OsArch    = mo["OSArchitecture"]?.ToString()?.Trim(); // ex: "64-bit"
            break;
        }

        return rec;
    }

    // ---------- 2) Monitors (WmiMonitorID in root\wmi) ----------
    private static System.Collections.Generic.List<MonitorRecord> CollectMonitors()
    {
        var list = new System.Collections.Generic.List<MonitorRecord>();
        try
        {
            var scope = new ManagementScope(@"\\.\root\wmi");
            scope.Connect();
            using var s = new ManagementObjectSearcher(scope,
                new ObjectQuery("SELECT * FROM WmiMonitorID WHERE Active = True"));
            foreach (ManagementObject mo in s.Get())
            {
                string? mfg = TryDecodeUShortArray(mo["ManufacturerName"]);
                string? model = TryDecodeUShortArray(mo["UserFriendlyName"]);
                string? serial = TryDecodeUShortArray(mo["SerialNumberID"]);
                list.Add(new MonitorRecord { Manufacturer = mfg, Model = model, Serial = serial });
            }
        }
        catch { /* บางเครื่องอาจปิด WMI นี้ไว้ */ }
        return list;
    }

    private static string? TryDecodeUShortArray(object? value)
    {
        try
        {
            if (value is ushort[] arr)
            {
                var chars = arr.TakeWhile(u => u != 0).Select(u => (char)u).ToArray();
                return new string(chars).Trim();
            }
        } catch {}
        return null;
    }

    // ---------- 3) Printers ----------
    private static System.Collections.Generic.List<PrinterRecord> CollectPrinters()
    {
        var list = new System.Collections.Generic.List<PrinterRecord>();
        try
        {
            using var s = new ManagementObjectSearcher(
                "SELECT Name, DriverName, PortName, Network, Default, Shared FROM Win32_Printer");
            foreach (ManagementObject mo in s.Get())
            {
                var name = mo["Name"]?.ToString();
                var driver = mo["DriverName"]?.ToString();
                var port = mo["PortName"]?.ToString();
                bool? network = mo["Network"] as bool?;
                bool? isDefault = mo["Default"] as bool?;
                bool? shared = mo["Shared"] as bool?;

                // Manufacturer บางครั้งอยู่ใน DriverName (เช่น "HP Universal Printing ...")
                string? mfg = GuessVendorFromText(driver ?? name);

                list.Add(new PrinterRecord {
                    Name = name,
                    DriverName = driver,
                    PortName = port,
                    IsNetwork = network,
                    IsDefault = isDefault,
                    IsShared = shared,
                    Manufacturer = mfg
                });
            }
        }
        catch {}
        return list;
    }

    private static string? GuessVendorFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.ToLowerInvariant();
        string[] vendors = { "hp", "canon", "epson", "brother", "ricoh", "lexmark", "samsung", "kyocera", "xerox" };
        foreach (var v in vendors)
            if (t.Contains(v)) return v.ToUpperInvariant();
        return null;
    }

    // ---------- 4) Scanners (Imaging devices via PnP) ----------
    private static System.Collections.Generic.List<ScannerRecord> CollectScanners()
    {
        var list = new System.Collections.Generic.List<ScannerRecord>();
        try
        {
            // PNPClass 'Imaging' หรือ GUID ของ Imaging devices
            using var s = new ManagementObjectSearcher(
                "SELECT Name, Manufacturer, PNPDeviceID FROM Win32_PnPEntity " +
                "WHERE PNPClass='Imaging' OR PNPClass='Image' " +
                "   OR ClassGuid='{6bdd1fc6-810f-11d0-bec7-08002be2092f}'");
            foreach (ManagementObject mo in s.Get())
            {
                var name = mo["Name"]?.ToString();
                var mfg  = mo["Manufacturer"]?.ToString();
                var pnp  = mo["PNPDeviceID"]?.ToString();

                // เดา Model จาก Name (เพราะหลายครั้ง WMI ไม่มี property 'Model')
                var model = ExtractModelFromName(name);

                list.Add(new ScannerRecord {
                    Name = name, Manufacturer = mfg, Model = model, PnpDeviceId = pnp
                });
            }
        }
        catch {}
        return list;
    }

    // ---------- 5) Smart Card Readers ----------
    private static System.Collections.Generic.List<CardReaderRecord> CollectCardReaders()
    {
        var list = new System.Collections.Generic.List<CardReaderRecord>();
        try
        {
            using var s = new ManagementObjectSearcher(
                "SELECT Name, Manufacturer, PNPDeviceID FROM Win32_PnPEntity " +
                "WHERE PNPClass='SmartCardReader' " +
                "   OR ClassGuid='{50dd5230-ba8a-11d1-bf5d-0000f805f530}'");
            foreach (ManagementObject mo in s.Get())
            {
                var name = mo["Name"]?.ToString();
                var mfg  = mo["Manufacturer"]?.ToString();
                var pnp  = mo["PNPDeviceID"]?.ToString();
                var model = ExtractModelFromName(name);

                list.Add(new CardReaderRecord {
                    Name = name, Manufacturer = mfg, Model = model, PnpDeviceId = pnp
                });
            }
        }
        catch {}
        return list;
    }

    private static string? ExtractModelFromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        // ดึงคำที่ดูเหมือน 'รุ่น' แบบง่าย ๆ: ตัดวงเล็บ/Driver suffix ออก
        var cleaned = name.Replace("WIA", "", StringComparison.OrdinalIgnoreCase)
                          .Replace("USB", "", StringComparison.OrdinalIgnoreCase)
                          .Replace("Device", "", StringComparison.OrdinalIgnoreCase)
                          .Replace("Scanner", "", StringComparison.OrdinalIgnoreCase)
                          .Replace("Smart Card Reader", "", StringComparison.OrdinalIgnoreCase)
                          .Trim();
        return cleaned;
    }
}
