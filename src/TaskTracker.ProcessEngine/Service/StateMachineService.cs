namespace TaskTracker.ProcessEngine.Service;

/// <summary>
/// Сервис-конечный автомат с очередью сообщений
/// Поддерживает динамическую регистрацию состояний, переходов и обработчиков событий
/// </summary>
public class StateMachineService
{
    private readonly HashSet<string> _states = new();
    private string? _currentState;
    private readonly Dictionary<(string From, string Event), string> _transitions = new();
    private readonly Dictionary<string, Action<Message>> _handlers = new();
    private readonly Queue<Message> _queue = new();
    private readonly Action<string>? _log;

    public string? CurrentState => _currentState;
    public int QueueCount => _queue.Count;
    public IReadOnlyCollection<string> States => _states;

    /// <summary>
    /// Хранилище данных сервиса (обработчики могут читать и изменять значения)
    /// Изменения логируются автоматически при обработке сообщений
    /// </summary>
    public Dictionary<string, object?> Data { get; } = new();

    public StateMachineService(Action<string>? log = null)
    {
        _log = log;
    }

    private string FormatStates() => _states.Count == 0 ? "(пусто)" : string.Join(", ", _states);
    private string FormatQueue() => _queue.Count == 0
        ? "(пусто)"
        : string.Join(" -> ", _queue.Select(m => m.Event));
    private string FormatData() => Data.Count == 0
        ? "(пусто)"
        : string.Join(", ", Data.Select(kv => $"{kv.Key}={kv.Value ?? "null"}"));

    /// <summary>
    /// Зарегистрировать новое состояние в автомате
    /// </summary>
    public void AddState(string stateId)
    {
        if (!_states.Add(stateId))
            throw new InvalidOperationException($"Состояние '{stateId}' уже зарегистрировано");
        _log?.Invoke($"[+STATE] Добавлено состояние: '{stateId}' | Все состояния: [{FormatStates()}]");
    }

    /// <summary>
    /// Установить текущее состояние автомата
    /// </summary>
    public void SetInitialState(string stateId)
    {
        if (!_states.Contains(stateId))
            throw new InvalidOperationException($"Состояние '{stateId}' не зарегистрировано");
        var prev = _currentState;
        _currentState = stateId;
        _log?.Invoke(prev == null
            ? $"[INIT] Начальное состояние: '{stateId}' | Все состояния: [{FormatStates()}]"
            : $"[INIT] Состояние сброшено: '{prev}' -> '{stateId}' | Все состояния: [{FormatStates()}]");
    }

    /// <summary>
    /// Добавить переход между состояниями по событию
    /// </summary>
    public void AddTransition(string from, string eventId, string to)
    {
        if (!_states.Contains(from))
            throw new InvalidOperationException($"Исходное состояние '{from}' не зарегистрировано");
        if (!_states.Contains(to))
            throw new InvalidOperationException($"Целевое состояние '{to}' не зарегистрировано");
        _transitions[(from, eventId)] = to;
        _log?.Invoke($"[+TRANSITION] '{from}' --[{eventId}]--> '{to}' | Всего переходов: {_transitions.Count}");
    }

    /// <summary>
    /// Зарегистрировать обработчик для события (заменяет предыдущий, если он был)
    /// </summary>
    public void RegisterHandler(string eventId, Action<Message> handler)
    {
        var isOverride = _handlers.ContainsKey(eventId);
        _handlers[eventId] = handler;
        _log?.Invoke(isOverride
            ? $"[~HANDLER] Обработчик '{eventId}' заменён | Всего обработчиков: {_handlers.Count}"
            : $"[+HANDLER] Зарегистрирован обработчик: '{eventId}' | Всего обработчиков: {_handlers.Count}");
    }

    /// <summary>
    /// Добавить сообщение в очередь
    /// </summary>
    public void Enqueue(Message message)
    {
        _queue.Enqueue(message);
        _log?.Invoke($"[ENQUEUE] event='{message.Event}', data={message.Data ?? "null"} | Очередь ({_queue.Count}): [{FormatQueue()}]");
    }

    /// <summary>
    /// Обработать следующее сообщение из очереди, вызывает обработчик (если есть) и выполняет переход (если задан)
    /// </summary>
    public bool ProcessNext()
    {
        if (_queue.Count == 0)
        {
            _log?.Invoke("[PROCESS] Очередь пуста — нечего обрабатывать");
            return false;
        }

        var msg = _queue.Dequeue();
        _log?.Invoke($"[PROCESS] Извлечено: event='{msg.Event}' | Состояние: '{_currentState}' | Осталось в очереди ({_queue.Count}): [{FormatQueue()}]");

        if (_handlers.TryGetValue(msg.Event, out var handler))
        {
            var dataBefore = FormatData();
            handler(msg);
            var dataAfter = FormatData();
            if (dataBefore != dataAfter)
                _log?.Invoke($"  [HANDLER] Обработчик '{msg.Event}' выполнен | Данные: {{{dataBefore}}} -> {{{dataAfter}}}");
            else
                _log?.Invoke($"  [HANDLER] Обработчик '{msg.Event}' выполнен | Данные не изменились: {{{dataAfter}}}");
        }
        else
        {
            _log?.Invoke($"  [HANDLER] Обработчик для '{msg.Event}' не найден — пропуск");
        }

        if (_currentState != null && _transitions.TryGetValue((_currentState, msg.Event), out var target))
        {
            var prev = _currentState;
            _currentState = target;
            _log?.Invoke($"  [TRANSITION] '{prev}' -> '{target}' | Текущее состояние: '{_currentState}'");
        }
        else
        {
            _log?.Invoke($"  [NO TRANSITION] Переход не задан для ('{_currentState}', '{msg.Event}') | Состояние: '{_currentState}'");
        }

        return true;
    }

    /// <summary>
    /// Обработать все сообщения в очереди последовательно
    /// </summary>
    public int ProcessAll()
    {
        int count = 0;
        _log?.Invoke($"[PROCESS ALL] Начало обработки очереди ({_queue.Count}): [{FormatQueue()}] | Состояние: '{_currentState}'");
        while (ProcessNext())
            count++;
        _log?.Invoke($"[PROCESS ALL] Завершено, обработано: {count} | Итоговое состояние: '{_currentState}'");
        return count;
    }
}
