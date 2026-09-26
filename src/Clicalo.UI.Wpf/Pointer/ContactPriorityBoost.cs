namespace Clicalo.UI.Wpf.Pointer;

/// <summary>
/// Raises the UI thread to <see cref="ThreadPriority.AboveNormal"/> while any contact is down on any surface of the
/// thread, and gives it back its priority when the last one ends (blueprint §3.2: Normal, AboveNormal while there is
/// contact). Every <see cref="PointerInputSource"/> of the thread shares the count, so two surfaces never undo each
/// other's boost. Thread-affine: only called from the window procedure of the thread's windows.
/// </summary>
internal static class ContactPriorityBoost
{
    [ThreadStatic]
    private static int _holders;

    [ThreadStatic]
    private static ThreadPriority _previous;

    /// <summary>A source of this thread has contacts down: boost if it is the first one.</summary>
    public static void Acquire()
    {
        if (_holders++ > 0)
        {
            return;
        }

        var thread = Thread.CurrentThread;
        _previous = thread.Priority;
        if (_previous < ThreadPriority.AboveNormal)
        {
            thread.Priority = ThreadPriority.AboveNormal;
        }
    }

    /// <summary>A source of this thread has no contact left: restore the priority if it was the last one.</summary>
    public static void Release()
    {
        if (_holders == 0 || --_holders > 0)
        {
            return;
        }

        Thread.CurrentThread.Priority = _previous;
    }
}
