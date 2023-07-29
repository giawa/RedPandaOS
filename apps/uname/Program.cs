using Runtime;

namespace uname
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string uname = "RedPandaOS";

            for (int i = 0; i < uname.Length; i++)
                Syscalls.WriteCharToStdOutSysCall(uname[i]);

            while (true) ;
        }
    }
}