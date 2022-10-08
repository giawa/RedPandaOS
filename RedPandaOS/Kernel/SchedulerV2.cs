using System;
using IL2Asm.BaseTypes;
using Kernel.Memory;
using Runtime.Collections;

namespace Kernel
{
    public enum TaskState
    {
        Uninitialized,
        ReadyToRun,
        Running,
        Waiting,
        Sleeping
    }

    public class TaskV2
    {
        public uint EBP, ESP, EIP;
        public PageDirectory PageDirectory;
        public TaskState State;

        public TaskV2(uint stackTop, PageDirectory pageDirectory)
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

            State = TaskState.Uninitialized;
        }
    }

    public class SchedulerV2
    {
        public static TaskV2 CurrentTask;

        public static TaskV2 GetCurrentTask()
        {
            if (CurrentTask == null)
            {
                CurrentTask = new TaskV2(0x9000, Paging.KernelDirectory);
                CurrentTask.PageDirectory = Paging.CurrentDirectory;
                CurrentTask.ESP = CPUHelper.CPU.ReadESP();
                CurrentTask.EBP = CPUHelper.CPU.ReadEBP();
                CurrentTask.State = TaskState.Running;
            }

            return CurrentTask;
        }

        public static List<TaskV2> Tasks = new List<TaskV2>();
        public static List<TaskV2> SleepingTasks = new List<TaskV2>();
        private static int _currentTask = 0;

        public static void Schedule()
        {
            if (Tasks.Count == 0)
            {
                // TODO: Run idle task
                throw new Exception("No idle task");
            }

            _currentTask = (_currentTask + 1) % Tasks.Count;
            SwitchToTask(Tasks[_currentTask]);
        }

        [RequireStackFrame]
        public static void SwitchToTask(TaskV2 nextTask)
        {
            if (CurrentTask == null)
            {
                throw new Exception("No current task");
            }

            if (CurrentTask == nextTask) return;

            // disable interrupts
            CPUHelper.CPU.Cli();

            CurrentTask.ESP = CPUHelper.CPU.ReadESP();
            CurrentTask.EBP = CPUHelper.CPU.ReadESP();

            // set up page directories
            if (CurrentTask.PageDirectory.PhysicalAddress != nextTask.PageDirectory.PhysicalAddress)
            {
                // set this first since nextTask could be on the stack, and the stack will disappear on page directory change
                if (CurrentTask.State == TaskState.Running) CurrentTask.State = TaskState.ReadyToRun;
                else
                {
                    Tasks.Remove(CurrentTask);
                    SleepingTasks.Add(CurrentTask);
                }
                CurrentTask = nextTask;
                
                Paging.CurrentDirectory = nextTask.PageDirectory;
                CPUHelper.CPU.SetPageDirectory(nextTask.PageDirectory.PhysicalAddress);
            }
            else CurrentTask = nextTask;

            CPUHelper.CPU.WriteEBP(CurrentTask.EBP);
            CPUHelper.CPU.WriteESP(CurrentTask.ESP);
            CurrentTask.State = TaskState.Running;

            // re-enable interrupts
            CPUHelper.CPU.Sti();

            // ret at end of this function will pop the EIP for the nextTask
        }
    }
}
