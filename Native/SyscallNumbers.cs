namespace LinuxSyscalls.Native;

/// <summary>
/// Linux system call numbers for the x86_64 ABI.
///
/// These are the integers you load into the RAX register before the `syscall`
/// instruction. They are architecture specific: on ARM64 (aarch64) the numbers
/// are DIFFERENT. The canonical source is the kernel table
/// arch/x86/entry/syscalls/syscall_64.tbl.
///
/// WSL2 on a normal PC is x86_64, so these are the right ones there.
/// </summary>
public static class Sys
{
    // ---- I/O (used only for demos) ----
    public const long read  = 0;
    public const long write = 1;

    // ---- process lifecycle ----
    public const long clone   = 56;  // create a task (thread/process) - what fork/pthread build on
    public const long fork    = 57;  // create a child process (copy of the caller)
    public const long execve  = 59;  // replace the current process image with a new program
    public const long exit    = 60;  // terminate the CALLING thread
    public const long wait4   = 61;  // wait for a child process to change state (reap zombies)
    public const long kill    = 62;  // send a signal to a process
    public const long exit_group = 231; // terminate ALL threads in the process

    // ---- identity ----
    public const long getpid  = 39;  // process id (thread-group id)
    public const long gettid  = 186; // kernel thread id of the calling thread
    public const long getppid = 110; // parent process id

    // ---- signals aimed at a single thread ----
    public const long tgkill  = 234; // send a signal to a specific thread (tgid + tid)
}

/// <summary>Common signal numbers (also x86_64/most-Linux stable).</summary>
public static class Signal
{
    public const int SIGINT  = 2;   // interrupt (Ctrl+C)
    public const int SIGKILL = 9;   // unconditional kill, cannot be caught
    public const int SIGTERM = 15;  // polite "please terminate"
    public const int SIGCONT = 18;
    public const int SIGSTOP = 19;
}
