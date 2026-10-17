using YamlDotNet.Serialization;

namespace TaskTracker.ProcessEngine.Models;

/// <summary>
/// Определение параметра класса объекта
/// </summary>
public class ParameterDefinition
{
    /// <summary>Уникальный идентификатор параметра</summary>
    public string Id { get; set; } = "";
    /// <summary>Отображаемое название параметра</summary>
    public string Title { get; set; } = "";
    /// <summary>Тип значения параметра (Integer, Decimal, String)</summary>
    public ParameterType Type { get; set; }
    /// <summary>Минимально допустимое значение (null — без ограничения снизу)</summary>
    public decimal? Min { get; set; }
    /// <summary>Максимально допустимое значение (null — без ограничения сверху)</summary>
    public decimal? Max { get; set; }
}

/// <summary>
/// Определение класса объекта с поддержкой наследования
/// </summary>
public class ObjectClassDefinition
{
    /// <summary>Уникальный идентификатор класса</summary>
    public string Id { get; set; } = "";
    /// <summary>Отображаемое название класса</summary>
    public string Title { get; set; } = "";
    /// <summary>Идентификатор родительского класса (null — корневой класс)</summary>
    public string? Parent { get; set; }
    /// <summary>Собственные параметры класса (могут переопределять min/max родителя)</summary>
    public List<ParameterDefinition> Parameters { get; set; } = new();
}

/// <summary>
/// Файл определений классов объектов (корневой элемент YAML)
/// </summary>
public class ClassesFile
{
    /// <summary>Список всех определений классов</summary>
    public List<ObjectClassDefinition> Classes { get; set; } = new();
}

/// <summary>
/// Определение роли в системе
/// </summary>
public class RoleDefinition
{
    /// <summary>Уникальный идентификатор роли</summary>
    public string Id { get; set; } = "";
    /// <summary>Отображаемое название роли</summary>
    public string Title { get; set; } = "";
}

/// <summary>
/// Определение пользователя системы
/// </summary>
public class UserDefinition
{
    /// <summary>Уникальный идентификатор пользователя</summary>
    public string Id { get; set; } = "";
    /// <summary>Имя пользователя</summary>
    public string Name { get; set; } = "";
    /// <summary>Список идентификаторов ролей пользователя</summary>
    public List<string> Roles { get; set; } = new();
}

/// <summary>
/// Файл определений пользователей и ролей (корневой элемент YAML)
/// </summary>
public class UsersFile
{
    /// <summary>Список всех ролей</summary>
    public List<RoleDefinition> Roles { get; set; } = new();
    /// <summary>Список всех пользователей</summary>
    public List<UserDefinition> Users { get; set; } = new();
}

/// <summary>
/// Определение действия в шаблоне процесса
/// </summary>
public class ActionDefinition
{
    /// <summary>Уникальный идентификатор действия</summary>
    public string Id { get; set; } = "";
    /// <summary>Отображаемое название действия</summary>
    public string Title { get; set; } = "";
}

/// <summary>
/// Определение предиката — условие на параметр или принадлежность к классу
/// </summary>
public class PredicateDefinition
{
    /// <summary>Уникальный идентификатор предиката</summary>
    public string Id { get; set; } = "";
    /// <summary>Идентификатор параметра для проверки (null для предикатов класса)</summary>
    public string? Parameter { get; set; }
    /// <summary>Оператор сравнения: =, !=, &lt;, &lt;=, &gt;, &gt;=, filled, in_range</summary>
    public string? Operator { get; set; }
    /// <summary>Значение для сравнения (null для filled/in_range)</summary>
    public object? Value { get; set; }
    /// <summary>Идентификатор класса для предиката принадлежности (null для предикатов параметра)</summary>
    [YamlMember(Alias = "class")]
    public string? Class { get; set; }
}

/// <summary>
/// Определение состояния в жизненном цикле процесса
/// </summary>
public class StateDefinition
{
    /// <summary>Уникальный идентификатор состояния</summary>
    public string Id { get; set; } = "";
    /// <summary>Отображаемое название состояния</summary>
    public string Title { get; set; } = "";
    /// <summary>Является ли состояние начальным</summary>
    public bool Initial { get; set; }
    /// <summary>Является ли состояние конечным (из него нельзя перейти далее)</summary>
    public bool Final { get; set; }
    /// <summary>Роли, которым разрешены действия в этом состоянии</summary>
    public List<string> Roles { get; set; } = new();
    /// <summary>Разрешено ли редактирование параметров в этом состоянии</summary>
    public bool EditParameters { get; set; }
}

/// <summary>
/// Определение перехода между состояниями
/// </summary>
public class TransitionDefinition
{
    /// <summary>Исходное состояние перехода</summary>
    [YamlMember(Alias = "from")]
    public string From { get; set; } = "";
    /// <summary>Действие, инициирующее переход</summary>
    public string Action { get; set; } = "";
    /// <summary>Целевое состояние перехода</summary>
    public string To { get; set; } = "";
    /// <summary>Условие в ДНФ: внешний список — дизъюнкция (ИЛИ), внутренний — конъюнкция (И) предикатов</summary>
    public List<List<string>>? Condition { get; set; }
}

/// <summary>
/// Шаблон процесса — описывает жизненный цикл объектов определённого класса
/// </summary>
public class ProcessTemplateDefinition
{
    /// <summary>Уникальный идентификатор процесса</summary>
    public string Process { get; set; } = "";
    /// <summary>Отображаемое название процесса</summary>
    public string Title { get; set; } = "";
    /// <summary>Класс объектов, к которому привязан шаблон</summary>
    [YamlMember(Alias = "class")]
    public string Class { get; set; } = "";
    /// <summary>Роли, которым разрешено создавать объекты по этому шаблону</summary>
    public List<string> CreateRoles { get; set; } = new();
    /// <summary>Набор действий, доступных в процессе</summary>
    public List<ActionDefinition> Actions { get; set; } = new();
    /// <summary>Набор предикатов для условий переходов</summary>
    public List<PredicateDefinition> Predicates { get; set; } = new();
    /// <summary>Набор состояний жизненного цикла</summary>
    public List<StateDefinition> States { get; set; } = new();
    /// <summary>Набор переходов между состояниями</summary>
    public List<TransitionDefinition> Transitions { get; set; } = new();
}
