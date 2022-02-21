//#define VGA_LOGGING
#define COM_LOGGING

using Kernel.Devices;

namespace Kernel
{
    public enum LogLevel : uint
    {
        Uninitialized,
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
#if COM_LOGGING
                COM.WriteFormattedString(s, u1, u2, u3);
                COM.WriteLine();
#endif
#if VGA_LOGGING
                VGA.WriteFormattedString(s, u1, u2, u3);
                VGA.WriteLine();
#endif
            }
        }

        public static void WriteLine(LogLevel level, string s)
        {
            if (level >= LoggingLevel)
            {
#if COM_LOGGING
                COM.WriteString(s);
                COM.WriteLine();
#endif
#if VGA_LOGGING
                VGA.WriteString(s);
                VGA.WriteLine();
#endif
            }
        }

        public static void Write(LogLevel level, string s, uint u1, uint u2 = 0, uint u3 = 0)
        {
            if (level >= LoggingLevel)
            {
#if COM_LOGGING
                COM.WriteFormattedString(s, u1, u2, u3);
#endif
#if VGA_LOGGING
                VGA.WriteFormattedString(s, u1, u2, u3);
#endif
            }
        }

        public static void Write(LogLevel level, string s)
        {
            if (level >= LoggingLevel)
            {
#if COM_LOGGING
                COM.WriteString(s);
#endif
#if VGA_LOGGING
                VGA.WriteString(s);
#endif
            }
        }
    }
}
