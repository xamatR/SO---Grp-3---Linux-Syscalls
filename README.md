# losys — Linux process & thread control via syscalls (.NET, no libraries)

A .NET console app that creates, lists, and terminates **processes** and **threads**
by talking directly to the Linux kernel — no `System.Diagnostics.Process`, no NuGet
packages.

- **Processes** use the **raw** path: P/Invoke the single `syscall(number, …)`
  dispatcher and pass the x86_64 syscall ID ourselves (`fork=57`, `execve=59`,
  `kill=62`, `wait4=61`, `getpid=39`, …).
- **Threads** use **pthreads** (`pthread_create/join/cancel`). There is no practical
  raw-`clone()` path here — a raw clone would run managed code on a thread the CLR
  never set up and crash — so the pthread wrappers are the one named-symbol P/Invoke
  that remains.

(libc still hosts the `syscall` symbol — the CLR itself links against libc, so
removing it entirely isn't realistic on .NET; the point is that every process
operation is expressed as an explicit syscall number.)

See **`EXAMPLES.md`** for a worked walkthrough of every menu choice.

## What maps to what

| Operation            | Processes                          | Threads                              |
|----------------------|------------------------------------|--------------------------------------|
| **Create**           | `fork()` + `execve()`              | `pthread_create()` (calls `clone()`) |
| **List**             | enumerate `/proc/<pid>`            | enumerate `/proc/self/task/<tid>`    |
| **Terminate**        | `kill(pid, SIGTERM/SIGKILL)`       | stop flag + `pthread_join()` / `pthread_cancel()` |

## Run it (WSL2, x86_64)

```bash
cd "/mnt/c/Users/xamat/Documents/Projetos/Trabalho SO/LinuxSyscalls"
dotnet run -c Release
```

If `dotnet` isn't in WSL yet:

```bash
sudo apt update && sudo apt install -y dotnet-sdk-8.0
```

Then follow the menu. Try: option **1** with `/bin/echo` and args `hello world`;
option **2** to list processes; **4** to spawn worker threads, **5** to see them
under `/proc/self/task`, **6** to join them cleanly.

## Design notes / caveats

- **`fork()` in a managed runtime is delicate.** After `fork()` only the calling
  thread survives in the child; the CLR's other threads (GC, etc.) do not. So we
  marshal argv/envp/path to native memory *before* forking and, in the child, do
  nothing but `execve` (then `_exit` on failure). See `ProcessOps.Spawn`.
- **Why pthreads, not raw `clone()`.** `clone()` would drop a fresh kernel task onto
  a stack we allocate, with none of the CLR's per-thread setup — managed code there
  tends to crash. `pthread_create` builds the same kernel thread but leaves it
  attachable; the `[UnmanagedCallersOnly]` worker entrypoint lets the runtime attach
  the thread on the reverse-P/Invoke boundary. See `ThreadOps.Worker`.
- **Syscall numbers are x86_64.** ARM64 numbers differ. The program warns if the
  process architecture isn't x64.
- **`pthread_cancel`** only fires at cancellation points (e.g. `Thread.Sleep`), so a
  tight CPU loop wouldn't stop that way — hence the cooperative stop flag for the
  clean shutdown path.

## Files

- `Native/SyscallNumbers.cs` — x86_64 syscall IDs + signal numbers
- `Native/RawSyscall.cs` — P/Invoke to the generic `syscall()` entrypoint
- `Native/Libc.cs` — named libc/pthread P/Invoke declarations
- `ProcessOps.cs` — create / list / terminate processes
- `ThreadOps.cs` — create / list / terminate threads
- `Program.cs` — interactive menu
