using TaskTracker.ProcessEngine;
using TaskTracker.ProcessEngine.Engine;
using TaskTracker.ProcessEngine.Runtime;
using PEngine = TaskTracker.ProcessEngine.Engine.ProcessEngine;

namespace TaskTracker.ProcessEngine.Tests;

public class TestFixture
{
    public static (PEngine engine, ClassRegistry classes, UserRegistry users, TemplateRegistry templates)
        SetupEngine(string? templateOverride = null)
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, "TestData");
        var classRegistry = new ClassRegistry();
        classRegistry.Load(YamlLoader.LoadClasses(File.ReadAllText(Path.Combine(basePath, "classes.yaml"))));
        var userRegistry = new UserRegistry();
        userRegistry.Load(YamlLoader.LoadUsers(File.ReadAllText(Path.Combine(basePath, "users.yaml"))));
        var templateRegistry = new TemplateRegistry(classRegistry);
        var templateFile = templateOverride ?? "task_lifecycle.yaml";
        templateRegistry.Load(YamlLoader.LoadTemplate(File.ReadAllText(Path.Combine(basePath, templateFile))));
        var engine = new PEngine(classRegistry, userRegistry, templateRegistry);
        return (engine, classRegistry, userRegistry, templateRegistry);
    }
}

public class ProcessEngineTests
{
    private static Dictionary<string, object?> Full(int priority, object hours) => new()
    {
        ["title"] = "T",
        ["description"] = "D",
        ["priority"] = priority,
        ["estimated_hours"] = hours
    };

    private static void Do(PEngine e, string user, int id, string action)
    {
        var r = e.PerformAction(user, id, action);
        Assert.True(r.Success, $"{action} by {user}: {r.Message}");
    }

    private static int Create(PEngine e, string cls, Dictionary<string, object?>? vals, string user = "anna")
    {
        var r = e.CreateObject(user, cls, vals);
        Assert.True(r.Success, r.Message);
        return r.Object!.Id;
    }

    private static int ToReview(PEngine e, string cls, Dictionary<string, object?> vals)
    {
        var id = Create(e, cls, vals);
        Do(e, "anna", id, "analyze");
        Do(e, "anna", id, "start_dev");
        Do(e, "ivan", id, "submit_review");
        return id;
    }

    [Fact]
    public void BugFastTrack_FullLifecycle()
    {
        var (e, _, _, _) = TestFixture.SetupEngine();
        var id = Create(e, "bug", new()
        {
            ["title"] = "NPE", ["description"] = "crash", ["priority"] = 1,
            ["estimated_hours"] = 4, ["severity"] = 3
        });
        Do(e, "anna", id, "analyze");
        Do(e, "anna", id, "start_dev");
        Do(e, "ivan", id, "submit_review");
        Do(e, "ivan", id, "approve");
        Assert.Equal("done", e.GetObject(id)!.CurrentState);
        Assert.Equal(5, e.GetHistory(id).Count);
    }

    [Fact]
    public void Feature_NormalPath_FullLifecycle()
    {
        var (e, _, _, _) = TestFixture.SetupEngine();
        var id = Create(e, "feature", new()
        {
            ["title"] = "Dark mode", ["description"] = "add dark theme", ["priority"] = 3,
            ["estimated_hours"] = 20, ["story_points"] = 8
        });
        Do(e, "anna", id, "analyze");
        Do(e, "anna", id, "start_dev");
        Do(e, "ivan", id, "submit_review");
        Do(e, "ivan", id, "approve");
        Assert.Equal("testing", e.GetObject(id)!.CurrentState);
        Do(e, "maria", id, "pass_test");
        Assert.Equal("done", e.GetObject(id)!.CurrentState);
        Assert.Equal(6, e.GetHistory(id).Count);
    }

    [Fact]
    public void RejectFromReview_ReturnToDev()
    {
        var (e, _, _, _) = TestFixture.SetupEngine();
        var id = ToReview(e, "task", Full(3, 5));
        Do(e, "ivan", id, "reject");
        Assert.Equal("development", e.GetObject(id)!.CurrentState);
        Do(e, "ivan", id, "submit_review");
        Assert.Equal("review", e.GetObject(id)!.CurrentState);
    }

    [Fact]
    public void FailTest_ReturnToDev()
    {
        var (e, _, _, _) = TestFixture.SetupEngine();
        var id = ToReview(e, "feature", Full(3, 5));
        Do(e, "ivan", id, "approve");
        Do(e, "maria", id, "fail_test");
        Assert.Equal("development", e.GetObject(id)!.CurrentState);
    }

    [Fact]
    public void CancelFromBacklog()
    {
        var (e, _, _, _) = TestFixture.SetupEngine();
        var id = Create(e, "task", null);
        Do(e, "anna", id, "cancel");
        Assert.Equal("cancelled", e.GetObject(id)!.CurrentState);
        Assert.False(e.PerformAction("anna", id, "analyze").Success);
    }

    [Fact]
    public void BugInheritsTaskTemplate()
    {
        var (e, _, _, _) = TestFixture.SetupEngine();
        var id = Create(e, "bug", null);
        Assert.Equal("backlog", e.GetObject(id)!.CurrentState);
    }

    [Fact]
    public void BugOverriddenPriorityRange()
    {
        var (e, _, _, _) = TestFixture.SetupEngine();
        var id = Create(e, "bug", Full(4, 5));
        Do(e, "anna", id, "analyze");
        var fail = e.PerformAction("anna", id, "start_dev");
        Assert.False(fail.Success);
        Assert.True(e.ModifyParameter("anna", id, "priority", 2).Success);
        Do(e, "anna", id, "start_dev");
        Assert.Equal("development", e.GetObject(id)!.CurrentState);
    }

    [Fact]
    public void FeatureInheritsAndAddsParams()
    {
        var (e, classes, _, _) = TestFixture.SetupEngine();
        var r = e.CreateObject("anna", "feature", new() { ["story_points"] = 8 });
        Assert.True(r.Success);
        var ids = classes.GetEffectiveParameters("feature").Select(p => p.Id).ToList();
        foreach (var x in new[] { "title", "description", "priority", "estimated_hours", "story_points" })
            Assert.Contains(x, ids);
    }

    [Fact]
    public void ConditionNotMet_StartDevFails()
    {
        var (e, _, _, _) = TestFixture.SetupEngine();
        var id = Create(e, "task", new() { ["title"] = "T", ["priority"] = 3, ["estimated_hours"] = 5 });
        Do(e, "anna", id, "analyze");
        Assert.False(e.PerformAction("anna", id, "start_dev").Success);
        Assert.Equal("analysis", e.GetObject(id)!.CurrentState);
    }

    [Fact]
    public void NullValue_PredicateFalse()
    {
        var (e, _, _, _) = TestFixture.SetupEngine();
        var id = Create(e, "task", null);
        Do(e, "anna", id, "analyze");
        Assert.False(e.PerformAction("anna", id, "start_dev").Success);
    }

    [Fact]
    public void CreateWithoutRole_Fails()
    {
        var (e, _, _, _) = TestFixture.SetupEngine();
        Assert.False(e.CreateObject("ivan", "task", null).Success);
    }

    [Fact]
    public void ActionWithoutRole_Fails()
    {
        var (e, _, _, _) = TestFixture.SetupEngine();
        var id = Create(e, "task", null);
        Assert.False(e.PerformAction("ivan", id, "analyze").Success);
    }

    [Fact]
    public void ActionInFinalState_Fails()
    {
        var (e, _, _, _) = TestFixture.SetupEngine();
        var id = Create(e, "task", null);
        Do(e, "anna", id, "cancel");
        var r = e.PerformAction("anna", id, "analyze");
        Assert.False(r.Success);
        Assert.Contains("конечном", r.Message);
    }

    [Fact]
    public void ModifyParameterForbidden_Fails()
    {
        var (e, _, _, _) = TestFixture.SetupEngine();
        var id = Create(e, "task", Full(3, 5));
        Do(e, "anna", id, "analyze");
        Do(e, "anna", id, "start_dev");
        Assert.False(e.ModifyParameter("ivan", id, "title", "X").Success);
    }

    [Fact]
    public void ModifyParameterWrongType_Fails()
    {
        var (e, _, _, _) = TestFixture.SetupEngine();
        var id = Create(e, "task", null);
        Assert.False(e.ModifyParameter("anna", id, "priority", "not_a_number").Success);
    }

    [Fact]
    public void ModifyParameterWrongRole_Fails()
    {
        var (e, _, _, _) = TestFixture.SetupEngine();
        var id = Create(e, "task", null);
        Assert.False(e.ModifyParameter("ivan", id, "title", "X").Success);
    }

    [Fact]
    public void AmbiguousTransitions_Fails()
    {
        var (e, _, _, _) = TestFixture.SetupEngine("ambiguous_template.yaml");
        var id = Create(e, "task", new() { ["title"] = "test", ["priority"] = 3 });
        var r = e.PerformAction("anna", id, "proceed");
        Assert.False(r.Success);
        Assert.Contains("шаблона", r.Message);
    }

    [Fact]
    public void CreateWithInvalidParameter_Fails()
    {
        var (e, _, _, _) = TestFixture.SetupEngine();
        Assert.False(e.CreateObject("anna", "task", new() { ["nonexistent"] = 1 }).Success);
    }

    [Fact]
    public void HistoryRecordsAllOperations()
    {
        var (e, _, _, _) = TestFixture.SetupEngine();
        var id = Create(e, "task", null);
        Assert.True(e.ModifyParameter("anna", id, "title", "Hello").Success);
        Do(e, "anna", id, "analyze");
        var h = e.GetHistory(id);
        Assert.Equal(3, h.Count);
        Assert.Equal(OperationType.Create, h[0].Operation);
        Assert.Equal(OperationType.ModifyParameter, h[1].Operation);
        Assert.Equal(OperationType.Action, h[2].Operation);
        Assert.All(h, x => Assert.Equal("anna", x.UserId));
        Assert.Contains("title", h[1].Details);
        Assert.Contains("Hello", h[1].Details);
        Assert.Equal("analysis", h[2].NewState);
        Assert.Contains("analyze", h[2].Details);
    }

    [Fact]
    public void GetAvailableActions_ReturnsCorrectActions()
    {
        var (e, _, _, _) = TestFixture.SetupEngine();
        var id = Create(e, "task", null);
        var a = e.GetAvailableActions("anna", id).Select(x => x.Id).OrderBy(x => x).ToList();
        Assert.Equal(new[] { "analyze", "cancel" }, a);
        Assert.Empty(e.GetAvailableActions("ivan", id));
    }
}
