using TaskTracker.ProcessEngine.Models;
using TaskTracker.ProcessEngine.Runtime;

namespace TaskTracker.ProcessEngine.Engine;

/// <summary>
/// Движок процессов: start запускает процесс над объектом, act выполняет действие
/// </summary>
public class ProcessEngine
{
    private readonly ClassRegistry _classes;
    private readonly UserRegistry _users;
    private readonly TemplateRegistry _templates;
    private readonly ObjectRegistry _objects;
    private readonly ConditionEvaluator _evaluator;
    private readonly IAuditLog _log;
    private readonly Dictionary<string, ProcessInstance> _processes = new();

    public ProcessEngine(ClassRegistry classes, UserRegistry users,
        TemplateRegistry templates, ObjectRegistry objects, IAuditLog log)
    {
        _classes = classes;
        _users = users;
        _templates = templates;
        _objects = objects;
        _evaluator = new ConditionEvaluator(classes);
        _log = log;
    }

    /// <summary>
    /// Запустить процесс над существующим объектом
    /// </summary>
    public OperationResult Start(string userId, string objectId)
    {
        if (!_users.Exists(userId))
            return Log("start", userId, objectId, null, "rejected", "Пользователь не найден");

        var obj = _objects.GetObject(objectId);
        if (obj == null)
            return Log("start", userId, objectId, null, "rejected", "Объект не найден");

        if (_processes.ContainsKey(objectId))
        {
            var cur = _processes[objectId].CurrentState;
            return Log("start", userId, objectId, cur, "rejected", "Процесс над объектом уже запущен", cur);
        }

        var template = _templates.GetActingTemplate(obj.Class);
        if (template == null)
            return Log("start", userId, objectId, null, "rejected",
                $"Для класса '{obj.Class}' нет шаблона процесса");

        if (!_users.HasAnyRole(userId, template.CreateRoles))
            return Log("start", userId, objectId, null, "rejected",
                "У пользователя нет роли для запуска процесса");

        var initial = template.States.FirstOrDefault(s => s.Initial);
        if (initial == null)
            return Log("start", userId, objectId, null, "error",
                "В шаблоне нет начального состояния");

        _processes[objectId] = new ProcessInstance
        {
            ObjectId = objectId,
            TemplateClass = obj.Class,
            CurrentState = initial.Id
        };

        return Log("start", userId, objectId, null, "accepted", "Процесс запущен", initial.Id);
    }

    /// <summary>
    /// Выполнить действие над объектом с запущенным процессом
    /// </summary>
    public OperationResult Act(string userId, string objectId, string actionId)
    {
        if (!_users.Exists(userId))
            return Log("action", userId, objectId, null, "rejected", "Пользователь не найден", action: actionId);

        var obj = _objects.GetObject(objectId);
        if (obj == null)
            return Log("action", userId, objectId, null, "rejected", "Объект не найден", action: actionId);

        if (!_processes.TryGetValue(objectId, out var instance))
            return Log("action", userId, objectId, null, "rejected", "Процесс не запущен", action: actionId);

        var template = _templates.GetActingTemplate(instance.TemplateClass)!;
        var state = template.States.FirstOrDefault(s => s.Id == instance.CurrentState);
        if (state == null)
            return Log("action", userId, objectId, instance.CurrentState, "error",
                "Текущее состояние не найдено в шаблоне", action: actionId);

        if (state.Final)
            return Log("action", userId, objectId, instance.CurrentState, "rejected",
                "Процесс завершён", action: actionId);

        if (!_users.HasAnyRole(userId, state.Roles))
            return Log("action", userId, objectId, instance.CurrentState, "rejected",
                $"Требуется роль: {string.Join(" или ", state.Roles)}", action: actionId);

        var candidates = template.Transitions
            .Where(t => t.From == state.Id && t.Action == actionId).ToList();
        if (candidates.Count == 0)
            return Log("action", userId, objectId, instance.CurrentState, "rejected",
                "Действие не предусмотрено в текущем состоянии", action: actionId);

        var map = template.Predicates.ToDictionary(p => p.Id);
        var values = obj.Values ?? new Dictionary<string, object?>();
        var matched = candidates
            .Where(t => _evaluator.EvaluateCondition(t.Condition, map, obj.Class, values))
            .ToList();

        if (matched.Count == 0)
            return Log("action", userId, objectId, instance.CurrentState, "rejected",
                "Условие перехода не выполнено", action: actionId);

        if (matched.Count > 1)
            return Log("action", userId, objectId, instance.CurrentState, "error",
                "Ошибка шаблона: выполнены условия нескольких переходов", action: actionId);

        var oldState = instance.CurrentState;
        instance.CurrentState = matched[0].To;

        return Log("action", userId, objectId, oldState, "accepted",
            $"Переход в состояние '{matched[0].To}'", matched[0].To, actionId);
    }

    public ProcessInstance? GetProcess(string objectId) =>
        _processes.TryGetValue(objectId, out var p) ? p : null;

    private OperationResult Log(string type, string userId, string objectId,
        string? stateBefore, string result, string message,
        string? stateAfter = null, string? action = null)
    {
        stateAfter ??= stateBefore;

        object command = action != null
            ? new { type, user = userId, @object = objectId, action }
            : (object)new { type, user = userId, @object = objectId };

        _log.Write(new AuditLogEntry
        {
            Command = command,
            User = userId,
            Object = objectId,
            StateBefore = stateBefore,
            StateAfter = stateAfter,
            Result = result,
            Reason = result != "accepted" ? message : null
        });

        return result == "accepted"
            ? OperationResult.Ok(message, stateBefore, stateAfter)
            : OperationResult.Fail(message, stateBefore);
    }
}
