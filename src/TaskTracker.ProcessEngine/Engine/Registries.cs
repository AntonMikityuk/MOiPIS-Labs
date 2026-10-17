using TaskTracker.ProcessEngine.Models;

namespace TaskTracker.ProcessEngine.Engine;

/// <summary>
/// Реестр классов объектов с поддержкой наследования и вычисления эффективных параметров
/// </summary>
public class ClassRegistry
{
    private readonly Dictionary<string, ObjectClassDefinition> _classes = new();

    /// <summary>Загрузить определения классов из файла</summary>
    public void Load(ClassesFile file)
    {
        foreach (var c in file.Classes) _classes[c.Id] = c;
    }

    /// <summary>Получить определение класса по идентификатору</summary>
    public ObjectClassDefinition GetClass(string classId) =>
        _classes.TryGetValue(classId, out var c) ? c : throw new KeyNotFoundException($"Класс '{classId}' не найден");

    /// <summary>Проверить существование класса в реестре</summary>
    public bool Exists(string classId) => _classes.ContainsKey(classId);

    /// <summary>Построить цепочку наследования от данного класса до корневого</summary>
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

    /// <summary>Проверить, принадлежит ли объект классу или его потомку</summary>
    public bool IsOfClass(string objectClassId, string classId) =>
        Exists(objectClassId) && GetInheritanceChain(objectClassId).Contains(classId);

    /// <summary>Вычислить эффективные параметры класса с учётом наследования (потомок переопределяет min/max)</summary>
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
                    // Тип наследуется от родителя, min/max переопределяются потомком
                    result[idx] = new ParameterDefinition
                    {
                        Id = p.Id,
                        Title = p.Title,
                        Type = result[idx].Type,
                        Min = p.Min,
                        Max = p.Max
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

    /// <summary>Загрузить определения пользователей из файла</summary>
    public void Load(UsersFile file)
    {
        foreach (var u in file.Users) _users[u.Id] = u;
    }

    /// <summary>Получить определение пользователя по идентификатору</summary>
    public UserDefinition GetUser(string userId) =>
        _users.TryGetValue(userId, out var u) ? u : throw new KeyNotFoundException($"Пользователь '{userId}' не найден");

    /// <summary>Проверить, есть ли у пользователя хотя бы одна из указанных ролей</summary>
    public bool HasAnyRole(string userId, List<string> roleIds) =>
        _users.TryGetValue(userId, out var u) && u.Roles.Any(roleIds.Contains);
}

/// <summary>
/// Реестр шаблонов процессов с поиском действующего шаблона вверх по иерархии классов
/// </summary>
public class TemplateRegistry
{
    private readonly ClassRegistry _classes;
    private readonly Dictionary<string, ProcessTemplateDefinition> _byClass = new();

    public TemplateRegistry(ClassRegistry classes) => _classes = classes;

    /// <summary>Зарегистрировать шаблон процесса для его класса</summary>
    public void Load(ProcessTemplateDefinition template) => _byClass[template.Class] = template;

    /// <summary>Найти действующий шаблон: ближайший вверх по цепочке наследования классов</summary>
    public ProcessTemplateDefinition? GetActingTemplate(string classId)
    {
        if (!_classes.Exists(classId)) return null;
        foreach (var id in _classes.GetInheritanceChain(classId))
            if (_byClass.TryGetValue(id, out var t)) return t;
        return null;
    }
}
