using System;
using IL2Asm.BaseTypes;
using Kernel.Devices;
using Kernel.Memory;
using Runtime.Collections;

namespace Kernel
{
    public class SchedulerV2
    {
        public enum TaskState
        {
            Uninitialized,
            FirstRun,
            ReadyToRun,
            Running,
            Waiting,
            Sleeping
        }

        public class Task
        {
            public uint EBP, ESP, EIP;
            public PageDirectory PageDirectory;
            public TaskState State;
            public uint StateInfo;

            public Task(uint stackTop, PageDirectory pageDirectory)
            {
                ESP = EBP = stackTop;
                PageDirectory = pageDirectory;
                State = TaskState.Uninitialized;
            }

            public void SetEntryPoint(Action entryPoint)
            {
                var currentDirectory = Paging.CurrentDirectory;
                Paging.SwitchPageDirectory(PageDirectory);

                //var actionSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Action>();
                var actionPtr = Memory.Utilities.ObjectToPtr(entryPoint);

                EIP = CPUHelper.CPU.ReadMemInt(actionPtr);

                // put the EIP into the new stack
                ESP -= 4;
                CPUHelper.CPU.WriteMemInt(ESP, EIP);

                // TODO:  If SwitchToTask creates any additional locals/etc on the stack then we need to add them here

                // EBP will be popped by SwitchToTask since SwitchToTask has a stack frame
                ESP -= 4;
                CPUHelper.CPU.WriteMemInt(ESP, EBP);

                Paging.SwitchPageDirectory(currentDirectory);

                State = TaskState.FirstRun;
            }
        }

        public static Task CurrentTask;

        public static Task GetCurrentTask()
        {
            if (CurrentTask == null)
            {
                CurrentTask = new Task(0x9000, Paging.KernelDirectory);
                CurrentTask.PageDirectory = Paging.CurrentDirectory;
                CurrentTask.ESP = CPUHelper.CPU.ReadESP();
                CurrentTask.EBP = CPUHelper.CPU.ReadEBP();
                CurrentTask.State = TaskState.Running;
            }

            return CurrentTask;
        }

        private static List<Task> _runningTasks = new List<Task>();
        private static List<Task> _tasks = new List<Task>();
        private static int _currentTask = 0;

        public static void Add(Task task)
        {
            if (task.State == TaskState.ReadyToRun || task.State == TaskState.Running || task.State == TaskState.FirstRun) _runningTasks.Add(task);
            _tasks.Add(task);
        }

        public static void Tick()
        {
            for (int i = 0; i < _tasks.Count; i++)
            {
                if (_tasks[i].State == TaskState.Sleeping)
                {
                    if (_tasks[i].StateInfo <= PIT.TickCount)
                    {
                        Lock();
                        _tasks[i].State = TaskState.ReadyToRun;
                        _runningTasks.Add(_tasks[i]);
                        Unlock();
                    }
                }
            }
        }

        public static void Sleep(Task task, uint milliseconds)
        {
            uint start = PIT.TickCount;
            uint end = start + PIT.Frequency * (uint)milliseconds / 1000;

            // TODO:  This has a bug where _currentTask will now point to the next task after removal of this task
            // which means the next task could be skipped and have to wait another full round before being run
            Lock();
            //_runningTasks.Remove(task);
            task.State = TaskState.Sleeping;
            task.StateInfo = end;
            Unlock();
            Schedule();
        }

        public static void Schedule()
        {
            Lock();

            if (_runningTasks.Count == 0)
            {
                // TODO: Run idle task
                throw new Exception("No idle task");
            }

            _currentTask = (_currentTask + 1) % _runningTasks.Count;
            SwitchToTask(_runningTasks[_currentTask]);

            Unlock();
        }

        private static int _locks = 0;

        private static void Lock()
        {
            // disable interrupts
            CPUHelper.CPU.Cli();
            _locks++;

            //Logging.WriteLine(LogLevel.Panic, "Lock {0}", (uint)_locks);
        }

        private static void Unlock()
        {
            _locks--;
            // re-enable interrupts
            if (_locks == 0) CPUHelper.CPU.Sti();
            if (_locks <= 0) _locks = 0;

            //Logging.WriteLine(LogLevel.Panic, "Unlock {0}", (uint)_locks);
        }

        public static void BlockTask(Task task, TaskState state)
        {
            Lock();
            task.State = state;
            Schedule();
            Unlock();
        }

        public static void UnblockTask(Task task)
        {
            Lock();
            
            if (task.State != TaskState.Running && task.State != TaskState.ReadyToRun)
            {
                _runningTasks.Add(task);
            }
            task.State = TaskState.ReadyToRun;

            Unlock();
        }

        [RequireStackFrame]
        public static void SwitchToTask(Task nextTask)
        {
            if (CurrentTask == null)
            {
                throw new Exception("No current task");
            }

            if (CurrentTask == nextTask) return;

            CurrentTask.ESP = CPUHelper.CPU.ReadESP();
            CurrentTask.EBP = CPUHelper.CPU.ReadESP();

            // set up page directories
            if (CurrentTask.PageDirectory.PhysicalAddress != nextTask.PageDirectory.PhysicalAddress)
            {
                // set this first since nextTask could be on the stack, and the stack will disappear on page directory change
                if (CurrentTask.State == TaskState.Running) CurrentTask.State = TaskState.ReadyToRun;
                else
                {
                    if (!_runningTasks.Remove(CurrentTask))
                    {
                        Logging.WriteLine(LogLevel.Error, "Tried to remove task but it was already removed");
                    }
                }
                
                CurrentTask = nextTask;
                
                Paging.CurrentDirectory = nextTask.PageDirectory;
                CPUHelper.CPU.SetPageDirectory(nextTask.PageDirectory.PhysicalAddress);
            }
            else CurrentTask = nextTask;

            CPUHelper.CPU.WriteEBP(CurrentTask.EBP);
            CPUHelper.CPU.WriteESP(CurrentTask.ESP);
            // FirstRun tasks need to unlock the scheduler first since all calls to schedule/etc are expected to be wrapped by lock/unlock
            // but unlock will not have been part of the entry point of the task, so we do the unlock here instead
            if (CurrentTask.State == TaskState.FirstRun) Unlock();
            CurrentTask.State = TaskState.Running;

            // ret at end of this function will pop the EIP for the nextTask
        }
    }
}
