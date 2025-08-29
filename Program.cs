using System;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("Collecting device inventory...");
        var snap = Collector.CollectAll();

        // แสดงบนคอนโซล (debug)
        Console.WriteLine(JsonSerializer.Serialize(snap, new JsonSerializerOptions { WriteIndented = true }));

        // บันทึกลง MySQL
        var machineId = await MySqlSaver.SaveSnapshotAsync(snap);
        Console.WriteLine($"\nSaved to MySQL. machine_id = {machineId}");
    }
}
