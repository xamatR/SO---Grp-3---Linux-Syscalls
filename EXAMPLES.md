# losys — worked examples for every menu choice

Run under WSL2 / Linux (x86_64):

```bash
cd "/mnt/c/Users/xamat/Documents/Projetos/Trabalho SO/LinuxSyscalls"
dotnet run -c Release
```

At startup you'll see something like:

```
losys - Linux process/thread control via syscalls   (my pid = 4321)

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
choice>
```

Lines you type are marked `«you type»`. Output is illustrative — PIDs/TIDs vary.

---

## 1) Create process — `fork()` + `execve()`

Runs another program as a child of losys. `fork` (syscall 57) clones the process,
`execve` (syscall 59) replaces the child's image with the target program, and the
parent reaps it with `wait4` (syscall 61).

### Example A — echo some text
```
choice> 1                        «you type»
program to run (e.g. /bin/echo): /bin/echo      «you type»
arguments (space separated, optional): hello world from fork      «you type»
hello world from fork
-> spawned child pid 4402. Reaping...
-> child 4402 reaped.
```

### Example B — list a directory with /bin/ls
```
choice> 1
program to run (e.g. /bin/echo): /bin/ls
arguments (space separated, optional): -la /tmp
total 8
drwxrwxrwt  2 root root 4096 Jul  8 09:00 .
drwxr-xr-x 20 root root 4096 Jul  8 08:55 ..
-> spawned child pid 4419. Reaping...
-> child 4419 reaped.
```

### Example C — program not found (execve fails, child exits 127)
```
choice> 1
program to run (e.g. /bin/echo): /bin/does-not-exist
arguments (space separated, optional):
-> spawned child pid 4425. Reaping...
-> child 4425 reaped.
```
Nothing prints from the child: `execve` failed, so the child fell through to
`exit(127)`. Give a full path (`/bin/echo`, not `echo`) — there is no PATH search.

### Example D — a long-running child to use in choice 3
Start something that keeps running (e.g. `/bin/sleep 300`). Because losys reaps
synchronously, run this from a **second** terminal instead, or just note that the
menu will block on "Reaping..." until the child exits. To practice termination,
prefer launching `sleep` outside losys and killing it with choice 3.

---

## 2) List processes — read `/proc`

Enumerates every `/proc/<pid>` entry and reads `/proc/<pid>/stat` for the name and
state letter (`R` running, `S` sleeping, `Z` zombie, `T` stopped, ...).

```
choice> 2                        «you type»
     PID  STATE  NAME
       1      S  systemd
     120      S  dbus-daemon
     311      S  bash
    4321      R  losys
    4500      S  sleep
(42 processes)
```

Tip: note the `PID` of a process here (e.g. `4500 sleep`) and feed it to choice 3.

---

## 3) Terminate process — `kill(pid, signal)`

Sends a signal with `kill` (syscall 62). Default `15` = SIGTERM (polite), `9` =
SIGKILL (unconditional).

### Example A — polite terminate (SIGTERM)
```
choice> 3                        «you type»
pid to signal: 4500              «you type»
signal [15=TERM (default), 9=KILL]:        «you type: press Enter for 15»
-> sent signal 15 to pid 4500.
```

### Example B — force kill (SIGKILL)
```
choice> 3
pid to signal: 4500
signal [15=TERM (default), 9=KILL]: 9
-> sent signal 9 to pid 4500.
```

### Example C — no permission / no such process (errno surfaces)
```
choice> 3
pid to signal: 1
signal [15=TERM (default), 9=KILL]:
ERROR: kill(1,15) failed (errno 1)
```
errno 1 = EPERM (not allowed to signal pid 1). A dead/unknown pid gives errno 3
(ESRCH). Verify a kill worked by re-running choice 2 and checking the pid is gone.

---

## 4) Create N worker threads — `pthread_create()`

Spawns N real kernel threads. Each prints its kernel thread id (`gettid`, syscall
186) and then loops sleeping until told to stop.

```
choice> 4                        «you type»
how many worker threads: 3       «you type»
   [worker 0] started  kernel-tid=4611
   [worker 1] started  kernel-tid=4612
   [worker 2] started  kernel-tid=4613
Created 3 thread(s). Total tracked: 3
```
The workers keep running in the background while you use other menu options.

---

## 5) List threads — read `/proc/self/task`

Every thread of the losys process appears as `/proc/self/task/<tid>`. You'll see the
main thread, the .NET runtime's own threads (GC, finalizer, tiered-JIT), and your
workers.

```
choice> 5                        «you type»
     TID  NAME
    4321  losys
    4322  .NET TP Worker
    4325  .NET Finalizer
    4611  losys
    4612  losys
    4613  losys
(9 threads in this process)
```
`4611/4612/4613` are the workers from choice 4 — their kernel tids match the
`kernel-tid=` values printed there.

---

## 6) Terminate all workers — stop flag + `pthread_join()`

Sets a shared stop flag (cooperative shutdown), then blocks in `pthread_join` until
each worker actually exits.

```
choice> 6                        «you type»
Signalling workers to stop...
   [worker 0] stopping after 7 ticks (tid=4611)
   [worker 1] stopping after 7 ticks (tid=4612)
   [worker 2] stopping after 7 ticks (tid=4613)
Joined 3 thread(s).
```
Run choice 5 again afterwards — the worker tids are gone. With no workers running:
```
choice> 6
No worker threads to terminate.
```

---

## 7) Cancel one worker — `pthread_cancel()`

Requests cancellation of a single worker by its index (0-based, the order created).
Cancellation lands at the next cancellation point (the `Thread.Sleep` inside the
loop).

```
choice> 4
how many worker threads: 2
   [worker 0] started  kernel-tid=4701
   [worker 1] started  kernel-tid=4702
Created 2 thread(s). Total tracked: 2

choice> 7                        «you type»
worker index to cancel: 1        «you type»
pthread_cancel sent to worker 1.
```
Worker 1's kernel thread is now torn down; a following choice 5 no longer lists tid
`4702`. (The remaining worker keeps running until choice 6.)

Bad index:
```
choice> 7
worker index to cancel: 9
No such worker index.
```

---

## 0) Exit

Cleanly stops and joins any remaining workers, then quits.

```
choice> 0                        «you type»
Signalling workers to stop...
Joined 1 thread(s).
```

---

## Suggested end-to-end demo (good for a report/screencast)

1. `4` → create 3 workers.
2. `5` → show them under `/proc/self/task`.
3. `7`, index `0` → cancel one; `5` again to prove it's gone.
4. `6` → join the rest cleanly.
5. `1` → `/bin/echo` `hello` (fork+execve round-trip).
6. In a second terminal: `sleep 300 &` — note its pid.
7. `2` → find that pid in the process list.
8. `3` → SIGKILL (`9`) it; `2` again to confirm it's gone.
