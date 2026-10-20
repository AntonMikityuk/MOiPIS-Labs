using TaskTracker.ProcessEngine.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TaskTracker.ProcessEngine;

/// <summary>
/// Загрузчик YAML файлов определений классов, пользователей и шаблонов процессов
/// </summary>
public static class YamlLoader
{
    /// <summary>
    /// Создать десериализатор
    /// </summary>
    private static IDeserializer CreateDeserializer() => new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithEnumNamingConvention(LowerCaseNamingConvention.Instance)
        .Build();

    /// <summary>
    /// Загрузить определения классов объектов из YAML
    /// </summary>
    public static ClassesFile LoadClasses(string yaml) => CreateDeserializer().Deserialize<ClassesFile>(yaml);
    
    /// <summary>
    /// Загрузить определения пользователей и ролей из YAML
    /// </summary>
    public static UsersFile LoadUsers(string yaml) => CreateDeserializer().Deserialize<UsersFile>(yaml);
    
    /// <summary>
    /// Загрузить шаблон процесса из YAML
    /// </summary>
    public static ProcessTemplateDefinition LoadTemplate(string yaml) => CreateDeserializer().Deserialize<ProcessTemplateDefinition>(yaml);
}
