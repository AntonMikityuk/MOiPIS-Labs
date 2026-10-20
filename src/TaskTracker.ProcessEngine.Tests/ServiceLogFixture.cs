using System.Text;

namespace TaskTracker.ProcessEngine.Tests;

public class ServiceLogFixture : IDisposable
{
    private readonly StreamWriter _writer;
    private int _testCounter;

    public string LogPath { get; }

    public ServiceLogFixture()
    {
        LogPath = Path.Combine(AppContext.BaseDirectory, "service-tests.log");
        _writer = new StreamWriter(LogPath, false, Encoding.UTF8) { AutoFlush = true };
        _writer.WriteLine($"=== Лог тестов StateMachineService ===");
        _writer.WriteLine($"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
    }

    public Action<string> StartTest(string testName)
    {
        _testCounter++;
        _writer.WriteLine();
        _writer.WriteLine("---");
        _writer.WriteLine($"Тест {_testCounter}: {testName}");
        _writer.WriteLine("---");
        return msg => _writer.WriteLine($"  {msg}");
    }

    public void Dispose()
    {
        _writer.WriteLine();
        _writer.WriteLine($"=== Завершено: {_testCounter} тестов ===");
        _writer.Dispose();
    }
}
