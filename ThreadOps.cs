using System.Runtime.InteropServices;
using LinuxSyscalls.Native;

namespace LinuxSyscalls;

/// <summary>
/// Create / list / terminate THREADS using pthreads.
///
/// Why pthreads and not a raw clone() syscall: clone() hands control to a fresh
/// kernel task on a stack we allocate, with none of the CLR's per-thread setup.
/// Running managed code there tends to crash. pthread_create builds the same
/// kernel thread (it calls clone() internally) but leaves it in a state the
/// runtime can safely ATTACH to when our [UnmanagedCallersOnly] entrypoint is
/// invoked - that reverse-P/Invoke boundary is what makes it usable.
/// </summary>
public static class ThreadOps
{
    // Shared stop flag the worker threads poll. volatile via Volatile.Read/Write.
    private static int _keepRunning;

    // Keep the pthread_t handles so we can join/cancel them later.
    private static readonly List<nuint> _handles = new();

    /// <summary>
    /// The native entrypoint each pthread runs. It must be a static method marked
    /// [UnmanagedCallersOnly] so the CLR can expose it as a plain C function
    /// pointer and attach the OS thread when it is called.
    /// </summary>
    [UnmanagedCallersOnly]
    private static IntPtr Worker(IntPtr arg)
    {
        long index = arg.ToInt64();
        // gettid via the raw syscall shows the real kernel thread id (not the
        // pthread_t handle, which is just a userspace cookie).
        long tid = RawSyscall.syscall(Sys.gettid);
        Console.WriteLine($"   [worker {index}] started  kernel-tid={tid}");

        int ticks = 0;
        while (Volatile.Read(ref _keepRunning) == 1)
        {
            Thread.Sleep(500);
            ticks++;
        }

        Console.WriteLine($"   [worker {index}] stopping after {ticks} ticks (tid={tid})");
        return IntPtr.Zero;
    }

    /// <summary>Create <paramref name="count"/> real kernel threads via pthread_create.</summary>
    public static unsafe void Create(int count)
    {
        Volatile.Write(ref _keepRunning, 1);

        for (int i = 0; i < count; i++)
        {
            int rc = Libc.pthread_create(
                out nuint handle,
                IntPtr.Zero,                                            // default attributes
                &Worker,                                                // C function pointer
                new IntPtr(i));                                         // arg = worker index

            if (rc != 0)
                throw new InvalidOperationException($"pthread_create failed (rc {rc})");

            _handles.Add(handle);
        }
        Console.WriteLine($"Created {count} thread(s). Total tracked: {_handles.Count}");
    }

    // =====================================================================
    // LIST:  enumerate /proc/self/task
    // =====================================================================
    // Every thread of the current process appears as /proc/self/task/<tid>.
    public static IEnumerable<(int Tid, string Name)> List()
    {
        foreach (var dir in Directory.EnumerateDirectories("/proc/self/task"))
        {
            string name = Path.GetFileName(dir);
            if (!int.TryParse(name, out int tid)) continue;

            string comm = "?";
            try { comm = File.ReadAllText($"/proc/self/task/{tid}/comm").Trim(); }
            catch { /* thread may have exited */ }

            yield return (tid, comm);
        }
    }

    // =====================================================================
    // TERMINATE:  cooperative stop + pthread_join
    // =====================================================================
    /// <summary>Signal every worker to stop, then join (wait) for each one.</summary>
    public static void TerminateAll()
    {
        if (_handles.Count == 0)
        {
            Console.WriteLine("No worker threads to terminate.");
            return;
        }

        Console.WriteLine("Signalling workers to stop...");
        Volatile.Write(ref _keepRunning, 0); // cooperative shutdown

        foreach (var h in _handles)
            Libc.pthread_join(h, out _);      // block until the thread actually exits

        Console.WriteLine($"Joined {_handles.Count} thread(s).");
        _handles.Clear();
    }

    /// <summary>
    /// Forcefully cancel a single thread by index (demonstrates pthread_cancel).
    /// Note: cancellation only takes effect at cancellation points such as
    /// Thread.Sleep, so a tight CPU loop would not stop this way.
    /// </summary>
    public static void Cancel(int index)
    {
        if (index < 0 || index >= _handles.Count)
        {
            Console.WriteLine("No such worker index.");
            return;
        }
        int rc = Libc.pthread_cancel(_handles[index]);
        Console.WriteLine(rc == 0 ? $"pthread_cancel sent to worker {index}." : $"pthread_cancel rc={rc}");
    }
}
