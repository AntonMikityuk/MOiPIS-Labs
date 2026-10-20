using TaskTracker.ProcessEngine.Service;

namespace TaskTracker.ProcessEngine.Tests;

/// <summary>
/// Тесты для сервиса-конечного автомата с очередью сообщений
/// Все тесты логируются в файл service-tests.log с разделителями между тестами
/// </summary>
public class StateMachineServiceTests : IClassFixture<ServiceLogFixture>
{
    private readonly ServiceLogFixture _fixture;

    public StateMachineServiceTests(ServiceLogFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Создать сервис с привязкой к логу текущего теста
    /// </summary>
    private StateMachineService CreateService(string testName)
    {
        var log = _fixture.StartTest(testName);
        return new StateMachineService(log);
    }

    /// <summary>
    /// Создать базовый автомат с тремя состояниями (idle - working - done)
    /// Используется как стартовая точка для большинства тестов
    /// </summary>
    private StateMachineService BasicMachine(string testName)
    {
        var svc = CreateService(testName);
        svc.AddState("idle");
        svc.AddState("working");
        svc.AddState("done");
        svc.SetInitialState("idle");
        svc.AddTransition("idle", "start", "working");
        svc.AddTransition("working", "finish", "done");
        return svc;
    }

    /// <summary>
    /// Проверяет, что AddState корректно регистрирует новые состояния и они доступны через свойство States
    /// </summary>
    [Fact]
    public void AddState_RegistersNewState()
    {
        var svc = CreateService("Регистрация состояний");
        svc.AddState("idle");
        svc.AddState("active");
        Assert.Contains("idle", svc.States);
        Assert.Contains("active", svc.States);
        Assert.Equal(2, svc.States.Count);
    }

    /// <summary>
    /// Проверяет, что повторная регистрация одного и того же состояния выбрасывает InvalidOperationException
    /// </summary>
    [Fact]
    public void AddState_Duplicate_Throws()
    {
        var svc = CreateService("Дублирование состояния");
        svc.AddState("idle");
        Assert.Throws<InvalidOperationException>(() => svc.AddState("idle"));
    }

    /// <summary>
    /// Проверяет, что SetInitialState корректно устанавливает текущее состояние автомата
    /// </summary>
    [Fact]
    public void SetInitialState_SetsCurrentState()
    {
        var svc = CreateService("Установка начального состояния");
        svc.AddState("idle");
        svc.SetInitialState("idle");
        Assert.Equal("idle", svc.CurrentState);
    }

    /// <summary>
    /// Проверяет, что установка несуществующего состояния как начального выбрасывает InvalidOperationException
    /// </summary>
    [Fact]
    public void SetInitialState_UnknownState_Throws()
    {
        var svc = CreateService("Начальное состояние — неизвестное");
        Assert.Throws<InvalidOperationException>(() => svc.SetInitialState("unknown"));
    }

    /// <summary>
    /// Проверяет, что добавление перехода с незарегистрированным исходным состоянием выбрасывает InvalidOperationException
    /// </summary>
    [Fact]
    public void AddTransition_InvalidSourceState_Throws()
    {
        var svc = CreateService("Переход из несуществующего состояния");
        svc.AddState("idle");
        Assert.Throws<InvalidOperationException>(() => svc.AddTransition("missing", "go", "idle"));
    }

    /// <summary>
    /// Проверяет, что добавление перехода с незарегистрированным целевым состоянием выбрасывает InvalidOperationException
    /// </summary>
    [Fact]
    public void AddTransition_InvalidTargetState_Throws()
    {
        var svc = CreateService("Переход в несуществующее состояние");
        svc.AddState("idle");
        Assert.Throws<InvalidOperationException>(() => svc.AddTransition("idle", "go", "missing"));
    }

    /// <summary>
    /// Проверяет полный цикл обработки сообщения, обработчик меняет данные сервиса и выполняется переход состояния
    /// </summary>
    [Fact]
    public void ProcessNext_HandlerAndTransition()
    {
        var svc = BasicMachine("Обработка: хэндлер + переход");
        svc.RegisterHandler("start", msg =>
        {
            svc.Data["assignee"] = "ivan";
            svc.Data["started_at"] = "2026-10-20";
        });
        svc.Enqueue(new Message("start"));
        Assert.True(svc.ProcessNext());
        Assert.Equal("ivan", svc.Data["assignee"]);
        Assert.Equal("working", svc.CurrentState);
    }

    /// <summary>
    /// Проверяет, что переход выполняется даже без зарегистрированного обработчика для события
    /// </summary>
    [Fact]
    public void ProcessNext_TransitionOnly_NoHandler()
    {
        var svc = BasicMachine("Переход без обработчика");
        svc.Enqueue(new Message("start"));
        svc.ProcessNext();
        Assert.Equal("working", svc.CurrentState);
    }

    /// <summary>
    /// Проверяет, что обработчик вызывается и меняет данные, даже если для события не определён переход (состояние не меняется)
    /// </summary>
    [Fact]
    public void ProcessNext_HandlerOnly_NoTransition()
    {
        var svc = BasicMachine("Обработчик без перехода");
        svc.RegisterHandler("ping", msg =>
        {
            svc.Data["last_ping"] = DateTime.Now.ToString("HH:mm:ss");
            svc.Data["ping_count"] = ((int)(svc.Data.GetValueOrDefault("ping_count") ?? 0)) + 1;
        });
        svc.Enqueue(new Message("ping"));
        svc.ProcessNext();
        Assert.Equal(1, svc.Data["ping_count"]);
        Assert.Equal("idle", svc.CurrentState);
    }

    /// <summary>
    /// Проверяет, что ProcessNext возвращает false, когда очередь сообщений пуста
    /// </summary>
    [Fact]
    public void ProcessNext_EmptyQueue_ReturnsFalse()
    {
        var svc = BasicMachine("Пустая очередь");
        Assert.False(svc.ProcessNext());
    }

    /// <summary>
    /// Проверяет, что ProcessAll обрабатывает все сообщения последовательно
    /// Каждый обработчик меняет данные (отрабатывают по цепочке)
    /// </summary>
    [Fact]
    public void ProcessAll_MultipleMessages()
    {
        var svc = BasicMachine("Обработка нескольких сообщений подряд");
        svc.Data["progress"] = "не начато";
        svc.RegisterHandler("start", _ => svc.Data["progress"] = "в работе");
        svc.RegisterHandler("finish", _ =>
        {
            svc.Data["progress"] = "завершено";
            svc.Data["result"] = "success";
        });
        svc.Enqueue(new Message("start"));
        svc.Enqueue(new Message("finish"));
        var count = svc.ProcessAll();
        Assert.Equal(2, count);
        Assert.Equal("done", svc.CurrentState);
        Assert.Equal("завершено", svc.Data["progress"]);
        Assert.Equal("success", svc.Data["result"]);
    }

    /// <summary>
    /// Проверяет, что ProcessAll на пустой очереди возвращает ноль без ошибок
    /// </summary>
    [Fact]
    public void ProcessAll_EmptyQueue_ReturnsZero()
    {
        var svc = BasicMachine("ProcessAll на пустой очереди");
        Assert.Equal(0, svc.ProcessAll());
    }

    /// <summary>
    /// Проверяет, что обработчик получает данные из сообщения и может записать их в хранилище сервиса
    /// </summary>
    [Fact]
    public void Handler_ReceivesCorrectData()
    {
        var svc = BasicMachine("Данные в сообщении передаются хэндлеру");
        svc.RegisterHandler("start", msg =>
        {
            svc.Data["priority"] = msg.Data;
            svc.Data["is_urgent"] = (int)(msg.Data ?? 0) <= 2;
        });
        svc.Enqueue(new Message("start", 1));
        svc.ProcessNext();
        Assert.Equal(1, svc.Data["priority"]);
        Assert.Equal(true, svc.Data["is_urgent"]);
    }

    /// <summary>
    /// Проверяет, что событие без зарегистрированного перехода и обработчика не меняет текущее состояние автомата
    /// </summary>
    [Fact]
    public void UnknownEvent_StateUnchanged()
    {
        var svc = BasicMachine("Неизвестное событие — состояние не меняется");
        svc.Enqueue(new Message("unknown_event"));
        svc.ProcessNext();
        Assert.Equal("idle", svc.CurrentState);
    }

    /// <summary>
    /// Проверяет динамическое добавление состояний и переходов уже после старта автомата (обработчики фиксируют причину паузы)
    /// </summary>
    [Fact]
    public void DynamicRegistration_AddStateAndTransitionAtRuntime()
    {
        var svc = BasicMachine("Динамическое добавление состояний и переходов в рантайме");
        svc.RegisterHandler("start", _ => svc.Data["assignee"] = "ivan");
        svc.Enqueue(new Message("start"));
        svc.ProcessNext();
        Assert.Equal("working", svc.CurrentState);

        svc.AddState("paused");
        svc.AddTransition("working", "pause", "paused");
        svc.AddTransition("paused", "resume", "working");

        svc.RegisterHandler("pause", msg => svc.Data["pause_reason"] = msg.Data);
        svc.RegisterHandler("resume", _ => svc.Data.Remove("pause_reason"));

        svc.Enqueue(new Message("pause", "ожидание ревью"));
        svc.ProcessNext();
        Assert.Equal("paused", svc.CurrentState);
        Assert.Equal("ожидание ревью", svc.Data["pause_reason"]);

        svc.Enqueue(new Message("resume"));
        svc.ProcessNext();
        Assert.Equal("working", svc.CurrentState);
        Assert.False(svc.Data.ContainsKey("pause_reason"));
    }

    /// <summary>
    /// Проверяет, что обработчик можно зарегистрировать после создания автомата и он изменит данные при следующем подходящем событии
    /// </summary>
    [Fact]
    public void DynamicRegistration_AddHandlerAtRuntime()
    {
        var svc = BasicMachine("Динамическая регистрация обработчика в рантайме");
        svc.Data["reviewed_by"] = null;

        svc.Enqueue(new Message("start"));
        svc.ProcessNext();

        svc.RegisterHandler("finish", _ =>
        {
            svc.Data["reviewed_by"] = "maria";
            svc.Data["review_result"] = "approved";
        });
        svc.Enqueue(new Message("finish"));
        svc.ProcessNext();

        Assert.Equal("maria", svc.Data["reviewed_by"]);
        Assert.Equal("approved", svc.Data["review_result"]);
        Assert.Equal("done", svc.CurrentState);
    }

    /// <summary>
    /// Проверяет, что сообщения обрабатываются в порядке FIFO (первое добавленное обрабатывается первым, данные меняются последовательно)
    /// </summary>
    [Fact]
    public void MessageQueue_FIFO_Order()
    {
        var svc = BasicMachine("Порядок очереди FIFO");
        svc.Data["step"] = 0;
        svc.RegisterHandler("start", _ => svc.Data["step"] = 1);
        svc.RegisterHandler("finish", _ => svc.Data["step"] = 2);

        svc.Enqueue(new Message("start"));
        svc.Enqueue(new Message("finish"));
        svc.ProcessAll();

        Assert.Equal(2, svc.Data["step"]);
    }

    /// <summary>
    /// Проверяет, что QueueCount корректно отражает количество сообщений в очереди после добавления и обработки
    /// </summary>
    [Fact]
    public void QueueCount_ReflectsState()
    {
        var svc = BasicMachine("Счётчик очереди");
        Assert.Equal(0, svc.QueueCount);
        svc.Enqueue(new Message("start"));
        Assert.Equal(1, svc.QueueCount);
        svc.Enqueue(new Message("finish"));
        Assert.Equal(2, svc.QueueCount);
        svc.ProcessNext();
        Assert.Equal(1, svc.QueueCount);
    }

    /// <summary>
    /// Проверяет, что повторная регистрация обработчика для того же события заменяет предыдущий (вызывается только новый)
    /// </summary>
    [Fact]
    public void OverrideHandler_NewReplacesOld()
    {
        var svc = BasicMachine("Замена обработчика");
        var calls = new List<int>();
        svc.RegisterHandler("start", _ => calls.Add(1));
        svc.RegisterHandler("start", _ => calls.Add(2));
        svc.Enqueue(new Message("start"));
        svc.ProcessNext();
        Assert.Single(calls);
        Assert.Equal(2, calls[0]);
    }
}
