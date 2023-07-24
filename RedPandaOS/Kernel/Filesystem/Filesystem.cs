using System;
using Runtime.Collections;

namespace Kernel.IO
{
    public class Directory
    {
        private List<File> _files = new List<File>();
        private List<Directory> _directories = new List<Directory>();

        private ReadOnlyList<File> _readOnlyFiles;
        private ReadOnlyList<Directory> _readOnlyDirectories;

        public ReadOnlyList<File> Files
        {
            get
            {
                if (!Opened && OnOpen != null) OnOpen(this);
                if (_readOnlyFiles == null) _readOnlyFiles = new ReadOnlyList<File>(_files);
                return _readOnlyFiles;
            }
        }

        public ReadOnlyList<Directory> Directories
        {
            get
            {
                if (!Opened && OnOpen != null) OnOpen(this);
                if (_readOnlyDirectories == null) _readOnlyDirectories = new ReadOnlyList<Directory>(_directories);
                return _readOnlyDirectories;
            }
        }

        public void AddFile(File file)
        {
            _files.Add(file);
        }

        public void AddDirectory(Directory dir)
        {
            _directories.Add(dir);
        }

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

        public Action<File> OnOpen { get; set; } = null;

        public FAT32 FileSystem { get; set; } = null;

        public uint FilesystemInformation { get; set; }

        public uint Size { get; set; }

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

            Root.AddDirectory(new Directory("boot", Root));
            Root.AddDirectory(new Directory("dev", Root));
            Root.AddDirectory(new Directory("proc", Root));
        }
    }
}
