using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArchiTimer;

public sealed class JsonDataStore
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonDataStore(string? baseDirectory = null)
    {
        var directory = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArchiTimer");
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "data.json");
    }

    public AppData Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return new AppData();
            return JsonSerializer.Deserialize<AppData>(File.ReadAllText(_filePath), _options) ?? new AppData();
        }
        catch (JsonException)
        {
            var backup = _filePath + ".broken-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            File.Copy(_filePath, backup, overwrite: true);
            return new AppData();
        }
    }

    public void Save(AppData data)
    {
        var temporaryPath = _filePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(data, _options));
        File.Move(temporaryPath, _filePath, overwrite: true);
    }
}
