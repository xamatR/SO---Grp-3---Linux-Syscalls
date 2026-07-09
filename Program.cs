using System.Runtime.InteropServices;
using LinuxSyscalls;
using LinuxSyscalls.Native;

// ---------------------------------------------------------------------------
// Guard rails: this program issues Linux x86_64 syscalls directly. It only
// makes sense on Linux (WSL2 counts) and the raw syscall numbers assume x86_64.
// ---------------------------------------------------------------------------
if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    Console.Error.WriteLine("This program calls Linux syscalls directly. Run it under WSL2 or Linux.");
    return 1;
}
if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
    Console.Error.WriteLine("WARNING: raw syscall numbers assume x86_64; other arches will misbehave.");

long myPid = RawSyscall.syscall(Sys.getpid);
Console.WriteLine($"losys - Linux process/thread control via syscalls   (my pid = {myPid})");

while (true)
{
    Console.WriteLine("""

        ============ MENU ============
        Processes (raw syscall):
          1) Create process    (fork + execve)
          2) List processes    (/proc)
          3) Terminate process (kill)
        Threads (pthreads):
          4) Create N worker threads (pthread_create)
          5) List threads      (/proc/self/task)
          6) Terminate all workers (stop + pthread_join)
          7) Cancel one worker (pthread_cancel)
          0) Exit
        ==============================
        """);
    Console.Write("choice> ");
    string? choice = Console.ReadLine();

    try
    {
        switch (choice)
        {
            case "1": CreateProcess(); break;
            case "2": ListProcesses(); break;
            case "3": TerminateProcess(); break;
            case "4": CreateThreads(); break;
            case "5": ListThreads(); break;
            case "6": ThreadOps.TerminateAll(); break;
            case "7": CancelThread(); break;
            case "0": case null: ThreadOps.TerminateAll(); return 0;
            default: Console.WriteLine("Unknown choice."); break;
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"ERROR: {ex.Message}");
    }
}

// --------------------------- handlers ---------------------------

static void CreateProcess()
{
    Console.Write("program to run (e.g. /bin/echo): ");
    string program = (Console.ReadLine() ?? "").Trim();
    if (program.Length == 0) { Console.WriteLine("cancelled."); return; }

    Console.Write("arguments (space separated, optional): ");
    string[] args = (Console.ReadLine() ?? "")
        .Split(' ', StringSplitOptions.RemoveEmptyEntries);

    int pid = ProcessOps.Spawn(program, args);
    Console.WriteLine($"-> spawned child pid {pid}. Reaping...");
    ProcessOps.Wait(pid);
    Console.WriteLine($"-> child {pid} reaped.");
}

static void ListProcesses()
{
    int n = 0;
    Console.WriteLine($"{"PID",8}  {"STATE",5}  NAME");
    foreach (var (pid, name, state) in ProcessOps.List().OrderBy(p => p.Pid))
    {
        Console.WriteLine($"{pid,8}  {state,5}  {name}");
        n++;
    }
    Console.WriteLine($"({n} processes)");
}

static void TerminateProcess()
{
    Console.Write("pid to signal: ");
    if (!int.TryParse(Console.ReadLine(), out int pid)) { Console.WriteLine("invalid pid."); return; }

    Console.Write("signal [15=TERM (default), 9=KILL]: ");
    string s = (Console.ReadLine() ?? "").Trim();
    int sig = s.Length == 0 ? Signal.SIGTERM : int.Parse(s);

    ProcessOps.Terminate(pid, sig);
    Console.WriteLine($"-> sent signal {sig} to pid {pid}.");
}

static void CreateThreads()
{
    Console.Write("how many worker threads: ");
    if (!int.TryParse(Console.ReadLine(), out int n) || n <= 0) { Console.WriteLine("invalid count."); return; }
    ThreadOps.Create(n);
}

static void ListThreads()
{
    Console.WriteLine($"{"TID",8}  NAME");
    int n = 0;
    foreach (var (tid, name) in ThreadOps.List().OrderBy(t => t.Tid))
    {
        Console.WriteLine($"{tid,8}  {name}");
        n++;
    }
    Console.WriteLine($"({n} threads in this process)");
}

static void CancelThread()
{
    Console.Write("worker index to cancel: ");
    if (!int.TryParse(Console.ReadLine(), out int i)) { Console.WriteLine("invalid index."); return; }
    ThreadOps.Cancel(i);
}
