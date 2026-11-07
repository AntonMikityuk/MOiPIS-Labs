using TaskTracker.ProcessEngine;
using TaskTracker.ProcessEngine.Engine;
using PEngine = TaskTracker.ProcessEngine.Engine.ProcessEngine;

namespace TaskTracker.ProcessEngine.Tests;

public class ProcessEngineTests
{
    private static (PEngine engine, ClassRegistry classes, ObjectRegistry objects, MemoryLog log)
        SetupEngine(string? templateOverride = null)
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, "TestData");
        var classRegistry = new ClassRegistry();
        classRegistry.Load(YamlLoader.LoadClasses(File.ReadAllText(Path.Combine(basePath, "classes.yaml"))));
        var userRegistry = new UserRegistry();
        userRegistry.Load(YamlLoader.LoadUsers(File.ReadAllText(Path.Combine(basePath, "users.yaml"))));
        var objectRegistry = new ObjectRegistry();
        objectRegistry.Load(YamlLoader.LoadObjects(File.ReadAllText(Path.Combine(basePath, "objects.yaml"))), classRegistry);
        var templateRegistry = new TemplateRegistry();
        var templateFile = templateOverride ?? "task_lifecycle.yaml";
        templateRegistry.Load(YamlLoader.LoadTemplate(File.ReadAllText(Path.Combine(basePath, templateFile))));
        var log = new MemoryLog();
        var engine = new PEngine(classRegistry, userRegistry, templateRegistry, objectRegistry, log);
        return (engine, classRegistry, objectRegistry, log);
    }

    private static void Do(PEngine e, string user, string objectId, string action)
    {
        var r = e.Act(user, objectId, action);
        Assert.True(r.Success, $"{action} by {user} on {objectId}: {r.Message}");
    }

    private static void ToReview(PEngine e, string objectId)
    {
        Assert.True(e.Start("anna", objectId).Success);
        Do(e, "anna", objectId, "analyze");
        Do(e, "anna", objectId, "start_dev");
        Do(e, "ivan", objectId, "submit_review");
    }

    /// <summary>
    /// Полный путь быстрого трека: приоритет высокий + малая задача → сразу done, минуя тестирование
    /// task-1: priority=2, estimated_hours=3
    /// </summary>
    [Fact]
    public void FullPath_FastTrack_ToDone()
    {
        var (e, _, _, log) = SetupEngine();
        ToReview(e, "task-1");
        Do(e, "ivan", "task-1", "approve");
        Assert.Equal("done", e.GetProcess("task-1")!.CurrentState);
        Assert.Equal(5, log.Entries.Count);
        Assert.True(log.Entries.All(entry => entry.Result == "accepted"));
    }

    /// <summary>
    /// Полный путь обычного трека: приоритет 3, оценка 10ч → через тестирование
    /// task-normal: priority=3, estimated_hours=10
    /// </summary>
    [Fact]
    public void FullPath_NormalRoute_ThroughTesting()
    {
        var (e, _, _, _) = SetupEngine();
        ToReview(e, "task-normal");
        Do(e, "ivan", "task-normal", "approve");
        Assert.Equal("testing", e.GetProcess("task-normal")!.CurrentState);
        Do(e, "maria", "task-normal", "pass_test");
        Assert.Equal("done", e.GetProcess("task-normal")!.CurrentState);
    }

    /// <summary>
    /// Возврат с ревью на доработку и повторная отправка
    /// </summary>
    [Fact]
    public void ReturnFromReview_BackToDevelopment()
    {
        var (e, _, _, _) = SetupEngine();
        ToReview(e, "task-1");
        Do(e, "ivan", "task-1", "reject");
        Assert.Equal("development", e.GetProcess("task-1")!.CurrentState);
        Do(e, "ivan", "task-1", "submit_review");
        Assert.Equal("review", e.GetProcess("task-1")!.CurrentState);
    }

    /// <summary>
    /// Отказ пользователю без нужной роли (тестировщик пытается выполнить действие аналитика)
    /// </summary>
    [Fact]
    public void ActionWithoutRole_Rejected()
    {
        var (e, _, _, _) = SetupEngine();
        e.Start("anna", "task-1");
        var r = e.Act("maria", "task-1", "analyze");
        Assert.False(r.Success);
        Assert.Contains("роль", r.Message);
        Assert.Equal("backlog", e.GetProcess("task-1")!.CurrentState);
    }

    /// <summary>
    /// Отказ при невыполненном условии перехода: нет описания → start_dev запрещён
    /// </summary>
    [Fact]
    public void ConditionNotMet_Rejected()
    {
        var (e, _, _, _) = SetupEngine();
        e.Start("anna", "task-no-desc");
        Do(e, "anna", "task-no-desc", "analyze");
        var r = e.Act("anna", "task-no-desc", "start_dev");
        Assert.False(r.Success);
        Assert.Contains("Условие", r.Message);
        Assert.Equal("analysis", e.GetProcess("task-no-desc")!.CurrentState);
    }

    /// <summary>
    /// Наследование: баг переопределяет диапазон priority (max=3 вместо max=5 у task)
    /// </summary>
    [Fact]
    public void InheritanceOverridesParameterRange()
    {
        var (_, classes, _, _) = SetupEngine();
        var taskParams = classes.GetEffectiveParameters("task");
        var bugParams = classes.GetEffectiveParameters("bug");
        var taskPriority = taskParams.First(p => p.Id == "priority");
        var bugPriority = bugParams.First(p => p.Id == "priority");
        Assert.Equal(5m, taskPriority.Max);
        Assert.Equal(3m, bugPriority.Max);
        Assert.Equal(taskPriority.Min, bugPriority.Min);
    }

    /// <summary>
    /// Наследование: фича наследует параметры task и добавляет story_points
    /// </summary>
    [Fact]
    public void FeatureInheritsAndAddsParams()
    {
        var (_, classes, _, _) = SetupEngine();
        var featureParams = classes.GetEffectiveParameters("feature").Select(p => p.Id).ToList();
        foreach (var p in new[] { "title", "description", "priority", "estimated_hours", "story_points" })
            Assert.Contains(p, featureParams);
    }

    /// <summary>
    /// Значения по умолчанию подставляются при отсутствии поля в объекте:
    /// task-minimal не задаёт priority и estimated_hours → получает default (3 и 8)
    /// </summary>
    [Fact]
    public void DefaultValuesApplied()
    {
        var (_, _, objects, _) = SetupEngine();
        var obj = objects.GetObject("task-minimal")!;
        Assert.Equal(3, obj.Values!["priority"]);
        Assert.Equal(8m, obj.Values!["estimated_hours"]);
        Assert.False(obj.Values!.ContainsKey("title"));
    }

    /// <summary>
    /// Явный null не заменяется default: priority: null при default: 3 → остаётся null
    /// </summary>
    [Fact]
    public void ExplicitNull_NotReplacedByDefault()
    {
        var (_, _, objects, _) = SetupEngine();
        var obj = objects.GetObject("task-explicit-null")!;
        Assert.Null(obj.Values!["priority"]);
        Assert.Equal(4m, obj.Values!["estimated_hours"]);
    }

    /// <summary>
    /// Лог содержит записи успешных команд и отказов с корректными полями
    /// </summary>
    [Fact]
    public void LogRecordsSuccessesAndRejections()
    {
        var (e, _, _, log) = SetupEngine();
        e.Start("anna", "task-1");
        e.Act("anna", "task-1", "analyze");
        e.Act("maria", "task-1", "analyze");

        Assert.Equal(3, log.Entries.Count);
        var start = log.Entries[0];
        Assert.Equal("accepted", start.Result);
        Assert.Equal("anna", start.User);
        Assert.Equal("task-1", start.Object);
        Assert.Null(start.StateBefore);
        Assert.Equal("backlog", start.StateAfter);
        Assert.Null(start.Reason);

        var success = log.Entries[1];
        Assert.Equal("accepted", success.Result);
        Assert.Equal("backlog", success.StateBefore);
        Assert.Equal("analysis", success.StateAfter);

        var reject = log.Entries[2];
        Assert.Equal("rejected", reject.Result);
        Assert.Equal("analysis", reject.StateBefore);
        Assert.Equal("analysis", reject.StateAfter);
        Assert.NotNull(reject.Reason);
    }

    /// <summary>
    /// Отказ не меняет состояние процесса
    /// </summary>
    [Fact]
    public void RejectionDoesNotChangeState()
    {
        var (e, _, _, _) = SetupEngine();
        e.Start("anna", "task-no-desc");
        Do(e, "anna", "task-no-desc", "analyze");
        Assert.Equal("analysis", e.GetProcess("task-no-desc")!.CurrentState);
        e.Act("anna", "task-no-desc", "start_dev");
        Assert.Equal("analysis", e.GetProcess("task-no-desc")!.CurrentState);
    }

    /// <summary>
    /// Повторный запуск процесса над тем же объектом отклоняется
    /// </summary>
    [Fact]
    public void DuplicateStart_Rejected()
    {
        var (e, _, _, _) = SetupEngine();
        Assert.True(e.Start("anna", "task-1").Success);
        var r = e.Start("anna", "task-1");
        Assert.False(r.Success);
        Assert.Contains("уже запущен", r.Message);
    }

    /// <summary>
    /// Действие в конечном состоянии отклоняется
    /// </summary>
    [Fact]
    public void ActionInFinalState_Rejected()
    {
        var (e, _, _, _) = SetupEngine();
        e.Start("anna", "task-1");
        Do(e, "anna", "task-1", "cancel");
        Assert.Equal("cancelled", e.GetProcess("task-1")!.CurrentState);
        var r = e.Act("anna", "task-1", "analyze");
        Assert.False(r.Success);
        Assert.Contains("завершён", r.Message);
    }

    /// <summary>
    /// Запуск процесса пользователем без роли create_roles отклоняется
    /// </summary>
    [Fact]
    public void StartWithoutRole_Rejected()
    {
        var (e, _, _, _) = SetupEngine();
        var r = e.Start("ivan", "task-1");
        Assert.False(r.Success);
        Assert.Contains("роли", r.Message);
    }

    /// <summary>
    /// ДНФ: обе группы второго условия — OR между [not high_priority] и [not small_task]
    /// task-normal (priority=3, hours=10): оба дизъюнкта истинны → переход в testing
    /// </summary>
    [Fact]
    public void DnfOrBranches_BothGroupsTrue()
    {
        var (e, _, _, _) = SetupEngine();
        ToReview(e, "task-normal");
        Do(e, "ivan", "task-normal", "approve");
        Assert.Equal("testing", e.GetProcess("task-normal")!.CurrentState);
    }

    /// <summary>
    /// Неоднозначные переходы: условия обоих переходов истинны → ошибка шаблона
    /// task-1 имеет title и priority в диапазоне → оба перехода ambiguous_template матчатся
    /// </summary>
    [Fact]
    public void AmbiguousTransitions_Error()
    {
        var (e, _, _, _) = SetupEngine("ambiguous_template.yaml");
        e.Start("anna", "task-1");
        var r = e.Act("anna", "task-1", "proceed");
        Assert.False(r.Success);
        Assert.Contains("шаблона", r.Message);
    }

    /// <summary>
    /// Класс IsOfClass: баг является подклассом task
    /// </summary>
    [Fact]
    public void ClassPredicate_BugIsOfClassTask()
    {
        var (_, classes, _, _) = SetupEngine();
        Assert.True(classes.IsOfClass("bug", "task"));
        Assert.True(classes.IsOfClass("feature", "task"));
        Assert.False(classes.IsOfClass("task", "bug"));
    }

    /// <summary>
    /// Запуск процесса для объекта без шаблона (bug-1 — нет шаблона для класса bug)
    /// </summary>
    [Fact]
    public void StartWithoutTemplate_Rejected()
    {
        var (e, _, _, _) = SetupEngine();
        var r = e.Start("anna", "bug-1");
        Assert.False(r.Success);
        Assert.Contains("нет шаблона", r.Message);
    }

    /// <summary>
    /// Запуск процесса для несуществующего объекта отклоняется
    /// </summary>
    [Fact]
    public void StartNonExistentObject_Rejected()
    {
        var (e, _, _, _) = SetupEngine();
        var r = e.Start("anna", "nonexistent");
        Assert.False(r.Success);
        Assert.Contains("не найден", r.Message);
    }
}
