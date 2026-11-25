namespace Freuds.Core.Events;

/// <summary>
/// Publish / subscribe channel between the controller, model and view.
/// Handlers are keyed by the event's type, so subscribers never see events they did not ask for.
/// </summary>
public sealed class EventManager
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();

    public void Subscribe<T>(Action<T> handler)
    {
        if (!_handlers.TryGetValue(typeof(T), out var list))
        {
            list = new List<Delegate>();
            _handlers[typeof(T)] = list;
        }
        list.Add(handler);
    }

    public void Unsubscribe<T>(Action<T> handler)
    {
        if (_handlers.TryGetValue(typeof(T), out var list))
        {
            list.Remove(handler);
        }
    }

    public void Publish<T>(T e)
    {
        if (!_handlers.TryGetValue(typeof(T), out var list) || list.Count == 0)
        {
            return;
        }

        // Copy so a handler may subscribe or unsubscribe while the event is being delivered.
        foreach (var handler in list.ToArray())
        {
            ((Action<T>)handler)(e);
        }
    }
}
