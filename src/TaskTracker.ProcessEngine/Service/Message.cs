namespace TaskTracker.ProcessEngine.Service;

public class Message
{
    public string Event { get; }
    public object? Data { get; }

    public Message(string @event, object? data = null)
    {
        Event = @event;
        Data = data;
    }
}
