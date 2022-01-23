using Kernel.Devices;
using Runtime.Collections;

namespace Kernel
{
    public class Task
    {
        private static uint taskCounter = 1;

        public uint Id { get; private set; }
        public uint esp, ebp, eip;
        public Memory.PageDirectory pageDirectory;
        
        public Task()
        {
            Id = taskCounter++;
        }
    }

    public static class Scheduler
    {
        public static List<Task> Tasks;
        private static int LastTask = 0;
        public static Task CurrentTask = null;

        public static void Init()
        {
            Tasks = new List<Task>();

            // disable interrupts
            CPUHelper.CPU.Cli();

            // create a kernel task
            Task kernel = new Task();
            kernel.pageDirectory = Memory.Paging.CurrentDirectory;
            Tasks.Add(kernel);
            CurrentTask = kernel;

            // re-enable interrupts
            CPUHelper.CPU.Sti();
        }

        public static uint Fork()
        {
            CPUHelper.CPU.Cli();

            // create a new task
            Task task = new Task();
            task.pageDirectory = Memory.Paging.CloneDirectory(Memory.Paging.CurrentDirectory);

            uint eip = CPUHelper.CPU.ReadEIP();

            if (eip != 0xdeadc0de)
            {
                task.esp = CPUHelper.CPU.ReadESP();
                task.ebp = CPUHelper.CPU.ReadEBP();
                task.eip = eip;
                CPUHelper.CPU.Sti();
                Tasks.Add(task);

                return task.Id;
            }
            else
            {
                CPUHelper.CPU.Pop();
                CPUHelper.CPU.Sti();

                return 0;
            }
        }

        public static void Tick()//uint eip, uint esp, uint ebp)
        {
            //if (Tasks == null || Tasks.Count <= 1) return;
            if (Tasks == null) return;

            uint esp = CPUHelper.CPU.ReadESP();
            uint ebp = CPUHelper.CPU.ReadEBP();

            uint eip = CPUHelper.CPU.ReadEIP();

            // check if we've just switched back to this task
            if (eip == 0xdeadc0de)
            {
                return;
            }

            CurrentTask.esp = esp;
            CurrentTask.ebp = ebp;
            CurrentTask.eip = eip;

            // grab the next task
            LastTask = (LastTask + 1) % Tasks.Count;

            CurrentTask = Tasks[LastTask];
            //Logging.Write(LogLevel.Trace, "Setting task to {0} {1:X}", CurrentTask.Id, CurrentTask.pageDirectory.PhysicalAddress);
            //Logging.WriteLine(LogLevel.Trace, " {0:X} {1:X} {2:X}", CurrentTask.esp, CurrentTask.ebp, CurrentTask.eip);

            Memory.Paging.CurrentDirectory = CurrentTask.pageDirectory;

            CPUHelper.CPU.Cli();
            JumpWithDummy(CurrentTask.esp, CurrentTask.eip, CurrentTask.ebp, CurrentTask.pageDirectory.PhysicalAddress);
        }

        [IL2Asm.BaseTypes.AsmMethod]
        private static void JumpWithDummy(uint esp, uint eip, uint ebp, uint cr3)
        {

        }

        [IL2Asm.BaseTypes.AsmPlug("Kernel_Scheduler_JumpWithDummy_Void_U4_U4_U4_U4", IL2Asm.BaseTypes.Architecture.X86)]
        private static void JumpWithDummyAsm(IL2Asm.BaseTypes.IAssembledMethod assembly)
        {
            assembly.AddAsm("pop eax"); // page directory
            assembly.AddAsm("pop ebp"); // ebp
            assembly.AddAsm("pop ebx"); // eip
            assembly.AddAsm("pop esp"); // esp

            assembly.AddAsm("mov cr3, eax");
            assembly.AddAsm("mov eax, 0xdeadc0de");
            assembly.AddAsm("push eax");
            assembly.AddAsm("sti");
            assembly.AddAsm("jmp ebx");
        }
    }
}
