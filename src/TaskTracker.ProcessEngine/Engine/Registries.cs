using System.Globalization;
using TaskTracker.ProcessEngine.Models;

namespace TaskTracker.ProcessEngine.Engine;

/// <summary>
/// Реестр классов объектов с поддержкой наследования и вычисления эффективных параметров
/// </summary>
public class ClassRegistry
{
    private readonly Dictionary<string, ObjectClassDefinition> _classes = new();

    public void Load(ClassesFile file)
    {
        foreach (var c in file.Classes)
            _classes[c.Id] = c;
    }

    public ObjectClassDefinition GetClass(string classId) =>
        _classes.TryGetValue(classId, out var c)
            ? c
            : throw new KeyNotFoundException($"Класс '{classId}' не найден");

    public bool Exists(string classId) => _classes.ContainsKey(classId);

    public List<string> GetInheritanceChain(string classId)
    {
        var chain = new List<string>();
        string? cur = classId;
        while (cur != null)
        {
            if (chain.Contains(cur)) throw new InvalidOperationException($"Циклическое наследование: {cur}");
            chain.Add(cur);
            cur = GetClass(cur).Parent;
        }
        return chain;
    }

    public bool IsOfClass(string objectClassId, string classId) =>
        Exists(objectClassId) && GetInheritanceChain(objectClassId).Contains(classId);

    /// <summary>
    /// Вычислить эффективные параметры класса с учётом наследования
    /// </summary>
    public List<ParameterDefinition> GetEffectiveParameters(string classId)
    {
        var result = new List<ParameterDefinition>();
        foreach (var id in Enumerable.Reverse(GetInheritanceChain(classId)))
        {
            foreach (var p in GetClass(id).Parameters)
            {
                var idx = result.FindIndex(x => x.Id == p.Id);
                if (idx >= 0)
                {
                    result[idx] = new ParameterDefinition
                    {
                        Id = p.Id,
                        Title = p.Title,
                        Type = result[idx].Type,
                        Min = p.Min,
                        Max = p.Max,
                        Default = p.Default
                    };
                }
                else result.Add(p);
            }
        }
        return result;
    }
}

/// <summary>
/// Реестр пользователей и проверка их ролей
/// </summary>
public class UserRegistry
{
    private readonly Dictionary<string, UserDefinition> _users = new();

    public void Load(UsersFile file)
    {
        foreach (var u in file.Users) _users[u.Id] = u;
    }

    public UserDefinition GetUser(string userId) =>
        _users.TryGetValue(userId, out var u)
            ? u
            : throw new KeyNotFoundException($"Пользователь '{userId}' не найден");

    public bool HasAnyRole(string userId, List<string> roleIds) =>
        _users.TryGetValue(userId, out var u) && u.Roles.Any(roleIds.Contains);

    public bool Exists(string userId) => _users.ContainsKey(userId);
}

/// <summary>
/// Реестр шаблонов процессов (привязка строго к указанному классу, без наследования)
/// </summary>
public class TemplateRegistry
{
    private readonly Dictionary<string, ProcessTemplateDefinition> _byClass = new();

    public TemplateRegistry() { }

    public void Load(ProcessTemplateDefinition template) => _byClass[template.Class] = template;

    /// <summary>
    /// Найти шаблон строго по классу объекта (шаблоны не наследуются)
    /// </summary>
    public ProcessTemplateDefinition? GetActingTemplate(string classId) =>
        _byClass.TryGetValue(classId, out var t) ? t : null;
}

/// <summary>
/// Реестр готовых объектов из конфигурации с подстановкой значений по умолчанию
/// </summary>
public class ObjectRegistry
{
    private readonly Dictionary<string, ObjectDefinition> _objects = new();

    /// <summary>
    /// Загрузить объекты и применить default-значения из описания классов
    /// </summary>
    public void Load(ObjectsFile file, ClassRegistry classes)
    {
        foreach (var raw in file.Objects)
        {
            var effective = classes.GetEffectiveParameters(raw.Class);
            var values = new Dictionary<string, object?>();

            foreach (var param in effective)
            {
                object? rawVal;
                if (raw.Values != null && raw.Values.ContainsKey(param.Id))
                    rawVal = raw.Values[param.Id];
                else if (param.Default != null)
                    rawVal = param.Default;
                else
                    continue;

                values[param.Id] = CoerceValue(rawVal, param.Type);
            }

            _objects[raw.Id] = new ObjectDefinition
            {
                Id = raw.Id,
                Class = raw.Class,
                Values = values
            };
        }
    }

    private static object? CoerceValue(object? value, ParameterType type)
    {
        if (value == null) return null;
        return type switch
        {
            ParameterType.Integer => Convert.ToInt32(value, CultureInfo.InvariantCulture),
            ParameterType.Decimal => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
            ParameterType.String => Convert.ToString(value, CultureInfo.InvariantCulture),
            _ => value
        };
    }

    public ObjectDefinition? GetObject(string objectId) =>
        _objects.TryGetValue(objectId, out var obj) ? obj : null;

    public bool Exists(string objectId) => _objects.ContainsKey(objectId);
}
