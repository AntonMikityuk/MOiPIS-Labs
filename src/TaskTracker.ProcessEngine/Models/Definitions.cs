using YamlDotNet.Serialization;

namespace TaskTracker.ProcessEngine.Models;

/// <summary>
/// Определение параметра класса объекта
/// </summary>
public class ParameterDefinition
{
    /// <summary>
    /// ID параметра
    /// </summary>
    public string Id { get; set; } = "";
    
    /// <summary>
    /// Название параметра
    /// </summary>
    public string Title { get; set; } = "";
    
    /// <summary>
    /// Тип значения параметра (Integer, Decimal, String)
    /// </summary>
    public ParameterType Type { get; set; }
    
    /// <summary>
    /// Минимальное допустимое значение (null = без ограничения снизу)
    /// </summary>
    public decimal? Min { get; set; }
    
    /// <summary>
    /// Максимальное допустимое значение (null = без ограничения сверху)
    /// </summary>
    public decimal? Max { get; set; }
}

/// <summary>
/// Определение класса объекта с поддержкой наследования
/// </summary>
public class ObjectClassDefinition
{
    /// <summary>
    /// ID класса
    /// </summary>
    public string Id { get; set; } = "";
    
    /// <summary>
    /// Название класса
    /// </summary>
    public string Title { get; set; } = "";
    
    /// <summary>
    /// ID родительского класса (null = нет родителя)
    /// </summary>
    public string? Parent { get; set; }
    
    /// <summary>
    /// Собственные параметры класса (могут переопределять min/max родителя)
    /// </summary>
    public List<ParameterDefinition> Parameters { get; set; } = new();
}

/// <summary>
/// Файл определений классов объектов (корневой элемент YAML)
/// </summary>
public class ClassesFile
{
    /// <summary>
    /// Список всех определений классов
    /// </summary>
    public List<ObjectClassDefinition> Classes { get; set; } = new();
}

/// <summary>
/// Определение роли в системе
/// </summary>
public class RoleDefinition
{
    /// <summary>
    /// ID роли
    /// </summary>
    public string Id { get; set; } = "";
    
    /// <summary>
    /// Название роли
    /// </summary>
    public string Title { get; set; } = "";
}

/// <summary>
/// Определение пользователя системы
/// </summary>
public class UserDefinition
{
    /// <summary>
    /// ID пользователя
    /// </summary>
    public string Id { get; set; } = "";
    
    /// <summary>
    /// Имя пользователя
    /// </summary>
    public string Name { get; set; } = "";
    
    /// <summary>
    /// Список ID ролей пользователя
    /// </summary>
    public List<string> Roles { get; set; } = new();
}

/// <summary>
/// Файл определений пользователей и ролей (корневой элемент YAML)
/// </summary>
public class UsersFile
{
    /// <summary>
    /// Список всех ролей
    /// </summary>
    public List<RoleDefinition> Roles { get; set; } = new();
    
    /// <summary>
    /// Список всех пользователей
    /// </summary>
    public List<UserDefinition> Users { get; set; } = new();
}

/// <summary>
/// Определение действия в шаблоне процесса
/// </summary>
public class ActionDefinition
{
    /// <summary>
    /// ID действия
    /// </summary>
    public string Id { get; set; } = "";
    
    /// <summary>
    /// Название действия
    /// </summary>
    public string Title { get; set; } = "";
}

/// <summary>
/// Определение предиката — условие на параметр или принадлежность к классу
/// </summary>
public class PredicateDefinition
{
    /// <summary>
    /// ID предиката
    /// </summary>
    public string Id { get; set; } = "";
    
    /// <summary>
    /// ID параметра для проверки (null = предикаты класса)
    /// </summary>
    public string? Parameter { get; set; }
    
    /// <summary>
    /// Оператор сравнения
    /// </summary>
    public string? Operator { get; set; }
    
    /// <summary>
    /// Значение для сравнения (null = filled/in_range)
    /// </summary>
    public object? Value { get; set; }
    
    /// <summary>
    /// ID класса для предиката принадлежности (null = предикаты параметра)
    /// </summary>
    [YamlMember(Alias = "class")]
    public string? Class { get; set; }
}

/// <summary>
/// Определение состояния в жизненном цикле процесса
/// </summary>
public class StateDefinition
{
    /// <summary>
    /// ID состояния
    /// </summary>
    public string Id { get; set; } = "";
    
    /// <summary>
    /// Отображаемое название состояния
    /// </summary>
    public string Title { get; set; } = "";
    
    /// <summary>
    /// Является ли состояние начальным
    /// </summary>
    public bool Initial { get; set; }
    
    /// <summary>
    /// Является ли состояние конечным (из него нельзя перейти далее)
    /// </summary>
    public bool Final { get; set; }
    
    /// <summary>
    /// Роли, которым разрешены действия в этом состоянии
    /// </summary>
    public List<string> Roles { get; set; } = new();
    
    /// <summary>
    /// Разрешено ли редактирование параметров в этом состоянии
    /// </summary>
    public bool EditParameters { get; set; }
}

/// <summary>
/// Определение перехода между состояниями
/// </summary>
public class TransitionDefinition
{
    /// <summary>
    /// Исходное состояние перехода
    /// </summary>
    [YamlMember(Alias = "from")]
    public string From { get; set; } = "";
    
    /// <summary>
    /// Действие, инициирующее переход
    /// </summary>
    public string Action { get; set; } = "";
    
    /// <summary>
    /// Целевое состояние перехода
    /// </summary>
    public string To { get; set; } = "";
    
    /// <summary>
    /// Условие в ДНФ (внешний список = дизъюнкция, внутренний = конъюнкция предикатов
    /// </summary>
    public List<List<string>>? Condition { get; set; }
}

/// <summary>
/// Шаблон процесса (описывает жизненный цикл объектов определённого класса)
/// </summary>
public class ProcessTemplateDefinition
{
    /// <summary>
    /// ID процесса
    /// </summary>
    public string Process { get; set; } = "";
    
    /// <summary>
    /// Название процесса
    /// </summary>
    public string Title { get; set; } = "";
    
    /// <summary>
    /// Класс объектов, к которому привязан шаблон
    /// </summary>
    [YamlMember(Alias = "class")]
    public string Class { get; set; } = "";
    
    /// <summary>
    /// Роли, которым разрешено создавать объекты по этому шаблону
    /// </summary>
    public List<string> CreateRoles { get; set; } = new();
    
    /// <summary>
    /// Набор действий, доступных в процессе
    /// </summary>
    public List<ActionDefinition> Actions { get; set; } = new();
    
    /// <summary>
    /// Набор предикатов для условий переходов
    /// </summary>
    public List<PredicateDefinition> Predicates { get; set; } = new();
    
    /// <summary>
    /// Набор состояний жизненного цикла
    /// </summary>
    public List<StateDefinition> States { get; set; } = new();
    
    /// <summary>
    /// Набор переходов между состояниями
    /// </summary>
    public List<TransitionDefinition> Transitions { get; set; } = new();
}
