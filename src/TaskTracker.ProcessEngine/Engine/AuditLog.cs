using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskTracker.ProcessEngine.Engine;

/// <summary>
/// Запись журнала обработки команд (формат JSON/JSONL)
/// </summary>
public class AuditLogEntry
{
    [JsonPropertyName("command")]
    public object Command { get; set; } = new();

    [JsonPropertyName("user")]
    public string User { get; set; } = "";

    [JsonPropertyName("object")]
    public string Object { get; set; } = "";

    [JsonPropertyName("state_before")]
    public string? StateBefore { get; set; }

    [JsonPropertyName("state_after")]
    public string? StateAfter { get; set; }

    [JsonPropertyName("result")]
    public string Result { get; set; } = "";

    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; set; }
}

/// <summary>
/// Интерфейс журнала — каждая команда (успех, отказ, ошибка) записывается
/// </summary>
public interface IAuditLog
{
    void Write(AuditLogEntry entry);
}

/// <summary>
/// Запись в JSONL-файл (один JSON-объект на строку)
/// </summary>
public class JsonLinesLog : IAuditLog, IDisposable
{
    private readonly StreamWriter _writer;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public JsonLinesLog(StreamWriter writer) => _writer = writer;

    public void Write(AuditLogEntry entry)
    {
        _writer.WriteLine(JsonSerializer.Serialize(entry, JsonOpts));
        _writer.Flush();
    }

    public void Dispose() => _writer.Dispose();
}

/// <summary>
/// In-memory журнал для тестов (позволяет проверять записи без файлового ввода-вывода)
/// </summary>
public class MemoryLog : IAuditLog
{
    public List<AuditLogEntry> Entries { get; } = new();

    public void Write(AuditLogEntry entry) => Entries.Add(entry);
}
