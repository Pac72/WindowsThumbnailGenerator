using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Thumbnail_Generator_Library
{
    public class DirNode : IComparable<DirNode>
    {
        public List<DirNode> SubDirs { get; }
        public string Path { get; }
        public int Level { get; }

        public DirNode(string pth, int lvl)
        {
            Path = pth;
            Level = lvl;
            SubDirs = new();
        }

        public void Add(DirNode node)
        {
            SubDirs.Add(node);
        }

        public int Size()
        {
            int result = 1;
            foreach(DirNode node in SubDirs)
            {
                result += node.Size();
            }
            return result;
        }

        public int CompareTo(DirNode other)
        {
            int deltaLevel = other.Level - Level;
            if (deltaLevel != 0) {
                return deltaLevel;
            }

            return Path.CompareTo(other.Path);
        }

        public SortedDictionary<int, List<DirNode>> DepthFirstNodes()
        {
            SortedDictionary<int, List<DirNode>> dict = new();
            Visit(ref dict);
            return dict;
        }

        private void Visit(ref SortedDictionary<int, List<DirNode>> dict)
        {
            List<DirNode> nodes;
            if (!dict.TryGetValue(Level, out nodes))
            {
                nodes = new List<DirNode>();
                dict[Level] = nodes;
            }
            nodes.Add(this);
            foreach (DirNode node in SubDirs)
            {
                node.Visit(ref dict);
            }
        }

        override public string ToString()
        {
            return Level + ": " + Path;
        }
    }

    public class PathHandler
    {
        public static IEnumerable<string> GetAllDirectoriesOld(string rootDirectory, string searchPattern)
        {
            Stack<string> searchList = new();
            Stack<string> returnList = new();

            searchList.Push(rootDirectory);
            returnList.Push(rootDirectory);

            while (searchList.Count != 0)
            {
                string searchPath = searchList.Pop();
                try
                {
                    IEnumerable<string> subDirsEnumerable = Directory.EnumerateDirectories(searchPath, searchPattern);
                    List<string> subDir = new(subDirsEnumerable);
                    foreach (string directory in subDir) {
                        searchList.Push(directory);
                        if (IsDirectoryWritable(directory)) returnList.Push(directory);
                    }
                }
                catch { }
            }

            return returnList;
        }

        private static readonly EnumerationOptions RECURSE_OPTIONS = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            ReturnSpecialDirectories = false
        };

        public static IEnumerable<string> GetAllDirectories(string rootDirectory, string searchPattern)
        {
            DirectoryInfo di = new DirectoryInfo(rootDirectory);

            IEnumerable<string> directories = di.EnumerateDirectories(searchPattern, RECURSE_OPTIONS)
                .Where(di => KeepDirectory(di))
                .Select(di => di.FullName);

            return directories;
        }

        private static readonly EnumerationOptions NO_RECURSE_OPTIONS = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false
        };

        public static DirNode GetDirTree(string rootDirectory, string searchPattern)
        {
            DirNode rootNode = new DirNode(rootDirectory, 0);

            BuildDirTree(ref rootNode, searchPattern);

            return rootNode;
        }

        private static void BuildDirTree(ref DirNode parent, string searchPattern)
        {
            DirectoryInfo di = new DirectoryInfo(parent.Path);

            IEnumerable<string> directories = di.EnumerateDirectories(searchPattern, NO_RECURSE_OPTIONS)
                .Where(di => KeepDirectory(di))
                .Select(di => di.FullName);

            foreach(string dir in directories)
            {
                DirNode node = new DirNode(dir, parent.Level + 1);
                parent.Add(node);
                BuildDirTree(ref node, searchPattern);
            }
        }

        private static bool KeepDirectory(DirectoryInfo di)
        {
            if (IsReparsed(di))
            {
                return false;
            }

            return true;
        }

        private static bool IsReparsed(DirectoryInfo di)
        {
            return (di.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        }

        public static bool IsDirectoryWritable(string dirPath)
        {
            try
            {
                string randomFileName = Path.Combine(dirPath, Path.GetRandomFileName());
                using (FileStream fs = File.Create(randomFileName, 1, FileOptions.DeleteOnClose)) { }
                return true;
            }
            catch
            {
                 return false;
            }
        }
    }
}
