namespace TaskTracker.ProcessEngine.Runtime;

/// <summary>
/// Экземпляр запущенного процесса (связывает объект с шаблоном и хранит текущее состояние)
/// </summary>
public class ProcessInstance
{
    public string ObjectId { get; set; } = "";
    public string TemplateClass { get; set; } = "";
    public string CurrentState { get; set; } = "";
}

/// <summary>
/// Результат выполнения команды движком процессов
/// </summary>
public class OperationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public string? StateBefore { get; init; }
    public string? StateAfter { get; init; }

    public static OperationResult Ok(string message, string? stateBefore = null, string? stateAfter = null) =>
        new() { Success = true, Message = message, StateBefore = stateBefore, StateAfter = stateAfter };

    public static OperationResult Fail(string message, string? stateBefore = null) =>
        new() { Success = false, Message = message, StateBefore = stateBefore, StateAfter = stateBefore };
}
