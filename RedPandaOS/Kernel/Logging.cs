using Kernel.Devices;

namespace Kernel
{
    public enum LogLevel : uint
    {
        Trace,
        Warning,
        Error,
        Panic
    }

    public static class Logging
    {
        public static LogLevel LoggingLevel { get; set; }

        static Logging()
        {
            LoggingLevel = LogLevel.Warning;
        }

        public static void WriteLine(LogLevel level, string s, uint u1, uint u2 = 0, uint u3 = 0)
        {
            if (level >= LoggingLevel)
            {
                VGA.WriteFormattedString(s, u1, u2, u3);
                VGA.WriteLine();
            }
        }

        public static void WriteLine(LogLevel level, string s)
        {
            if (level >= LoggingLevel)
            {
                VGA.WriteString(s);
                VGA.WriteLine();
            }
        }

        public static void Write(LogLevel level, string s, uint u1, uint u2 = 0, uint u3 = 0)
        {
            if (level >= LoggingLevel)
            {
                VGA.WriteFormattedString(s, u1, u2, u3);
            }
        }

        public static void Write(LogLevel level, string s)
        {
            if (level >= LoggingLevel)
            {
                VGA.WriteString(s);
            }
        }
    }
}
