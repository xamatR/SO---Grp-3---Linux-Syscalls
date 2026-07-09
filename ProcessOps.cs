using System.Runtime.InteropServices;
using System.Text;
using LinuxSyscalls.Native;

namespace LinuxSyscalls;

/// <summary>
/// Create / list / terminate PROCESSES using the RAW syscall() dispatcher only:
/// we pass the x86_64 syscall number ourselves for every kernel trap.
/// </summary>
public static class ProcessOps
{
    // =====================================================================
    // CREATE:  fork() + execve()   (syscall 57 + 59)
    // =====================================================================
    //
    // Danger note (important for a .NET runtime):
    // After fork() only the calling thread survives in the child; the CLR's
    // other threads (GC, etc.) do not. If the child touches a lock that some
    // other thread held at fork time, it can deadlock. The safe recipe is:
    //   1. Marshal EVERYTHING to native memory BEFORE forking.
    //   2. In the child, do nothing but execve (+ exit on failure).
    // We follow exactly that recipe below.
    public static int Spawn(string program, string[] programArgs)
    {
        // argv[0] is conventionally the program name/path itself.
        string[] argv = new string[programArgs.Length + 1];
        argv[0] = program;
        Array.Copy(programArgs, 0, argv, 1, programArgs.Length);

        // Inherit the current environment as "KEY=VALUE" strings.
        var env = Environment.GetEnvironmentVariables();
        var envList = new List<string>(env.Count);
        foreach (System.Collections.DictionaryEntry kv in env)
            envList.Add($"{kv.Key}={kv.Value}");

        // ---- marshal to unmanaged memory (parent side, before fork) ----
        IntPtr pPath = Utf8(program);
        IntPtr pArgv = StringArray(argv);
        IntPtr pEnvp = StringArray(envList.ToArray());

        int pid = (int)RawSyscall.syscall(Sys.fork);

        if (pid == 0)
        {
            // ---------------- CHILD ----------------
            // Only async-signal-safe-ish work here: fire execve, then exit.
            RawSyscall.Call(Sys.execve, pPath, pArgv, pEnvp);
            // Reached only if execve failed (e.g. program not found).
            RawSyscall.syscall(Sys.exit, 127);
            return -1; // unreachable
        }

        // ---------------- PARENT ----------------
        int err = Marshal.GetLastPInvokeError();
        FreeStringArray(pArgv, argv.Length);
        FreeStringArray(pEnvp, envList.Count);
        Marshal.FreeHGlobal(pPath);

        if (pid < 0)
            throw new InvalidOperationException($"fork failed (errno {err})");

        return pid;
    }

    /// <summary>Reap a child so it does not linger as a zombie. wait4(pid, null, 0, null).</summary>
    public static void Wait(int pid)
        => RawSyscall.syscall(Sys.wait4, pid, 0, 0);

    // =====================================================================
    // TERMINATE:  kill(pid, signal)   (syscall 62)
    // =====================================================================
    public static void Terminate(int pid, int signal)
    {
        long rc = RawSyscall.syscall(Sys.kill, pid, signal);
        if (rc != 0)
            throw new InvalidOperationException(
                $"kill({pid},{signal}) failed (errno {Marshal.GetLastPInvokeError()})");
    }

    // =====================================================================
    // LIST:  enumerate /proc
    // =====================================================================
    //
    // There is no dedicated "list processes" syscall; the kernel exposes the
    // process table through the /proc virtual filesystem. Reading it still ends
    // up in getdents64/openat/read syscalls underneath - we just let the
    // runtime issue those, since hand-rolling getdents64 struct parsing adds a
    // lot of noise without teaching anything new about process control.
    public static IEnumerable<(int Pid, string Name, char State)> List()
    {
        foreach (var dir in Directory.EnumerateDirectories("/proc"))
        {
            string name = Path.GetFileName(dir);
            if (!int.TryParse(name, out int pid)) continue; // skip non-PID entries

            string comm = "?";
            char state = '?';
            try
            {
                // /proc/<pid>/stat: "pid (comm) state ppid ..."
                // comm may contain spaces/parentheses, so slice on the LAST ')'.
                string stat = File.ReadAllText($"/proc/{pid}/stat");
                int open = stat.IndexOf('(');
                int close = stat.LastIndexOf(')');
                if (open >= 0 && close > open)
                {
                    comm = stat.Substring(open + 1, close - open - 1);
                    string rest = stat[(close + 2)..];
                    state = rest.Length > 0 ? rest[0] : '?';
                }
            }
            catch { /* process may have exited between listing and reading */ }

            yield return (pid, comm, state);
        }
    }

    // ---------------------- marshalling helpers ----------------------

    /// <summary>Copy a managed string to a NUL-terminated UTF-8 block in native memory.</summary>
    private static IntPtr Utf8(string s)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(s);
        IntPtr p = Marshal.AllocHGlobal(bytes.Length + 1);
        Marshal.Copy(bytes, 0, p, bytes.Length);
        Marshal.WriteByte(p, bytes.Length, 0); // NUL terminator
        return p;
    }

    /// <summary>Build a C-style char*[] (NULL-terminated array of string pointers).</summary>
    private static IntPtr StringArray(string[] items)
    {
        IntPtr block = Marshal.AllocHGlobal(IntPtr.Size * (items.Length + 1));
        for (int i = 0; i < items.Length; i++)
            Marshal.WriteIntPtr(block, i * IntPtr.Size, Utf8(items[i]));
        Marshal.WriteIntPtr(block, items.Length * IntPtr.Size, IntPtr.Zero); // NULL sentinel
        return block;
    }

    private static void FreeStringArray(IntPtr block, int count)
    {
        for (int i = 0; i < count; i++)
            Marshal.FreeHGlobal(Marshal.ReadIntPtr(block, i * IntPtr.Size));
        Marshal.FreeHGlobal(block);
    }
}
