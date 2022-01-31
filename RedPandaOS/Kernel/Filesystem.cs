using System;
using Runtime.Collections;
using System.Text;

namespace Kernel.IO
{
    public class Directory
    {
        public List<File> Contents { get; private set; } = new List<File>();

        public List<Directory> Directories { get; private set; } = new List<Directory>();

        public string Name { get; private set; }

        public Directory Parent { get; private set; }

        public Action<Directory> OnOpen { get; set; } = null;

        public bool Opened { get; set; } = false;

        public uint FilesystemInformation { get; set; }

        public Directory(string name, Directory parent)
        {
            Name = name;
            Parent = parent;
        }

        public Directory()
        {

        }

        public string FullName
        {
            get
            {
                List<string> path = new List<string>();
                Directory dir = this;
                while (dir != null)
                {
                    if (dir.Name != null) path.Add("/" + dir.Name);
                    dir = dir.Parent;
                }

                if (path.Count > 0)
                {
                    string result = path[0];
                    for (int i = 1; i < path.Count; i++)
                    {
                        result = path[i] + result;
                    }

                    return result;
                }
                else return "/";
            }
        }
    }

    public class File
    {
        public string Name { get; private set; }

        public Directory Parent { get; private set; }

        public enum Type
        {
            File,
            SymbolicLink,
            Device,
            Mount
        }

        public uint Flags { get; set; }

        public File(string name, Directory parent)
        {
            Name = name;
            Parent = parent;
        }
    }

    public static class Filesystem
    {
        public static Directory Root;

        public static void Init()
        {
            Root = new Directory();

            Root.Directories.Add(new Directory("boot", Root));
            Root.Directories.Add(new Directory("dev", Root));
            Root.Directories.Add(new Directory("proc", Root));
        }
    }
}
