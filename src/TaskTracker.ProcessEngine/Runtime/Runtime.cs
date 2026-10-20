namespace TaskTracker.ProcessEngine.Runtime;

/// <summary>
/// Тип операции над объектом процесса
/// </summary>
public enum OperationType
{
    /// <summary>
    /// Создание нового объекта
    /// </summary>
    Create,
    
    /// <summary>
    /// Выполнение действия (переход между состояниями)
    /// </summary>
    Action,
    
    /// <summary>
    /// Изменение значения параметра
    /// </summary>
    ModifyParameter
}

/// <summary>
/// Запись в журнале истории операций над объектом
/// </summary>
public class HistoryEntry
{
    /// <summary>
    /// Время выполнения операции
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// ID пользователя, выполнившего операцию
    /// </summary>
    public string UserId { get; set; } = "";
    
    /// <summary>
    /// Тип выполненной операции
    /// </summary>
    public OperationType Operation { get; set; }
    
    /// <summary>
    /// Описание операции
    /// </summary>
    public string Details { get; set; } = "";
    
    /// <summary>
    /// Новое состояние после операции (null = состояние не изменилось)
    /// </summary>
    public string? NewState { get; set; }
}

/// <summary>
/// Экземпляр объекта процесса с текущим состоянием, параметрами и историей
/// </summary>
public class ProcessObject
{
    /// <summary>
    /// ID объекта
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// ID класса, к которому принадлежит объект
    /// </summary>
    public string ClassId { get; set; } = "";
    
    /// <summary>
    /// ID текущего состояния в жизненном цикле
    /// </summary>
    public string CurrentState { get; set; } = "";
    
    /// <summary>
    /// Текущие значения параметров объекта (ключ = id параметра)
    /// </summary>
    public Dictionary<string, object?> ParameterValues { get; set; } = new();
    
    /// <summary>
    /// Журнал всех операций над объектом
    /// </summary>
    public List<HistoryEntry> History { get; set; } = new();
}

/// <summary>
/// Результат выполнения операции движком процессов
/// </summary>
public class OperationResult
{
    /// <summary>
    /// Признак успешного выполнения операции
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Сообщение о результате или причине ошибки
    /// </summary>
    public string Message { get; init; } = "";
    
    /// <summary>
    /// Объект процесса после операции (null = ошибка)
    /// </summary>
    public ProcessObject? Object { get; init; }

    /// <summary>
    /// Создать успешный результат
    /// </summary>
    public static OperationResult Ok(string message = "OK", ProcessObject? obj = null) =>
        new() { Success = true, Message = message, Object = obj };

    /// <summary>
    /// Создать результат с ошибкой
    /// </summary>
    public static OperationResult Fail(string message) =>
        new() { Success = false, Message = message };
}
