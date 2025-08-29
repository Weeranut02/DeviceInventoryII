using System.Text.Json;

var info = MachineInfoCollector.Collect();

var json = JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
var file = $"machineinfo_{DateTime.Now:yyyyMMdd_HHmmss}.json";
await File.WriteAllTextAsync(file, json);

Console.WriteLine($"Saved -> {file}");
Console.WriteLine(json);
