using TaskTracker.ProcessEngine.Models;
using TaskTracker.ProcessEngine.Runtime;

namespace TaskTracker.ProcessEngine.Engine;

/// <summary>
/// Движок процессов — управляет жизненным циклом объектов по шаблонам
/// </summary>
public class ProcessEngine
{
    private readonly ClassRegistry _classes;
    private readonly UserRegistry _users;
    private readonly TemplateRegistry _templates;
    private readonly ConditionEvaluator _evaluator;
    private readonly List<ProcessObject> _objects = new();
    private int _nextId = 1;

    public ProcessEngine(ClassRegistry classes, UserRegistry users, TemplateRegistry templates)
    {
        _classes = classes;
        _users = users;
        _templates = templates;
        _evaluator = new ConditionEvaluator(classes);
    }

    /// <summary>Создать новый объект процесса с проверкой роли, параметров и типов</summary>
    public OperationResult CreateObject(string userId, string classId, Dictionary<string, object?>? initialValues)
    {
        var template = _templates.GetActingTemplate(classId);
        if (template == null) return OperationResult.Fail($"Для класса '{classId}' нет действующего шаблона процесса");
        if (!_users.HasAnyRole(userId, template.CreateRoles))
            return OperationResult.Fail("У пользователя нет роли, позволяющей создавать объект");

        var effective = _classes.GetEffectiveParameters(classId);
        var values = new Dictionary<string, object?>();
        if (initialValues != null)
        {
            foreach (var (key, val) in initialValues)
            {
                var def = effective.FirstOrDefault(p => p.Id == key);
                if (def == null) return OperationResult.Fail($"Параметр '{key}' не определён для класса '{classId}'");
                if (val != null && !ValidateType(def.Type, val))
                    return OperationResult.Fail($"Значение параметра '{key}' не соответствует типу {def.Type}");
                values[key] = val;
            }
        }

        var initial = template.States.FirstOrDefault(s => s.Initial);
        if (initial == null) return OperationResult.Fail("В шаблоне нет начального состояния");

        var obj = new ProcessObject
        {
            Id = _nextId++,
            ClassId = classId,
            CurrentState = initial.Id,
            ParameterValues = values
        };
        obj.History.Add(new HistoryEntry
        {
            Timestamp = DateTime.UtcNow,
            UserId = userId,
            Operation = OperationType.Create,
            Details = $"Создан объект класса '{classId}'",
            NewState = initial.Id
        });
        _objects.Add(obj);
        return OperationResult.Ok("Объект создан", obj);
    }

    /// <summary>Выполнить действие над объектом: проверить роли, найти переход, вычислить условия в ДНФ</summary>
    public OperationResult PerformAction(string userId, int objectId, string actionId)
    {
        var obj = GetObject(objectId);
        if (obj == null) return OperationResult.Fail("Объект не найден");
        var template = _templates.GetActingTemplate(obj.ClassId);
        if (template == null) return OperationResult.Fail("Нет действующего шаблона процесса");
        var state = template.States.FirstOrDefault(s => s.Id == obj.CurrentState);
        if (state == null) return OperationResult.Fail("Текущее состояние не найдено в шаблоне");
        if (state.Final) return OperationResult.Fail("Объект в конечном состоянии");
        if (!_users.HasAnyRole(userId, state.Roles))
            return OperationResult.Fail("У пользователя нет роли для действий в текущем состоянии");

        var candidates = template.Transitions.Where(t => t.From == state.Id && t.Action == actionId).ToList();
        if (candidates.Count == 0) return OperationResult.Fail("Действие не предусмотрено в текущем состоянии");

        var map = template.Predicates.ToDictionary(p => p.Id);
        var matched = candidates.Where(t => _evaluator.EvaluateCondition(t.Condition, map, obj)).ToList();
        if (matched.Count == 0) return OperationResult.Fail("Условие не выполнено");
        if (matched.Count > 1) return OperationResult.Fail("Ошибка шаблона: выполнены условия нескольких переходов");

        var tr = matched[0];
        var old = obj.CurrentState;
        obj.CurrentState = tr.To;
        obj.History.Add(new HistoryEntry
        {
            Timestamp = DateTime.UtcNow,
            UserId = userId,
            Operation = OperationType.Action,
            Details = $"Действие '{actionId}': {old} -> {tr.To}",
            NewState = tr.To
        });
        return OperationResult.Ok($"Переход в состояние '{tr.To}'", obj);
    }

    /// <summary>Изменить значение параметра объекта с проверкой состояния, роли и типа</summary>
    public OperationResult ModifyParameter(string userId, int objectId, string parameterId, object? value)
    {
        var obj = GetObject(objectId);
        if (obj == null) return OperationResult.Fail("Объект не найден");
        var template = _templates.GetActingTemplate(obj.ClassId);
        if (template == null) return OperationResult.Fail("Нет действующего шаблона процесса");
        var state = template.States.FirstOrDefault(s => s.Id == obj.CurrentState);
        if (state == null) return OperationResult.Fail("Текущее состояние не найдено в шаблоне");
        if (state.Final) return OperationResult.Fail("Объект в конечном состоянии");
        if (!state.EditParameters) return OperationResult.Fail("В текущем состоянии редактирование параметров запрещено");
        if (!_users.HasAnyRole(userId, state.Roles))
            return OperationResult.Fail("У пользователя нет роли для изменения параметров в текущем состоянии");

        var def = _classes.GetEffectiveParameters(obj.ClassId).FirstOrDefault(p => p.Id == parameterId);
        if (def == null) return OperationResult.Fail($"Параметр '{parameterId}' не определён для класса '{obj.ClassId}'");
        if (value != null && !ValidateType(def.Type, value))
            return OperationResult.Fail($"Значение не соответствует типу {def.Type}");

        obj.ParameterValues.TryGetValue(parameterId, out var oldValue);
        obj.ParameterValues[parameterId] = value;
        obj.History.Add(new HistoryEntry
        {
            Timestamp = DateTime.UtcNow,
            UserId = userId,
            Operation = OperationType.ModifyParameter,
            Details = $"Параметр '{parameterId}': {oldValue ?? "null"} -> {value ?? "null"}",
            NewState = null
        });
        return OperationResult.Ok("Параметр изменён", obj);
    }

    /// <summary>Получить список доступных действий для пользователя в текущем состоянии объекта</summary>
    public List<ActionDefinition> GetAvailableActions(string userId, int objectId)
    {
        var obj = GetObject(objectId);
        if (obj == null) return new();
        var template = _templates.GetActingTemplate(obj.ClassId);
        var state = template?.States.FirstOrDefault(s => s.Id == obj.CurrentState);
        if (template == null || state == null || state.Final) return new();
        if (!_users.HasAnyRole(userId, state.Roles)) return new();

        var ids = template.Transitions.Where(t => t.From == state.Id).Select(t => t.Action).Distinct();
        return ids.Select(id => template.Actions.FirstOrDefault(a => a.Id == id) ?? new ActionDefinition { Id = id, Title = id }).ToList();
    }

    /// <summary>Найти объект процесса по идентификатору</summary>
    public ProcessObject? GetObject(int objectId) => _objects.FirstOrDefault(o => o.Id == objectId);

    /// <summary>Получить журнал истории операций над объектом</summary>
    public List<HistoryEntry> GetHistory(int objectId) => GetObject(objectId)?.History ?? new();

    /// <summary>Проверить соответствие значения указанному типу параметра</summary>
    internal static bool ValidateType(ParameterType type, object value) => type switch
    {
        ParameterType.Integer => value is int or long or short or byte,
        ParameterType.Decimal => value is int or long or short or byte or float or double or decimal,
        ParameterType.String => value is string,
        _ => false
    };
}
