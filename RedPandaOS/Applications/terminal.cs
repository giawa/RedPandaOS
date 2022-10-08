using Kernel.IO;
using Kernel.Devices;

namespace Applications
{
    public class terminal
    {
        private Directory _currentDirectory;

        public void WriteLine(string s)
        {
            VGA.WriteString(s);
            VGA.WriteLine();
        }

        private void cd(char[] command)
        {
            if (command[3] == '.' && command[4] == '.')
            {
                if (_currentDirectory.Parent != null) _currentDirectory = _currentDirectory.Parent;
            }
            else if (command[3] != '.')
            {
                Directory newDirectory = null;

                for (int i = 0; i < _currentDirectory.Directories.Count && newDirectory == null; i++)
                {
                    string comparison = _currentDirectory.Directories[i].Name;
                    for (int j = 0; j < comparison.Length; j++)
                    {
                        if (comparison[j] != command[j + 3]) break;

                        if (j == comparison.Length - 1 && command[j + 4] == 0)
                            newDirectory = _currentDirectory.Directories[i];
                    }
                }

                if (newDirectory != null)
                {
                    _currentDirectory = newDirectory;
                    if (_currentDirectory.OnOpen != null)
                    {
                        _currentDirectory.OnOpen(_currentDirectory);
                    }
                }
                else
                {
                    VGA.WriteString("Could not find directory");
                    VGA.WriteLine();
                }
            }
        }

        private void uname()
        {
            VGA.WriteString("RedPandaOS");
            VGA.WriteLine();
        }

        private void cad()
        {
            VGA.WriteString("Yes?");
            VGA.WriteLine();
        }

        private bool CompareCommand(char[] command, string comparison)
        {
            int i = 0;

            for (i = 0; i < comparison.Length; i++)
            {
                if (command[i] != comparison[i]) return false;
            }

            if (command[i] != ' ' && command[i] != 0) return false;

            return true;
        }

        public void Run(Directory dir)
        {
            //Kernel.Logging.LoggingLevel = Kernel.LogLevel.Trace;

            //VGA.Clear();
            VGA.EnableScrolling = true;
            VGA.EnableCursor();
            //VGA.ResetPosition();

            _currentDirectory = dir;

            //VGA.WriteHex(Kernel.Memory.Utilities.ObjectToPtr(dir));
            char[] command = new char[256];
            Kernel.Memory.TraceableHeap trace = new Kernel.Memory.TraceableHeap();
            //Kernel.Memory.KernelHeap.KernelAllocator.AddTrace(trace);
            //Kernel.Memory.KernelHeap.KernelAllocator.RemoveTrace(trace);

            //Kernel.Logging.LoggingLevel = Kernel.LogLevel.Trace;

            while (true)
            {
                //Kernel.Memory.KernelHeap.KernelAllocator.AddTrace(trace);
                {
                    string directory = _currentDirectory.FullName;
                    if (directory == null) directory = "/";

                    VGA.WriteString("giawa@redpandaos", 0x0A00);
                    VGA.WriteChar(':');
                    VGA.WriteString(directory, 0x0B00);
                    VGA.WriteString("$ ");
                }
                //trace.Dispose();

                int pos = 0;// message.Length;
                int index = 0;
                System.Array.Clear(command, 0, command.Length);

                while (true)
                {
                    // wait for enter key
                    while (Keyboard.KeyQueue.Count == 0)
                    {
                        //Kernel.Scheduler.Yield();
                    }

                    var key = Keyboard.KeyQueue.Dequeue();

                    if (key == 0x0D)
                    {
                        VGA.WriteLine();

                        if (CompareCommand(command, "ls"))
                        {
                            System.Action<string> onWrite = new System.Action<string>(WriteLine);
                            ls.Run(_currentDirectory, onWrite);
                            Kernel.Memory.KernelHeap.KernelAllocator.Free(Kernel.Memory.Utilities.ObjectToPtr(onWrite), 8);
                        }
                        else if (CompareCommand(command, "cd"))
                            cd(command);
                        else if (CompareCommand(command, "uname"))
                            uname();
                        else if (CompareCommand(command, "cad"))
                            cad();
                        else if (CompareCommand(command, "stack"))
                        {
                            var esp = CPUHelper.CPU.ReadESP();
                            do
                            {
                                VGA.WriteFormattedString("{0:X} : {1:X}", esp, CPUHelper.CPU.ReadMemInt(esp), 0);
                                VGA.WriteLine();
                                esp += 4;
                            } while ((esp & 0xfff) != 0);
                        }
                        else
                        {
                            VGA.WriteString("Unknown command");
                            VGA.WriteLine();
                        }
                        break;
                    }
                    else if (key >= 32 && key <= 126)
                    {
                        VGA.WriteChar(key);
                        command[index++] = key;
                    }
                    else if (key == 0x08)
                    {
                        if (index > 0)
                        {
                            command[index] = '\0';
                            index--;
                            VGA.Delete();
                            VGA.WriteChar(' ');
                            VGA.Delete();
                        }
                    }
                    else
                    {
                        VGA.WriteString(" 0x");
                        VGA.WriteHex(key);
                    }
                }
            }
        }
    }
}
