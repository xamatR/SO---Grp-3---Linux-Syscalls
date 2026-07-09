using System.Runtime.InteropServices;

namespace LinuxSyscalls.Native;

/// <summary>
/// The only named-symbol P/Invoke left in the project: the pthreads API used to
/// create/join/cancel threads. There is no practical "raw syscall number" path
/// for thread creation here, because a raw clone() would hand control to a
/// fresh kernel task with none of the CLR's per-thread setup and crash. So the
/// pthread wrappers stay - they call clone() internally but leave the thread in
/// a state the runtime can safely attach to.
///
/// On glibc 2.34+ (Ubuntu 22.04 / WSL2 default) the pthread_* functions live
/// inside libc itself, so DllImport("libc") is correct. On very old distros
/// they were in libpthread.
/// </summary>
public static unsafe class Libc
{
    // pthread_t is an "unsigned long" (8 bytes on x86_64) -> nuint.
    // start_routine is a C function pointer `void* (*)(void*)`.

    [DllImport("libc", SetLastError = true)]
    public static extern int pthread_create(
        out nuint thread,
        IntPtr attr,
        delegate* unmanaged<IntPtr, IntPtr> start_routine,
        IntPtr arg);

    /// <summary>Block until the thread finishes; retval receives its return value.</summary>
    [DllImport("libc", SetLastError = true)]
    public static extern int pthread_join(nuint thread, out IntPtr retval);

    /// <summary>Request cancellation of a thread (cooperative, at cancellation points).</summary>
    [DllImport("libc", SetLastError = true)]
    public static extern int pthread_cancel(nuint thread);

    /// <summary>Send a signal to a specific thread within this process.</summary>
    [DllImport("libc", SetLastError = true)]
    public static extern int pthread_kill(nuint thread, int sig);
}
