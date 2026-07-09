using System.Runtime.InteropServices;

namespace LinuxSyscalls.Native;

/// <summary>
/// The "bare metal" path: we P/Invoke a SINGLE entrypoint, the libc function
/// `syscall(long number, ...)`, and pass the numeric syscall ID ourselves.
///
/// This is the closest .NET can practically get to issuing a raw `syscall`
/// instruction. We never call a named kernel wrapper like fork()/kill(); we
/// only ask the generic dispatcher to run syscall number N with these args.
///
/// libc still hosts the `syscall` symbol (the CLR itself is linked against
/// libc, so removing it entirely is not realistic on .NET), but from the
/// program's point of view every operation is expressed as a raw number.
/// </summary>
public static class RawSyscall
{
    // One overload per arity. Unused registers are simply ignored by the kernel.
    [DllImport("libc", EntryPoint = "syscall", SetLastError = true)]
    public static extern long syscall(long number);

    [DllImport("libc", EntryPoint = "syscall", SetLastError = true)]
    public static extern long syscall(long number, long a1);

    [DllImport("libc", EntryPoint = "syscall", SetLastError = true)]
    public static extern long syscall(long number, long a1, long a2);

    [DllImport("libc", EntryPoint = "syscall", SetLastError = true)]
    public static extern long syscall(long number, long a1, long a2, long a3);

    // Convenience wrapper: pointers are just addresses, so pass them as longs.
    public static long Call(long number, IntPtr a1, IntPtr a2, IntPtr a3)
        => syscall(number, a1.ToInt64(), a2.ToInt64(), a3.ToInt64());
}
