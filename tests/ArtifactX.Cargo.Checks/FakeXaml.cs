// A queued dispatcher and Loaded/Unloaded events for testing feed lifetimes.
// No native controls or application are launched by these checks.
namespace Microsoft.UI.Xaml;

public sealed class TestDispatcher
{
    private readonly Queue<Action> _queue = new();
    public bool TryEnqueue(Action action) { _queue.Enqueue(action); return true; }
    public void Drain() { while (_queue.TryDequeue(out var action)) action(); }
}
public sealed class FrameworkElement
{
    public event EventHandler? Loaded;
    public event EventHandler? Unloaded;
    public TestDispatcher DispatcherQueue { get; } = new();
    public void RaiseLoaded() => Loaded?.Invoke(this, EventArgs.Empty);
    public void RaiseUnloaded() => Unloaded?.Invoke(this, EventArgs.Empty);
}
