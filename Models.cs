using System.Collections.Generic;

public class MachineRecord
{
    public string MachineName { get; set; } = "";
    public string? UserName { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? BiosSerial { get; set; }
    public string? OsCaption { get; set; }
    public string? OsVersion { get; set; }
    public string? OsArch { get; set; }
}

public class MonitorRecord
{
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? Serial { get; set; }
}

public class PrinterRecord
{
    public string? Name { get; set; }
    public string? DriverName { get; set; }
    public string? PortName { get; set; }
    public bool? IsNetwork { get; set; }
    public bool? IsDefault { get; set; }
    public bool? IsShared { get; set; }
    public string? Manufacturer { get; set; }
}

public class ScannerRecord
{
    public string? Name { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? PnpDeviceId { get; set; }
}

public class CardReaderRecord
{
    public string? Name { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? PnpDeviceId { get; set; }
}

public class MachineSnapshot
{
    public MachineRecord Machine { get; set; } = new();
    public List<MonitorRecord> Monitors { get; set; } = new();
    public List<PrinterRecord> Printers { get; set; } = new();
    public List<ScannerRecord> Scanners { get; set; } = new();
    public List<CardReaderRecord> CardReaders { get; set; } = new();
}
