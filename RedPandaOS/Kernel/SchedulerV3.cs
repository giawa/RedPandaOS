using System;
using System.Reflection.Metadata;
using IL2Asm.BaseTypes;
using Kernel.Devices;
using Kernel.Memory;
using Runtime;
using Runtime.Collections;

namespace Kernel
{
    public class Scheduler
    {
        public enum TaskState
        {
            Uninitialized,
            FirstRun,
            ReadyToRun,
            Running,
            Waiting,
            Sleeping,
            Terminated
        }

        public class Task
        {
            //public uint EBP, ESP, EIP, EAX, ECX, EDX, EBX, ESI, EDI;
            public uint[] Stack; 
            public PageDirectory PageDirectory;
            public TaskState State;
            public uint StateInfo;
            public uint Id;
            public uint EntryPoint { get; private set; }

            private static uint _id = 1;

            public Task(uint entryPoint, PageDirectory pageDirectory)
            {
                Id = _id++;
                State = TaskState.FirstRun;

                PageDirectory = pageDirectory;
                EntryPoint = entryPoint;

                Stack = new uint[16];
            }
        }

        public static Task CurrentTask;

        public static Task GetCurrentTask()
        {
            if (CurrentTask == null)
            {
                /*CurrentTask = new Task(0x9000, Paging.KernelDirectory);
                CurrentTask.PageDirectory = Paging.CurrentDirectory;
                CurrentTask.ESP = CPUHelper.CPU.ReadESP();
                CurrentTask.EBP = CPUHelper.CPU.ReadEBP();
                CurrentTask.State = TaskState.Running;*/
                throw new Exception("No CurrentTask");
            }

            return CurrentTask;
        }

        private static List<Task> _runningTasks = new List<Task>();
        private static List<Task> _tasks = new List<Task>();
        private static int _currentTask = 0;
        public static Task IdleTask;

        private static void IdleTaskEntryPoint()
        {
            while (true)
            {
                //for (uint i = 0; i < 10000000; i++) ;
                //Logging.Write(LogLevel.Panic, "I");
                CPUHelper.CPU.Halt();
            }
        }

        public static void Add(Task task)
        {
            if (task.State == TaskState.ReadyToRun || task.State == TaskState.Running || task.State == TaskState.FirstRun) _runningTasks.Add(task);
            _tasks.Add(task);
        }

        public static bool PreemptiveScheduler = false;

        public static void Tick()
        {
            /*bool alreadyPreempted = false;

            for (int i = 0; i < _tasks.Count; i++)
            {
                if (_tasks[i].State == TaskState.Sleeping)
                {
                    if (_tasks[i].StateInfo <= PIT.TickCount)
                    {
                        // it's possible we preempt the idle thread while unblocking here,
                        // so if that happens then we do not need to run the scheduler at the end of Tick
                        alreadyPreempted = UnblockTask(_tasks[i]);
                    }
                }
            }*/

            Schedule();
        }

        public static void CreateIdleTask()
        {
            if (Paging.CurrentDirectory != Paging.KernelDirectory)
                throw new Exception("Must be called with kernel stack");

            using (InterruptDisabler.Instance)
            {
                var idlePagingDirectory = Paging.CloneDirectory(Paging.KernelDirectory);
                Paging.SwitchPageDirectory(idlePagingDirectory);
                var idleStack = Paging.GetPage(0xDEAD0000, true, idlePagingDirectory);
                Paging.AllocateFrame(idleStack, true, true);

                for (uint i = 0; i < 512; i += 4) CPUHelper.CPU.WriteMemInt(0xDEAD0000 + i, 0);

                Paging.SwitchPageDirectory(Paging.KernelDirectory);

                IdleTask = new Task(0xDEAD0000 + 512, idlePagingDirectory);
                //IdleTask.SetEntryPoint(IdleTaskEntryPoint);
            }
        }

        public static bool UseScheduler = false;

        // this happens inside an ISR so interrupts are already disabled for now
        public static void Schedule()
        {
            if (!UseScheduler) return;

            if (_runningTasks.Count == 0)
            {
                if (IdleTask == null) throw new Exception("No idle task");
                SwitchToTask(IdleTask);
            }
            else
            {
                _currentTask = (_currentTask + 1) % _runningTasks.Count;
                SwitchToTask(_runningTasks[_currentTask]);
            }
        }

        public static void TerminateTask(Task task)
        {
            task.State = TaskState.Terminated;
            _tasks.Remove(task);
            _runningTasks.Remove(task);
            Schedule();
        }

        [RequireStackFrame]
        public static void SwitchToTask(Task nextTask)
        {
            if (CurrentTask != null && CurrentTask == nextTask) return;

            Paging.SwitchPageDirectory(Paging.KernelDirectory);

            if (CurrentTask != null)
            {
                // store registers/etc of current task before switching
                for (uint i = 0; i < 16; i++)
                    CurrentTask.Stack[i] = CPUHelper.CPU.ReadMemInt(PIC.InterruptStackStart - (i * 4) - 4);
            }

            CurrentTask = nextTask;

            if (CurrentTask.State == TaskState.FirstRun)
            {
                CurrentTask.State = TaskState.Running;

                Paging.SwitchPageDirectory(CurrentTask.PageDirectory);
                CPUHelper.CPU.Sti();
                CPUHelper.CPU.JumpUserMode(CurrentTask.EntryPoint);
            }
            else if (CurrentTask.State == TaskState.Running)
            {
                // restore registers which also gets us set with the return instruction pointer for iret
                for (uint i = 0; i < 16; i++)
                {
                    CPUHelper.CPU.WriteMemInt(PIC.InterruptStackStart - (i * 4) - 4, CurrentTask.Stack[i]);
                }

                Paging.SwitchPageDirectory(CurrentTask.PageDirectory);
            }
        }
    }
}
