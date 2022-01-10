using Kernel.IO;
using System;

namespace Applications
{
    public static class ls
    {
        public static void Run(Directory directory, Action<string> callback)
        {
            callback.Invoke(".");
            callback.Invoke("..");
            for (int i = 0; i < directory.Directories.Count; i++)
            {
                callback.Invoke(directory.Directories[i].Name);
            }
            for (int i = 0; i < directory.Contents.Count; i++)
            {
                callback.Invoke(directory.Contents[i].Name);
            }
        }
    }
}
