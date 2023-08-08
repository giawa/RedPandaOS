using System;
using IL2Asm.BaseTypes;
using Kernel.Devices;
using Kernel.Memory;
using Runtime.Collections;

namespace Kernel
{
    public class Scheduler
    {
        public enum TaskState
        {
            Uninitialized,
            FirstRun,
            Ready,
            Running,
            Waiting,
            Sleeping,
            Terminated
        }

        public class Task
        {
            // stores the stack as handed to Schedule by the interrupt service routine
            // ss, useresp, eflags, cs, eip, err_code, int_no, eax, ecx, edx, ebx, esp, ebp, esi, edi, ds
            public uint[] Stack;    
            public PageDirectory PageDirectory;
            public TaskState State;
            public uint StateInfo;
            public uint Id;
            public uint EntryPoint { get; private set; }
            public uint PrivilegeLevel { get; internal set; } = 3;  // user mode by default

            private static uint _id = 1;

            public Task(uint entryPoint, PageDirectory pageDirectory)
            {
                Id = _id++;
                State = TaskState.FirstRun;

                PageDirectory = pageDirectory;
                EntryPoint = entryPoint;

                Stack = new uint[16];
            }

            public Task(Action entryPoint, PageDirectory pageDirectory)
            {
                Id = _id++;
                State = TaskState.FirstRun;

                PageDirectory = pageDirectory;
                EntryPoint = CPUHelper.CPU.ReadMemInt(Runtime.Memory.Utilities.ObjectToPtr(entryPoint));

                Stack = new uint[16];
            }
        }

        public static Task CurrentTask;

        public static Task GetCurrentTask()
        {
            if (CurrentTask == null)
            {
                throw new Exception("No CurrentTask");
            }

            return CurrentTask;
        }

        private static List<Task> _tasks = new List<Task>();
        private static int _currentTask = 0;
        public static Task IdleTask;

        private static void IdleTaskEntryPoint()
        {
            while (true)
            {
                CPUHelper.CPU.Halt();
            }
        }

        public static void Add(Task task)
        {
            _tasks.Add(task);
        }

        public static bool PreemptiveScheduler = false;

        public static void Tick()
        {
            Schedule();
        }

        private static void CreateIdleTask()
        {
            Paging.SwitchPageDirectory(Paging.KernelDirectory);

            IdleTask = new Task(new Action(IdleTaskEntryPoint), Paging.KernelDirectory);
            IdleTask.PrivilegeLevel = 0;
        }

        public static bool UseScheduler = false;
        private static List<Task> PossibleTasks;

        // this happens inside an ISR so interrupts are already disabled for now
        public static void Schedule()
        {
            if (PossibleTasks == null) PossibleTasks = new List<Task>();
            if (!UseScheduler) return;

            PossibleTasks.Clear();

            for (int i = 0; i < _tasks.Count; i++)
            {
                if (_tasks[i].State == TaskState.FirstRun || _tasks[i].State == TaskState.Ready)
                    PossibleTasks.Add(_tasks[i]);
            }

            if (PossibleTasks.Count == 0)
            {
                if (CurrentTask != null) return;    // leave the current task running
                if (IdleTask == null) CreateIdleTask();

                SwitchToTask(IdleTask);
            }
            else
            {
                _currentTask = (_currentTask + 1) % PossibleTasks.Count;
                SwitchToTask(PossibleTasks[_currentTask]);
            }
        }

        public static void TerminateTask(Task task)
        {
            task.State = TaskState.Terminated;
            _tasks.Remove(task);

            CurrentTask = null;

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

                if (CurrentTask.State == TaskState.Running)
                    CurrentTask.State = TaskState.Ready;
            }

            CurrentTask = nextTask;

            if (CurrentTask.State == TaskState.FirstRun)
            {
                // set the privilege level for this task
                CurrentTask.State = TaskState.Running;

                Paging.SwitchPageDirectory(CurrentTask.PageDirectory);
                CPUHelper.CPU.Sti();
                if (CurrentTask.PrivilegeLevel == 3) CPUHelper.CPU.JumpUserMode(CurrentTask.EntryPoint);
                else CPUHelper.CPU.JumpKernelMode(CurrentTask.EntryPoint);
            }
            else if (CurrentTask.State == TaskState.Ready)
            {
                CurrentTask.State = TaskState.Running;

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
