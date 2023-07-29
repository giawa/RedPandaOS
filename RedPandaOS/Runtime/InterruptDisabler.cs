using Kernel;
using System;

namespace Runtime
{
    public class InterruptDisabler : IDisposable
    {
        private static int _locks = 0;

        private static void Lock()
        {
            // disable interrupts
            CPUHelper.CPU.Cli();
            _locks++;

            Logging.WriteLine(LogLevel.Panic, "Lock {0}", (uint)_locks);
        }

        private static void Unlock()
        {
            _locks--;
            // re-enable interrupts
            if (_locks == 0) CPUHelper.CPU.Sti();
            if (_locks <= 0) _locks = 0;

            Logging.WriteLine(LogLevel.Panic, "Unlock {0}", (uint)_locks);
        }

        public void Dispose()
        {
            Unlock();
        }

        private static InterruptDisabler _disabler = new InterruptDisabler();

        public static InterruptDisabler Instance
        {
            get 
            {
                Lock();
                return _disabler; 
            }
        }
    }
}
